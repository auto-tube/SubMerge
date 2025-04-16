using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Implements ISubtitleService to generate timed SRT subtitle files.
    /// </summary>
    public class SubtitleService : ISubtitleService
    {
        private readonly ILoggerService _logger;
        private static readonly Regex SentenceSplitRegex = new Regex(@"(?<!\w\.\w.)(?<![A-Z][a-z]\.)(?<=\.|\?|!)\s", RegexOptions.Compiled);

        public SubtitleService(ILoggerService logger)
        {
            _logger = logger;
            _logger.LogInfo("SubtitleService created.");
        }

        // Updated signature to accept speech marks
        public async Task GenerateSrtFileAsync(
            string scriptText,
            TimeSpan totalDuration,
            string outputSrtPath,
            List<SpeechMark>? speechMarks = null, // Added optional speech marks
            int wordsPerMinute = 150,
            int maxCharsPerLine = 42,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(scriptText)) throw new ArgumentException("Script text cannot be empty.", nameof(scriptText));
            if (totalDuration <= TimeSpan.Zero) throw new ArgumentException("Total duration must be positive.", nameof(totalDuration));
            if (string.IsNullOrWhiteSpace(outputSrtPath)) throw new ArgumentException("Output SRT path cannot be empty.", nameof(outputSrtPath));
            if (wordsPerMinute <= 0) throw new ArgumentException("Words per minute must be positive.", nameof(wordsPerMinute));
            if (maxCharsPerLine <= 0) throw new ArgumentException("Max chars per line must be positive.", nameof(maxCharsPerLine));

            _logger.LogInfo($"Generating SRT file at '{outputSrtPath}'. Using speech marks: {speechMarks != null && speechMarks.Any()}");

            var srtBuilder = new StringBuilder();
            int sequenceNumber = 1;

            // --- Generate SRT Entries ---
            bool usedSpeechMarks = false;
            if (speechMarks != null && speechMarks.Any(m => m.Type == "sentence" || m.Type == "word"))
            {
                // --- Use Speech Marks for Timing ---
                _logger.LogDebug("Attempting to generate SRT using speech marks.");
                var sentenceMarks = speechMarks.Where(m => m.Type == "sentence").OrderBy(m => m.Time).ToList();

                if (sentenceMarks.Any())
                {
                    _logger.LogDebug($"Using {sentenceMarks.Count} sentence marks for timing.");
                    for (int i = 0; i < sentenceMarks.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var currentMark = sentenceMarks[i];
                        // End time is start time of next sentence mark, or total duration for the last one.
                        // Subtract a small buffer (e.g., 50ms) to avoid overlap.
                        var nextMarkTimeMs = (i + 1 < sentenceMarks.Count)
                            ? sentenceMarks[i + 1].Time
                            : (int)totalDuration.TotalMilliseconds;

                        TimeSpan startTime = TimeSpan.FromMilliseconds(currentMark.Time);
                        TimeSpan endTime = TimeSpan.FromMilliseconds(Math.Max(currentMark.Time + 50, nextMarkTimeMs - 50)); // Ensure min duration and buffer

                        // Clamp end time to total duration
                        if (endTime > totalDuration) endTime = totalDuration;
                        // Ensure end time is after start time
                        if (endTime <= startTime) endTime = startTime + TimeSpan.FromMilliseconds(500); // Minimum display time if overlap occurs

                        string formattedText = FormatTextForSrt(currentMark.Value, maxCharsPerLine);

                        srtBuilder.AppendLine(sequenceNumber.ToString());
                        srtBuilder.AppendLine($"{startTime:hh\\:mm\\:ss\\,fff} --> {endTime:hh\\:mm\\:ss\\,fff}");
                        srtBuilder.AppendLine(formattedText);
                        srtBuilder.AppendLine();
                        sequenceNumber++;
                    }
                    usedSpeechMarks = true;
                }
                else // Fallback to grouping word marks if no sentence marks
                {
                    _logger.LogDebug("No sentence marks found, attempting to group word marks.");
                    var wordMarks = speechMarks.Where(m => m.Type == "word").OrderBy(m => m.Time).ToList();
                    if (wordMarks.Any())
                    {
                        var currentLine = new StringBuilder();
                        TimeSpan lineStartTime = TimeSpan.Zero;
                        int lineStartIndex = 0;

                        for (int i = 0; i < wordMarks.Count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var currentWordMark = wordMarks[i];
                            if (currentLine.Length == 0)
                            {
                                lineStartTime = TimeSpan.FromMilliseconds(currentWordMark.Time);
                                lineStartIndex = i; // Remember the index of the first word in the line
                            }

                            string wordToAdd = currentWordMark.Value + " ";
                            bool isLastWord = (i == wordMarks.Count - 1);

                            // Check if adding the next word exceeds the line limit OR if it's the last word
                            if ((currentLine.Length + wordToAdd.Length > maxCharsPerLine && currentLine.Length > 0) || isLastWord)
                            {
                                // If it's the last word, add it before finalizing
                                if (isLastWord) currentLine.Append(wordToAdd);

                                // Finalize previous line
                                // End time is the start time of the current word (which starts the *next* line), minus buffer
                                // Or, if it's the very last word, use the total duration.
                                TimeSpan lineEndTime = isLastWord
                                                    ? totalDuration
                                                    : TimeSpan.FromMilliseconds(currentWordMark.Time - 10);

                                // Ensure minimum duration and end time is after start time
                                if (lineEndTime <= lineStartTime) lineEndTime = lineStartTime + TimeSpan.FromMilliseconds(500);
                                // Clamp to total duration
                                if (lineEndTime > totalDuration) lineEndTime = totalDuration;


                                string formattedText = FormatTextForSrt(currentLine.ToString().Trim(), maxCharsPerLine); // Format the built line

                                srtBuilder.AppendLine(sequenceNumber.ToString());
                                srtBuilder.AppendLine($"{lineStartTime:hh\\:mm\\:ss\\,fff} --> {lineEndTime:hh\\:mm\\:ss\\,fff}");
                                srtBuilder.AppendLine(formattedText);
                                srtBuilder.AppendLine();
                                sequenceNumber++;

                                // Start new line only if it wasn't the last word
                                if (!isLastWord)
                                {
                                    currentLine.Clear().Append(wordToAdd.Trim());
                                    lineStartTime = TimeSpan.FromMilliseconds(currentWordMark.Time);
                                    lineStartIndex = i;
                                }
                                else
                                {
                                    currentLine.Clear(); // Clear if it was the last word
                                }
                            }
                            else
                            {
                                currentLine.Append(wordToAdd);
                            }
                        }
                        usedSpeechMarks = true;
                    }
                }
            }

            // --- Fallback to Estimated Timing if speech marks weren't used ---
            if (!usedSpeechMarks)
            {
                _logger.LogWarning("No usable speech marks found or provided. Falling back to estimated timing (WPM).");
                await GenerateSrtByEstimationAsync(srtBuilder, scriptText, totalDuration, wordsPerMinute, maxCharsPerLine, cancellationToken);
            }


            // --- Write File ---
            try
            {
                string? outputDir = Path.GetDirectoryName(outputSrtPath);
                if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);
                await File.WriteAllTextAsync(outputSrtPath, srtBuilder.ToString(), Encoding.UTF8, cancellationToken);
                _logger.LogInfo($"Successfully generated SRT file: {outputSrtPath}");
            }
            catch (IOException ioEx) { _logger.LogError($"IO error writing SRT file '{outputSrtPath}': {ioEx.Message}", ioEx); throw; }
            catch (Exception ex) { _logger.LogError($"Unexpected error generating SRT file '{outputSrtPath}': {ex.Message}", ex); throw new Exception($"Failed to generate SRT file: {ex.Message}", ex); }
        }

        // Extracted estimation logic into a separate method
        private async Task GenerateSrtByEstimationAsync(
             StringBuilder srtBuilder,
             string scriptText,
             TimeSpan totalDuration,
             int wordsPerMinute,
             int maxCharsPerLine,
             CancellationToken cancellationToken)
        {
            int sequenceNumber = srtBuilder.ToString().Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length + 1;
            double currentTimeSeconds = 0.0;
            double secondsPerWord = 60.0 / wordsPerMinute;

            string[] segments = SentenceSplitRegex.Split(scriptText).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (!segments.Any()) segments = new[] { scriptText };

            _logger.LogDebug($"Split script into {segments.Length} segments for estimation.");

            foreach (string segment in segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string cleanedSegment = segment.Trim();
                if (string.IsNullOrEmpty(cleanedSegment)) continue;

                int wordCount = cleanedSegment.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                double estimatedDurationSeconds = wordCount * secondsPerWord;
                estimatedDurationSeconds = Math.Max(1.0, estimatedDurationSeconds);
                estimatedDurationSeconds = Math.Min(estimatedDurationSeconds, totalDuration.TotalSeconds - currentTimeSeconds);

                if (currentTimeSeconds >= totalDuration.TotalSeconds) break;

                TimeSpan startTime = TimeSpan.FromSeconds(currentTimeSeconds);
                TimeSpan endTime = TimeSpan.FromSeconds(currentTimeSeconds + estimatedDurationSeconds);
                string formattedText = FormatTextForSrt(cleanedSegment, maxCharsPerLine);

                srtBuilder.AppendLine(sequenceNumber.ToString());
                srtBuilder.AppendLine($"{startTime:hh\\:mm\\:ss\\,fff} --> {endTime:hh\\:mm\\:ss\\,fff}");
                srtBuilder.AppendLine(formattedText);
                srtBuilder.AppendLine();

                sequenceNumber++;
                currentTimeSeconds += estimatedDurationSeconds;
            }

             if (currentTimeSeconds < totalDuration.TotalSeconds && srtBuilder.Length > 0)
             {
                 _logger.LogDebug($"Adjusting end time of last subtitle entry from {currentTimeSeconds}s to {totalDuration.TotalSeconds}s.");
                 string srtString = srtBuilder.ToString();
                 int lastTimestampIndex = srtString.LastIndexOf(" --> ");
                 if (lastTimestampIndex > 0)
                 {
                     int lineStartIndex = srtString.LastIndexOf('\n', lastTimestampIndex) + 1;
                     if (lineStartIndex > 0 && lineStartIndex < srtString.Length) // Added boundary check
                     {
                         int lineEndIndex = srtString.IndexOf('\n', lastTimestampIndex);
                         if (lineEndIndex < 0) lineEndIndex = srtString.Length; // Handle case where it's the very last line
                         string timestampLine = srtString.Substring(lineStartIndex, lineEndIndex - lineStartIndex).TrimEnd(); // Trim potential trailing CR
                         string[] times = timestampLine.Split(new[] { " --> " }, StringSplitOptions.None);
                         if (times.Length == 2)
                         {
                             string newEndTime = totalDuration.ToString(@"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);
                             string newTimestampLine = $"{times[0]} --> {newEndTime}";
                             srtBuilder.Replace(timestampLine, newTimestampLine, lineStartIndex, timestampLine.Length);
                         }
                     }
                 }
             }
             await Task.CompletedTask;
        }


        // Simple text formatter to break lines for SRT
        private string FormatTextForSrt(string text, int maxCharsPerLine)
        {
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var currentLine = new StringBuilder();

            foreach (string word in words)
            {
                if (currentLine.Length == 0) { currentLine.Append(word); }
                else if (currentLine.Length + word.Length + 1 <= maxCharsPerLine) { currentLine.Append(" ").Append(word); }
                else { lines.Add(currentLine.ToString()); currentLine.Clear().Append(word); }
            }
            if (currentLine.Length > 0) { lines.Add(currentLine.ToString()); }
            // Limit to max 2 lines for typical SRT display
            return string.Join(Environment.NewLine, lines.Take(2));
        }
    }
}