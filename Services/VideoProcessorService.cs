using System;
using System.Diagnostics;
using System.Globalization; // For parsing double and formatting TimeSpan
using System.IO;
using System.Runtime.InteropServices; // For OS checks
using System.Text;
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

            if (File.Exists(bundledFfmpeg) &amp;&amp; File.Exists(bundledFfprobe))
            {
                _logger.LogInfo($"Found potential candidates in bundled directory: {bundledBinPath}");
                if (await VerifyExecutableAsync(bundledFfmpeg) &amp;&amp; await VerifyExecutableAsync(bundledFfprobe))
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

            if (!string.IsNullOrEmpty(pathFfmpeg) &amp;&amp; !string.IsNullOrEmpty(pathFfprobe))
            {
                 _logger.LogInfo($"Found potential candidates in PATH: FFmpeg='{pathFfmpeg}', FFprobe='{pathFfprobe}'");
                 if (await VerifyExecutableAsync(pathFfmpeg) &amp;&amp; await VerifyExecutableAsync(pathFfprobe))
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

                // On Windows, check for extensions like .exe, .cmd, .bat if not provided
                string[] extensions = OperatingSystem.IsWindows() &amp;&amp; !Path.HasExtension(executableName)
                                        ? new[] { ".exe", ".cmd", ".bat", "" }
                                        : new[] { "" }; // On Linux/macOS, exact name usually needed

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
                StandardOutputEncoding = Encoding.UTF8, // Specify encoding
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

                // Wait for exit with a timeout (e.g., 5 seconds)
                bool exited = await Task.Run(() => process.WaitForExit(5000));

                if (!exited)
                {
                    try { process.Kill(true); } catch { /* Ignore errors killing process */ }
                    _logger.LogWarning($"Verification failed: Process '{executablePath} -version' timed out.");
                    return false;
                }

                string output = outputBuilder.ToString();
                string error = errorBuilder.ToString();
                string name = Path.GetFileNameWithoutExtension(executablePath).ToLowerInvariant();

                // FFmpeg/FFprobe usually print version info to stderr
                if (process.ExitCode == 0 &amp;&amp; (output.Contains(name) || error.Contains(name)) &amp;&amp; (output.Contains("version") || error.Contains("version")))
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


        public async Task ExtractClipAsync(string inputFile, string outputFile, TimeSpan start, TimeSpan duration, bool formatAsVertical, IProgress<VideoProcessingProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug($"Attempting to extract clip: Input='{inputFile}', Output='{outputFile}', Start={start}, Duration={duration}, Vertical={formatAsVertical}");
            if (!IsAvailable || string.IsNullOrEmpty(FfmpegPath))
            {
                 _logger.LogError("Cannot extract clip: FFmpeg is not available or path is invalid.");
                 throw new InvalidOperationException("FFmpeg is not available or path is invalid.");
            }
            if (!File.Exists(inputFile))
            {
                 _logger.LogError($"Cannot extract clip: Input file not found at '{inputFile}'.");
                 throw new FileNotFoundException("Input video file not found.", inputFile);
            }

            // Ensure output directory exists
            try
            {
                string? outputDir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError($"Failed to create output directory for '{outputFile}': {ex.Message}", ex);
                 throw new IOException($"Failed to create output directory: {ex.Message}", ex);
            }


            // Construct FFmpeg arguments
            // Using CultureInfo.InvariantCulture for formatting numbers to avoid locale issues (e.g., comma vs dot decimal separator)
            var argsBuilder = new StringBuilder();
            argsBuilder.Append($"-ss {start.TotalSeconds.ToString(CultureInfo.InvariantCulture)} "); // Seek to start time
            argsBuilder.Append($"-i \"{inputFile}\" "); // Input file
            argsBuilder.Append($"-t {duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} "); // Duration

            // Video Filters (apply if formatting vertically)
            if (formatAsVertical)
            {
                // Crop to 9:16 aspect ratio (centered), then scale to 1080x1920, set SAR
                argsBuilder.Append("-vf \"crop=ih*9/16:ih,scale=1080:1920,setsar=1\" ");
                // Re-encoding is required for filters
                argsBuilder.Append("-c:v libx264 -preset medium -crf 23 "); // Video codec options
            }
            else
            {
                // If not formatting, try to copy codecs for speed (might fail depending on source/container)
                argsBuilder.Append("-c copy "); // Attempt codec copy
            }

            // Audio Codec (use AAC if re-encoding video or copying fails)
            // If copying video, try copying audio too. If re-encoding video, encode audio.
            if (formatAsVertical) // Always re-encode audio if re-encoding video
            {
                 argsBuilder.Append("-c:a aac -b:a 128k ");
            }
            else // If attempting video copy, attempt audio copy too
            {
                 argsBuilder.Append("-c:a copy ");
            }


            argsBuilder.Append("-movflags +faststart "); // Optimize for web/streaming
            argsBuilder.Append("-y "); // Overwrite output without asking
            argsBuilder.Append($"\"{outputFile}\""); // Output file

            string arguments = argsBuilder.ToString();
            _logger.LogDebug($"Running FFmpeg command: {FfmpegPath} {arguments}");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true, // Can be used for basic progress if needed
                RedirectStandardError = true, // FFmpeg often outputs progress/errors here
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using var process = new Process { StartInfo = processStartInfo };
                var errorBuilder = new StringBuilder(); // Capture stderr for errors/progress

                // Use TaskCompletionSource for asynchronous waiting
                var tcs = new TaskCompletionSource<bool>();

                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcs.TrySetResult(true); // Signal completion

                // TODO: Implement robust progress parsing from FFmpeg's stderr if needed.
                // This is complex as FFmpeg's output format can vary.
                // For now, we just capture errors.
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        errorBuilder.AppendLine(args.Data);
                        // Basic progress reporting placeholder (can be improved)
                        progress?.Report(new VideoProcessingProgress { Message = args.Data });
                    }
                };

                process.Start();
                process.BeginErrorReadLine(); // Start reading stderr

                // Asynchronously wait for the process to exit or cancellation
                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            _logger.LogWarning($"Cancellation requested for FFmpeg process (PID: {process.Id}). Attempting to kill.");
                            process.Kill(true); // Kill process tree if cancellation requested
                            tcs.TrySetCanceled(cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                         _logger.LogError($"Error attempting to kill FFmpeg process on cancellation: {ex.Message}", ex);
                         tcs.TrySetCanceled(cancellationToken); // Still signal cancellation
                    }
                }))
                {
                    await tcs.Task; // Wait for Exited event or cancellation
                }

                string errorOutput = errorBuilder.ToString();

                if (process.ExitCode != 0)
                {
                    // Check if failure was due to codec copy attempt, and retry with re-encoding if so
                    bool codecCopyFailed = !formatAsVertical &amp;&amp; errorOutput.Contains("codec copy") &amp;&amp; errorOutput.Contains("unsupported");
                    if (codecCopyFailed)
                    {
                         _logger.LogWarning($"Codec copy failed for '{outputFile}'. Retrying with re-encoding...");
                         // Construct arguments for re-encoding (similar to vertical format but without crop/scale)
                         argsBuilder.Clear();
                         argsBuilder.Append($"-ss {start.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
                         argsBuilder.Append($"-i \"{inputFile}\" ");
                         argsBuilder.Append($"-t {duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
                         argsBuilder.Append("-c:v libx264 -preset medium -crf 23 "); // Re-encode video
                         argsBuilder.Append("-c:a aac -b:a 128k "); // Re-encode audio
                         argsBuilder.Append("-movflags +faststart ");
                         argsBuilder.Append("-y ");
                         argsBuilder.Append($"\"{outputFile}\"");
                         arguments = argsBuilder.ToString();
                         _logger.LogDebug($"Retrying FFmpeg command: {FfmpegPath} {arguments}");
                         processStartInfo.Arguments = arguments;

                         // Re-run the process
                         using var retryProcess = new Process { StartInfo = processStartInfo };
                         var retryErrorBuilder = new StringBuilder();
                         var retryTcs = new TaskCompletionSource<bool>();
                         retryProcess.EnableRaisingEvents = true;
                         retryProcess.Exited += (s, a) => retryTcs.TrySetResult(true);
                         retryProcess.ErrorDataReceived += (s, a) => { if (a.Data != null) retryErrorBuilder.AppendLine(a.Data); };
                         retryProcess.Start();
                         retryProcess.BeginErrorReadLine();
                         using (cancellationToken.Register(() => { try { if (!retryProcess.HasExited) retryProcess.Kill(true); } catch { } retryTcs.TrySetCanceled(cancellationToken); }))
                         {
                             await retryTcs.Task;
                         }
                         errorOutput = retryErrorBuilder.ToString(); // Update error output
                         if (retryProcess.ExitCode != 0)
                         {
                              _logger.LogError($"FFmpeg failed on retry for '{outputFile}'. Exit Code: {retryProcess.ExitCode}. Error: {errorOutput}");
                              throw new Exception($"FFmpeg failed to extract clip (even on retry). Exit Code: {retryProcess.ExitCode}. See logs for details.");
                         }
                         _logger.LogInfo($"Successfully extracted clip '{outputFile}' on retry (re-encoding).");
                    }
                    else // Failed for other reasons
                    {
                        _logger.LogError($"FFmpeg failed for '{outputFile}'. Exit Code: {process.ExitCode}. Error: {errorOutput}");
                        throw new Exception($"FFmpeg failed to extract clip. Exit Code: {process.ExitCode}. See logs for details.");
                    }
                }
                else
                {
                    _logger.LogInfo($"Successfully extracted clip '{outputFile}'.");
                }
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInfo($"ExtractClipAsync cancelled for '{outputFile}'.");
                 // Optionally delete partially created outputFile here
                 try { if (File.Exists(outputFile)) File.Delete(outputFile); } catch (Exception delEx) { _logger.LogWarning($"Failed to delete partial output file '{outputFile}' on cancellation: {delEx.Message}"); }
                 throw; // Re-throw cancellation exception
            }
            catch (Exception ex) when (ex is not OperationCanceledException) // Avoid double logging cancellation
            {
                _logger.LogError($"Error extracting clip for '{outputFile}': {ex.Message}", ex);
                 // Optionally delete partially created outputFile here
                 try { if (File.Exists(outputFile)) File.Delete(outputFile); } catch (Exception delEx) { _logger.LogWarning($"Failed to delete partial output file '{outputFile}' after error: {delEx.Message}"); }
                throw; // Re-throw other exceptions
            }
        }


        public async Task<TimeSpan?> GetVideoDurationAsync(string inputFile, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug($"Attempting to get duration for: {inputFile}");
            if (!IsAvailable || string.IsNullOrEmpty(FfprobePath))
            {
                 _logger.LogError("Cannot get video duration: FFprobe is not available or path is invalid.");
                 throw new InvalidOperationException("FFprobe is not available or path is invalid.");
            }
             if (!File.Exists(inputFile))
             {
                 _logger.LogError($"Cannot get video duration: Input file not found at '{inputFile}'.");
                 throw new FileNotFoundException("Input video file not found.", inputFile);
             }

            // Command arguments: -v error (suppress info), -show_entries format=duration (get duration), -of default... (format output)
            string arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{inputFile}\"";
            _logger.LogDebug($"Running FFprobe command: {FfprobePath} {arguments}");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = FfprobePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Capture errors too
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using var process = new Process { StartInfo = processStartInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder(); // Capture stderr

                // Use TaskCompletionSource for asynchronous waiting
                var tcs = new TaskCompletionSource<bool>();

                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcs.TrySetResult(true); // Signal completion

                process.OutputDataReceived += (sender, args) => { if (args.Data != null) outputBuilder.Append(args.Data); }; // Append directly, might be single line
                process.ErrorDataReceived += (sender, args) => { if (args.Data != null) errorBuilder.AppendLine(args.Data); }; // Capture errors

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Asynchronously wait for the process to exit or cancellation
                using (cancellationToken.Register(() =>
                {
                     try { if (!process.HasExited) process.Kill(true); } catch { } // Attempt to kill on cancel
                     tcs.TrySetCanceled(cancellationToken);
                }))
                {
                    await tcs.Task; // Wait for Exited event or cancellation
                }


                string output = outputBuilder.ToString().Trim();
                string errorOutput = errorBuilder.ToString().Trim();

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogError($"FFprobe failed for '{inputFile}'. Exit Code: {process.ExitCode}. Output: '{output}'. Error: '{errorOutput}'");
                    return null;
                }

                // Parse the duration (should be in seconds, e.g., "123.456")
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
            catch (OperationCanceledException)
            {
                 _logger.LogInfo($"GetVideoDurationAsync cancelled for '{inputFile}'.");
                 return null; // Or rethrow if cancellation should propagate as exception
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting video duration for '{inputFile}': {ex.Message}", ex);
                return null; // Return null on error
            }
        }

        public async Task CombineVideoAudioSubtitlesAsync(
            string backgroundVideoPath,
            string audioPath,
            string subtitlePath, // Assuming subtitles are pre-generated in a file
            string outputPath,
            IProgress<VideoProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
             _logger.LogDebug($"Attempting to combine inputs for AI Short: Video='{backgroundVideoPath}', Audio='{audioPath}', Subtitles='{subtitlePath}', Output='{outputPath}'");
             if (!IsAvailable || string.IsNullOrEmpty(FfmpegPath) || string.IsNullOrEmpty(FfprobePath))
             {
                 _logger.LogError("Cannot combine: FFmpeg/FFprobe is not available or path is invalid.");
                 throw new InvalidOperationException("FFmpeg/FFprobe is not available or path is invalid.");
             }
             if (!File.Exists(backgroundVideoPath)) throw new FileNotFoundException("Background video file not found.", backgroundVideoPath);
             if (!File.Exists(audioPath)) throw new FileNotFoundException("Audio file not found.", audioPath);
             if (!File.Exists(subtitlePath)) throw new FileNotFoundException("Subtitle file not found.", subtitlePath);

             // 1. Get Audio Duration (determines output length)
             TimeSpan? audioDuration = await GetVideoDurationAsync(audioPath, cancellationToken); // Use the same method for audio files
             if (audioDuration == null)
             {
                 _logger.LogError($"Could not determine duration of audio file: {audioPath}");
                 throw new Exception($"Could not determine duration of audio file: {audioPath}");
             }
             double audioDurationSeconds = audioDuration.Value.TotalSeconds;
             _logger.LogInfo($"Audio duration determined: {audioDuration.Value}");


             // 2. Ensure output directory exists
             try
             {
                 string? outputDir = Path.GetDirectoryName(outputPath);
                 if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);
             }
             catch (Exception ex)
             {
                  _logger.LogError($"Failed to create output directory for '{outputPath}': {ex.Message}", ex);
                  throw new IOException($"Failed to create output directory: {ex.Message}", ex);
             }

             // 3. Construct FFmpeg Command
             var argsBuilder = new StringBuilder();

             // Input 0: Looping Background Video
             argsBuilder.Append($"-stream_loop -1 -i \"{backgroundVideoPath}\" ");
             // Input 1: Audio
             argsBuilder.Append($"-i \"{audioPath}\" ");
             // Input 2: Subtitles (will be applied via filter)
             // Note: Subtitle path needs escaping for the filter graph
             string escapedSubtitlePath = subtitlePath.Replace("\\", "/").Replace(":", "\\:"); // Basic escaping for FFmpeg filters

             // Filter Complex Graph
             argsBuilder.Append("-filter_complex \"");
             // Crop/Scale video [0:v] -> [bg]
             argsBuilder.Append("[0:v]crop=ih*9/16:ih,scale=1080:1920,setsar=1");
             // Overlay subtitles onto scaled video [bg] -> [outv]
             // Use 'force_style' if needed for ASS styling overrides
             argsBuilder.Append($",subtitles='{escapedSubtitlePath}'"); // Apply subtitles filter
             argsBuilder.Append("[outv]\""); // End filter complex, label output video stream

             // Mapping
             argsBuilder.Append(" -map \"[outv]\" "); // Map filtered video
             argsBuilder.Append("-map 1:a "); // Map audio from second input (audio file)

             // Encoding & Output Options
             argsBuilder.Append($"-t {audioDurationSeconds.ToString(CultureInfo.InvariantCulture)} "); // Set duration based on audio
             argsBuilder.Append("-c:v libx264 -preset medium -crf 23 "); // Video codec
             argsBuilder.Append("-c:a aac -b:a 128k "); // Audio codec
             argsBuilder.Append("-movflags +faststart ");
             argsBuilder.Append("-y "); // Overwrite output
             argsBuilder.Append($"\"{outputPath}\""); // Output file

             string arguments = argsBuilder.ToString();
             _logger.LogDebug($"Running FFmpeg command for combination: {FfmpegPath} {arguments}");

             // 4. Execute Process (similar to ExtractClipAsync)
             var processStartInfo = new ProcessStartInfo
             {
                 FileName = FfmpegPath,
                 Arguments = arguments,
                 RedirectStandardError = true, // Capture progress/errors
                 UseShellExecute = false,
                 CreateNoWindow = true,
                 StandardErrorEncoding = Encoding.UTF8
             };

             try
             {
                 using var process = new Process { StartInfo = processStartInfo };
                 var errorBuilder = new StringBuilder();
                 var tcs = new TaskCompletionSource<bool>();

                 process.EnableRaisingEvents = true;
                 process.Exited += (sender, args) => tcs.TrySetResult(true);

                 // TODO: Implement progress parsing from FFmpeg stderr
                 process.ErrorDataReceived += (sender, args) =>
                 {
                     if (args.Data != null)
                     {
                         errorBuilder.AppendLine(args.Data);
                         progress?.Report(new VideoProcessingProgress { Message = args.Data });
                     }
                 };

                 process.Start();
                 process.BeginErrorReadLine();

                 using (cancellationToken.Register(() =>
                 {
                     try { if (!process.HasExited) { process.Kill(true); _logger.LogWarning($"Killed FFmpeg process (PID: {process.Id}) due to cancellation."); } }
                     catch (Exception ex) { _logger.LogError($"Error killing FFmpeg process on cancellation: {ex.Message}", ex); }
                     tcs.TrySetCanceled(cancellationToken);
                 }))
                 {
                     await tcs.Task;
                 }

                 string errorOutput = errorBuilder.ToString();
                 if (process.ExitCode != 0)
                 {
                     _logger.LogError($"FFmpeg failed during combination for '{outputPath}'. Exit Code: {process.ExitCode}. Error: {errorOutput}");
                     throw new Exception($"FFmpeg failed during combination. Exit Code: {process.ExitCode}. See logs.");
                 }

                 _logger.LogInfo($"Successfully combined inputs into '{outputPath}'.");
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

    }
}