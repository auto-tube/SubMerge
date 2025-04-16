using System;
using System.Collections.Generic; // For List
using System.Diagnostics;
using System.Globalization; // For parsing double and formatting TimeSpan
using System.IO;
using System.Runtime.InteropServices; // For OS checks
using System.Text;
using System.Text.RegularExpressions; // For parsing scene detection output & progress
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    public class VideoProcessorService : IVideoProcessorService
    {
        private readonly ILoggerService _logger;
        private string? _ffmpegPath;
        private string? _ffprobePath;
        private bool _isAvailable;

        // Regex to capture pts_time and lavfi.scene_score from ffmpeg output
        private static readonly Regex SceneDetectRegex = new Regex(@"pts_time:(?<time>[\d\.]+)\s+.*lavfi\.scene_score=(?<score>[\d\.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // Regex to capture time=HH:MM:SS.ms from ffmpeg progress output
        private static readonly Regex FfmpegProgressRegex = new Regex(@"time=(?<time>[\d]{2}:[\d]{2}:[\d]{2}\.[\d]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);


        public string? FfmpegPath => _ffmpegPath;
        public string? FfprobePath => _ffprobePath;
        public bool IsAvailable => _isAvailable;

        public VideoProcessorService(ILoggerService logger)
        {
            _logger = logger;
            _logger.LogInfo("VideoProcessorService created.");
        }

        public async Task<bool> InitializeAsync()
        {
            _logger.LogInfo("Initializing VideoProcessorService: Locating FFmpeg and FFprobe...");
            _isAvailable = false; // Reset availability
            _ffmpegPath = null;
            _ffprobePath = null;

            string ffmpegExe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            string ffprobeExe = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

            // 1. Check bundled 'bin' directory
            string baseDirectory = AppContext.BaseDirectory;
            string bundledBinPath = Path.Combine(baseDirectory, "bin"); // Relative to executable
             _logger.LogDebug($"Checking bundled path: {bundledBinPath}");
            string bundledFfmpeg = Path.Combine(bundledBinPath, ffmpegExe);
            string bundledFfprobe = Path.Combine(bundledBinPath, ffprobeExe);

            if (File.Exists(bundledFfmpeg) && File.Exists(bundledFfprobe))
            {
                _logger.LogInfo($"Found potential candidates in bundled directory: {bundledBinPath}");
                if (await VerifyExecutableAsync(bundledFfmpeg) && await VerifyExecutableAsync(bundledFfprobe))
                {
                    _ffmpegPath = bundledFfmpeg;
                    _ffprobePath = bundledFfprobe;
                    _isAvailable = true;
                    _logger.LogInfo($"FFmpeg/FFprobe verified successfully from bundled directory: '{_ffmpegPath}', '{_ffprobePath}'");
                    return true;
                }
                 _logger.LogWarning("Bundled candidates found but failed verification.");
            }
            else
            {
                 _logger.LogDebug("FFmpeg/FFprobe not found in bundled 'bin' directory.");
            }


            // 2. Check system PATH
            _logger.LogDebug("Checking system PATH for FFmpeg/FFprobe...");
            string? pathFfmpeg = FindExecutableInPath(ffmpegExe);
            string? pathFfprobe = FindExecutableInPath(ffprobeExe);

            if (!string.IsNullOrEmpty(pathFfmpeg) && !string.IsNullOrEmpty(pathFfprobe))
            {
                 _logger.LogInfo($"Found potential candidates in PATH: FFmpeg='{pathFfmpeg}', FFprobe='{pathFfprobe}'");
                 if (await VerifyExecutableAsync(pathFfmpeg) && await VerifyExecutableAsync(pathFfprobe))
                 {
                     _ffmpegPath = pathFfmpeg;
                     _ffprobePath = pathFfprobe;
                     _isAvailable = true;
                     _logger.LogInfo($"FFmpeg/FFprobe verified successfully from system PATH.");
                     return true;
                 }
                  _logger.LogWarning("PATH candidates found but failed verification.");
            }
             else
            {
                 _logger.LogDebug("FFmpeg/FFprobe not found in system PATH.");
            }

            // If we reach here, neither method worked
            _logger.LogError("FFmpeg/FFprobe could not be located or verified. Video processing will be unavailable.");
            return false;
        }

        private string? FindExecutableInPath(string executableName)
        {
            try
            {
                string? pathVar = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrEmpty(pathVar)) return null;

                char pathSeparator = OperatingSystem.IsWindows() ? ';' : ':';
                string[] paths = pathVar.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries);

                string[] extensions = OperatingSystem.IsWindows() && !Path.HasExtension(executableName)
                                        ? new[] { ".exe", ".cmd", ".bat", "" }
                                        : new[] { "" };

                foreach (string path in paths)
                {
                    foreach (string ext in extensions)
                    {
                        string fullPath = Path.Combine(path.Trim(), executableName + ext);
                        if (File.Exists(fullPath))
                        {
                            _logger.LogDebug($"Found '{executableName}' at: {fullPath}");
                            return fullPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error searching PATH for '{executableName}': {ex.Message}");
            }
            return null;
        }

        private async Task<bool> VerifyExecutableAsync(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            {
                _logger.LogWarning($"Verification skipped: Path '{executablePath ?? "null"}' is invalid or file doesn't exist.");
                return false;
            }

            _logger.LogDebug($"Verifying executable: {executablePath}");
            var processStartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using var process = new Process { StartInfo = processStartInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, args) => { if (args.Data != null) outputBuilder.AppendLine(args.Data); };
                process.ErrorDataReceived += (sender, args) => { if (args.Data != null) errorBuilder.AppendLine(args.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool exited = await Task.Run(() => process.WaitForExit(5000));

                if (!exited)
                {
                    try { process.Kill(true); } catch { /* Ignore */ }
                    _logger.LogWarning($"Verification failed: Process '{executablePath} -version' timed out.");
                    return false;
                }

                string output = outputBuilder.ToString();
                string error = errorBuilder.ToString();
                string name = Path.GetFileNameWithoutExtension(executablePath).ToLowerInvariant();

                if (process.ExitCode == 0 && (output.Contains(name) || error.Contains(name)) && (output.Contains("version") || error.Contains("version")))
                {
                    _logger.LogDebug($"Verification successful for: {executablePath}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Verification failed for: {executablePath}. Exit Code: {process.ExitCode}. Output: {output.Split('\n')[0]}. Error: {error.Split('\n')[0]}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Verification error for '{executablePath}': {ex.Message}", ex);
                return false;
            }
        }

        // Updated ExtractClipAsync signature and implementation
        public async Task ExtractClipAsync(
            string inputFile,
            string outputFile,
            TimeSpan start,
            TimeSpan duration,
            bool formatAsVertical,
            bool removeAudio,
            bool mirrorVideo,
            bool enhanceVideo,
            IProgress<VideoProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug($"Attempting to extract clip: Input='{inputFile}', Output='{outputFile}', Start={start}, Duration={duration}, Vertical={formatAsVertical}, NoAudio={removeAudio}, Mirror={mirrorVideo}, Enhance={enhanceVideo}");
            if (!IsAvailable || string.IsNullOrEmpty(FfmpegPath)) throw new InvalidOperationException("FFmpeg is not available or path is invalid.");
            if (!File.Exists(inputFile)) throw new FileNotFoundException("Input video file not found.", inputFile);

            try { string? outputDir = Path.GetDirectoryName(outputFile); if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir); }
            catch (Exception ex) { _logger.LogError($"Failed to create output directory for '{outputFile}': {ex.Message}", ex); throw new IOException($"Failed to create output directory: {ex.Message}", ex); }

            var argsBuilder = new StringBuilder();
            argsBuilder.Append($"-ss {start.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
            argsBuilder.Append($"-i \"{inputFile}\" ");
            argsBuilder.Append($"-t {duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");

            // --- Build Video Filters ---
            var videoFilters = new List<string>();
            if (formatAsVertical) videoFilters.Add("crop=ih*9/16:ih,scale=1080:1920,setsar=1");
            if (mirrorVideo) videoFilters.Add("hflip");
            if (enhanceVideo) videoFilters.Add("eq=contrast=1.1:saturation=1.1");

            bool applyVideoFilters = videoFilters.Any();
            if (applyVideoFilters)
            {
                argsBuilder.Append($"-vf \"{string.Join(",", videoFilters)}\" ");
            }

            // --- Determine Codecs ---
            bool reEncodeVideo = applyVideoFilters; // Must re-encode if any video filters are applied
            bool reEncodeAudio = reEncodeVideo || removeAudio; // Must re-encode audio if video is re-encoded (unless removing audio)
            bool attemptCodecCopy = !reEncodeVideo && !removeAudio; // Only attempt copy if no filters and audio not removed

            if (reEncodeVideo) argsBuilder.Append("-c:v libx264 -preset medium -crf 23 ");
            else argsBuilder.Append("-c:v copy ");

            if (removeAudio) argsBuilder.Append("-an ");
            else if (reEncodeAudio) argsBuilder.Append("-c:a aac -b:a 128k ");
            else argsBuilder.Append("-c:a copy ");

            argsBuilder.Append("-movflags +faststart ");
            argsBuilder.Append("-y ");
            argsBuilder.Append($"\"{outputFile}\"");

            string arguments = argsBuilder.ToString();
            _logger.LogDebug($"Running FFmpeg command: {FfmpegPath} {arguments}");

            var processStartInfo = new ProcessStartInfo { /* ... */
                FileName = FfmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            DateTime startTime = DateTime.UtcNow;

            try
            {
                using var process = new Process { StartInfo = processStartInfo };
                var errorBuilder = new StringBuilder();
                var tcs = new TaskCompletionSource<bool>();

                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcs.TrySetResult(true);

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        errorBuilder.AppendLine(args.Data);
                        if (progress != null)
                        {
                            var match = FfmpegProgressRegex.Match(args.Data);
                            if (match.Success && TimeSpan.TryParseExact(match.Groups["time"].Value, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture, out TimeSpan currentTime))
                            {
                                double totalDurationSeconds = duration.TotalSeconds;
                                double currentSeconds = currentTime.TotalSeconds;
                                double percentage = totalDurationSeconds > 0 ? Math.Clamp(currentSeconds / totalDurationSeconds, 0.0, 1.0) : 0.0;
                                TimeSpan? eta = null;
                                double elapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
                                if (percentage > 0.01 && elapsedSeconds > 1)
                                {
                                    double totalEstimatedSeconds = elapsedSeconds / percentage;
                                    eta = TimeSpan.FromSeconds(Math.Max(0, totalEstimatedSeconds - elapsedSeconds));
                                }
                                progress.Report(new VideoProcessingProgress { Percentage = percentage, CurrentTime = currentTime, TotalDuration = duration, EstimatedRemaining = eta, Message = $"Extracting clip... {percentage:P1}" });
                            } else { progress.Report(new VideoProcessingProgress { Message = args.Data }); }
                        }
                    }
                };

                process.Start();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(() => { try { if (!process.HasExited) { process.Kill(true); _logger.LogWarning($"Killed FFmpeg process (PID: {process.Id}) due to cancellation."); } } catch (Exception ex) { _logger.LogError($"Error killing FFmpeg process on cancellation: {ex.Message}", ex); } tcs.TrySetCanceled(cancellationToken); }))
                { await tcs.Task; }

                string errorOutput = errorBuilder.ToString();

                if (process.ExitCode != 0)
                {
                    bool codecCopyFailedError = errorOutput.Contains("codec copy") && errorOutput.Contains("unsupported");

                    if (attemptCodecCopy && codecCopyFailedError) // Retry only if initial attempt was copy and failed specifically due to that
                    {
                         _logger.LogWarning($"Codec copy failed for '{outputFile}'. Retrying with re-encoding...");
                         argsBuilder.Clear();
                         argsBuilder.Append($"-ss {start.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
                         argsBuilder.Append($"-i \"{inputFile}\" ");
                         argsBuilder.Append($"-t {duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
                         // No filters applied in this case, so no -vf needed
                         argsBuilder.Append("-c:v libx264 -preset medium -crf 23 "); // Force video re-encode
                         argsBuilder.Append("-c:a aac -b:a 128k "); // Force audio re-encode
                         argsBuilder.Append("-movflags +faststart ");
                         argsBuilder.Append("-y ");
                         argsBuilder.Append($"\"{outputFile}\"");
                         arguments = argsBuilder.ToString();
                         _logger.LogDebug($"Retrying FFmpeg command: {FfmpegPath} {arguments}");
                         processStartInfo.Arguments = arguments;

                         using var retryProcess = new Process { StartInfo = processStartInfo };
                         var retryErrorBuilder = new StringBuilder();
                         var retryTcs = new TaskCompletionSource<bool>();
                         retryProcess.EnableRaisingEvents = true;
                         retryProcess.Exited += (s, a) => retryTcs.TrySetResult(true);
                         retryProcess.ErrorDataReceived += (s, a) => { if (a.Data != null) retryErrorBuilder.AppendLine(a.Data); };
                         retryProcess.Start();
                         retryProcess.BeginErrorReadLine();
                         using (cancellationToken.Register(() => { try { if (!retryProcess.HasExited) retryProcess.Kill(true); } catch { } retryTcs.TrySetCanceled(cancellationToken); }))
                         { await retryTcs.Task; }
                         errorOutput = retryErrorBuilder.ToString();
                         if (retryProcess.ExitCode != 0)
                         {
                              _logger.LogError($"FFmpeg failed on retry for '{outputFile}'. Exit Code: {retryProcess.ExitCode}. Error: {errorOutput}");
                              throw new Exception($"FFmpeg failed to extract clip (even on retry). Exit Code: {retryProcess.ExitCode}. See logs for details.");
                         }
                         _logger.LogInfo($"Successfully extracted clip '{outputFile}' on retry (re-encoding).");
                    }
                    else
                    {
                        _logger.LogError($"FFmpeg failed for '{outputFile}'. Exit Code: {process.ExitCode}. Error: {errorOutput}");
                        throw new Exception($"FFmpeg failed to extract clip. Exit Code: {process.ExitCode}. See logs for details.");
                    }
                }
                else
                {
                    _logger.LogInfo($"Successfully extracted clip '{outputFile}'.");
                    progress?.Report(new VideoProcessingProgress { Percentage = 1.0, Message = "Clip extracted." });
                }
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInfo($"ExtractClipAsync cancelled for '{outputFile}'.");
                 try { if (File.Exists(outputFile)) File.Delete(outputFile); } catch (Exception delEx) { _logger.LogWarning($"Failed to delete partial output file '{outputFile}' on cancellation: {delEx.Message}"); }
                 throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError($"Error extracting clip for '{outputFile}': {ex.Message}", ex);
                 try { if (File.Exists(outputFile)) File.Delete(outputFile); } catch (Exception delEx) { _logger.LogWarning($"Failed to delete partial output file '{outputFile}' after error: {delEx.Message}"); }
                throw;
            }
        }


        public async Task<TimeSpan?> GetVideoDurationAsync(string inputFile, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug($"Attempting to get duration for: {inputFile}");
            if (!IsAvailable || string.IsNullOrEmpty(FfprobePath)) throw new InvalidOperationException("FFprobe is not available or path is invalid.");
            if (!File.Exists(inputFile)) throw new FileNotFoundException("Input video file not found.", inputFile);

            string arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{inputFile}\"";
            _logger.LogDebug($"Running FFprobe command: {FfprobePath} {arguments}");

            var processStartInfo = new ProcessStartInfo { /* ... */
                FileName = FfprobePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using var process = new Process { StartInfo = processStartInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                var tcs = new TaskCompletionSource<bool>();

                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcs.TrySetResult(true);

                process.OutputDataReceived += (sender, args) => { if (args.Data != null) outputBuilder.Append(args.Data); };
                process.ErrorDataReceived += (sender, args) => { if (args.Data != null) errorBuilder.AppendLine(args.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } tcs.TrySetCanceled(cancellationToken); }))
                { await tcs.Task; }

                string output = outputBuilder.ToString().Trim();
                string errorOutput = errorBuilder.ToString().Trim();

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogError($"FFprobe failed for '{inputFile}'. Exit Code: {process.ExitCode}. Output: '{output}'. Error: '{errorOutput}'");
                    return null;
                }

                if (double.TryParse(output, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double durationSeconds))
                {
                    var duration = TimeSpan.FromSeconds(durationSeconds);
                    _logger.LogInfo($"Successfully retrieved duration for '{inputFile}': {duration}");
                    return duration;
                }
                else
                {
                    _logger.LogError($"FFprobe returned unexpected output for duration: '{output}' for file '{inputFile}'.");
                    return null;
                }
            }
            catch (OperationCanceledException) { _logger.LogInfo($"GetVideoDurationAsync cancelled for '{inputFile}'."); return null; }
            catch (Exception ex) { _logger.LogError($"Error getting video duration for '{inputFile}': {ex.Message}", ex); return null; }
        }

        public async Task CombineVideoAudioSubtitlesAsync(
            string backgroundVideoPath,
            string audioPath,
            string subtitlePath,
            string outputPath,
            IProgress<VideoProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
             _logger.LogDebug($"Attempting to combine inputs for AI Short: Video='{backgroundVideoPath}', Audio='{audioPath}', Subtitles='{subtitlePath}', Output='{outputPath}'");
             if (!IsAvailable || string.IsNullOrEmpty(FfmpegPath) || string.IsNullOrEmpty(FfprobePath)) throw new InvalidOperationException("FFmpeg/FFprobe is not available or path is invalid.");
             if (!File.Exists(backgroundVideoPath)) throw new FileNotFoundException("Background video file not found.", backgroundVideoPath);
             if (!File.Exists(audioPath)) throw new FileNotFoundException("Audio file not found.", audioPath);
             if (!File.Exists(subtitlePath)) throw new FileNotFoundException("Subtitle file not found.", subtitlePath);

             TimeSpan? audioDuration = await GetVideoDurationAsync(audioPath, cancellationToken);
             if (audioDuration == null) throw new Exception($"Could not determine duration of audio file: {audioPath}");
             double audioDurationSeconds = audioDuration.Value.TotalSeconds;
             _logger.LogInfo($"Audio duration determined: {audioDuration.Value}");

             try { string? outputDir = Path.GetDirectoryName(outputPath); if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir); }
             catch (Exception ex) { _logger.LogError($"Failed to create output directory for '{outputPath}': {ex.Message}", ex); throw new IOException($"Failed to create output directory: {ex.Message}", ex); }

             var argsBuilder = new StringBuilder();
             argsBuilder.Append($"-stream_loop -1 -i \"{backgroundVideoPath}\" ");
             argsBuilder.Append($"-i \"{audioPath}\" ");
             string escapedSubtitlePath = subtitlePath.Replace("\\", "/").Replace(":", "\\:");

             argsBuilder.Append("-filter_complex \"");
             argsBuilder.Append("[0:v]crop=ih*9/16:ih,scale=1080:1920,setsar=1");
             argsBuilder.Append($",subtitles='{escapedSubtitlePath}'");
             argsBuilder.Append("[outv]\"");

             argsBuilder.Append(" -map \"[outv]\" ");
             argsBuilder.Append("-map 1:a ");
             argsBuilder.Append($"-t {audioDurationSeconds.ToString(CultureInfo.InvariantCulture)} ");
             argsBuilder.Append("-c:v libx264 -preset medium -crf 23 ");
             argsBuilder.Append("-c:a aac -b:a 128k ");
             argsBuilder.Append("-movflags +faststart ");
             argsBuilder.Append("-y ");
             argsBuilder.Append($"\"{outputPath}\"");

             string arguments = argsBuilder.ToString();
             _logger.LogDebug($"Running FFmpeg command for combination: {FfmpegPath} {arguments}");

             var processStartInfo = new ProcessStartInfo { /* ... */
                FileName = FfmpegPath,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8
             };
             DateTime startTime = DateTime.UtcNow;

             try
             {
                 using var process = new Process { StartInfo = processStartInfo };
                 var errorBuilder = new StringBuilder();
                 var tcs = new TaskCompletionSource<bool>();

                 process.EnableRaisingEvents = true;
                 process.Exited += (sender, args) => tcs.TrySetResult(true);

                 process.ErrorDataReceived += (sender, args) =>
                 {
                     if (args.Data != null)
                     {
                         errorBuilder.AppendLine(args.Data);
                         if (progress != null)
                         {
                             var match = FfmpegProgressRegex.Match(args.Data);
                             if (match.Success && TimeSpan.TryParseExact(match.Groups["time"].Value, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture, out TimeSpan currentTime))
                             {
                                 double totalDurationSeconds = audioDuration.Value.TotalSeconds;
                                 double currentSeconds = currentTime.TotalSeconds;
                                 double percentage = totalDurationSeconds > 0 ? Math.Clamp(currentSeconds / totalDurationSeconds, 0.0, 1.0) : 0.0;
                                 TimeSpan? eta = null;
                                 double elapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
                                 if (percentage > 0.01 && elapsedSeconds > 1)
                                 {
                                     double totalEstimatedSeconds = elapsedSeconds / percentage;
                                     eta = TimeSpan.FromSeconds(Math.Max(0, totalEstimatedSeconds - elapsedSeconds));
                                 }
                                 progress.Report(new VideoProcessingProgress { Percentage = percentage, CurrentTime = currentTime, TotalDuration = audioDuration, EstimatedRemaining = eta, Message = $"Combining... {percentage:P1}" });
                             } else { progress.Report(new VideoProcessingProgress { Message = args.Data }); }
                         }
                     }
                 };

                 process.Start();
                 process.BeginErrorReadLine();

                 using (cancellationToken.Register(() => { try { if (!process.HasExited) { process.Kill(true); _logger.LogWarning($"Killed FFmpeg process (PID: {process.Id}) due to cancellation."); } } catch (Exception ex) { _logger.LogError($"Error killing FFmpeg process on cancellation: {ex.Message}", ex); } tcs.TrySetCanceled(cancellationToken); }))
                 { await tcs.Task; }

                 string errorOutput = errorBuilder.ToString();
                 if (process.ExitCode != 0)
                 {
                     _logger.LogError($"FFmpeg failed during combination for '{outputPath}'. Exit Code: {process.ExitCode}. Error: {errorOutput}");
                     throw new Exception($"FFmpeg failed during combination. Exit Code: {process.ExitCode}. See logs.");
                 }

                 _logger.LogInfo($"Successfully combined inputs into '{outputPath}'.");
                 progress?.Report(new VideoProcessingProgress { Percentage = 1.0, Message = "Combination complete." });
             }
             catch (OperationCanceledException)
             {
                  _logger.LogInfo($"CombineVideoAudioSubtitlesAsync cancelled for '{outputPath}'.");
                  try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch (Exception delEx) { _logger.LogWarning($"Failed to delete partial output file '{outputPath}' on cancellation: {delEx.Message}"); }
                  throw;
             }
             catch (Exception ex) when (ex is not OperationCanceledException)
             {
                 _logger.LogError($"Error combining inputs for '{outputPath}': {ex.Message}", ex);
                  try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch (Exception delEx) { _logger.LogWarning($"Failed to delete partial output file '{outputPath}' after error: {delEx.Message}"); }
                 throw;
             }
        }

        public async Task<List<SceneChangeInfo>?> DetectScenesAsync(string inputFile, double threshold = 0.4, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug($"Attempting to detect scenes in: {inputFile} with threshold {threshold}");
            if (!IsAvailable || string.IsNullOrEmpty(FfmpegPath)) throw new InvalidOperationException("FFmpeg is not available or path is invalid.");
            if (!File.Exists(inputFile)) throw new FileNotFoundException("Input video file not found.", inputFile);

            string thresholdString = threshold.ToString(CultureInfo.InvariantCulture);
            string arguments = $"-i \"{inputFile}\" -vf \"select='gt(scene,{thresholdString})',metadata=print:key=lavfi.scene_score\" -an -f null -";
            _logger.LogDebug($"Running FFmpeg command for scene detection: {FfmpegPath} {arguments}");

            var processStartInfo = new ProcessStartInfo { /* ... */
                FileName = FfmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8
            };

            var sceneChanges = new List<SceneChangeInfo>();
            try
            {
                using var process = new Process { StartInfo = processStartInfo };
                var errorOutput = new StringBuilder();
                var tcs = new TaskCompletionSource<bool>();

                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcs.TrySetResult(true);

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        errorOutput.AppendLine(args.Data);
                        var match = SceneDetectRegex.Match(args.Data);
                        if (match.Success)
                        {
                            if (double.TryParse(match.Groups["time"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double time) &&
                                double.TryParse(match.Groups["score"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double score))
                            {
                                sceneChanges.Add(new SceneChangeInfo { TimestampSeconds = time, Score = score });
                            }
                        }
                    }
                };

                process.Start();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(() => { try { if (!process.HasExited) { process.Kill(true); _logger.LogWarning($"Killed FFmpeg scene detect process (PID: {process.Id}) due to cancellation."); } } catch (Exception ex) { _logger.LogError($"Error killing FFmpeg scene detect process on cancellation: {ex.Message}", ex); } tcs.TrySetCanceled(cancellationToken); }))
                { await tcs.Task; }

                if (process.ExitCode != 0)
                {
                    if (!sceneChanges.Any())
                    {
                        _logger.LogError($"FFmpeg scene detection failed for '{inputFile}'. Exit Code: {process.ExitCode}. Error: {errorOutput}");
                        return null;
                    }
                    else
                    {
                         _logger.LogWarning($"FFmpeg scene detection process exited with code {process.ExitCode} for '{inputFile}', but scene changes were parsed. Proceeding with results. Error output: {errorOutput}");
                    }
                }

                _logger.LogInfo($"Scene detection complete for '{inputFile}'. Found {sceneChanges.Count} potential scene changes.");
                if (!sceneChanges.Any(s => Math.Abs(s.TimestampSeconds) < 0.001))
                {
                    sceneChanges.Insert(0, new SceneChangeInfo { TimestampSeconds = 0.0, Score = null });
                }
                return sceneChanges.OrderBy(s => s.TimestampSeconds).ToList();
            }
            catch (OperationCanceledException) { _logger.LogInfo($"Scene detection cancelled for '{inputFile}'."); return null; }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError($"Error detecting scenes for '{inputFile}': {ex.Message}", ex); return null; }
        }

    }
}