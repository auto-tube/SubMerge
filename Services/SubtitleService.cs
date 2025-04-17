using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json; // For parsing speech marks
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics; // Added for Debug

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
            int maxCharsPerLine = 38, // MODIFIED: Reduced default max chars
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(scriptText)) throw new ArgumentException("Script text cannot be empty.", nameof(scriptText));
            if (totalDuration <= TimeSpan.Zero) throw new ArgumentException("Total duration must be positive.", nameof(totalDuration));
            if (string.IsNullOrWhiteSpace(outputSrtPath)) throw new ArgumentException("Output SRT path cannot be empty.", nameof(outputSrtPath));
            if (wordsPerMinute <= 0) throw new ArgumentException("Words per minute must be positive.", nameof(wordsPerMinute));
            if (maxCharsPerLine <= 0) throw new ArgumentException("Max chars per line must be positive.", nameof(maxCharsPerLine));

            _logger.LogInfo($"Generating SRT file at '{outputSrtPath}'. Speech marks provided: {speechMarks != null && speechMarks.Any()}. Max Chars/Line: {maxCharsPerLine}"); // Log max chars

            var srtBuilder = new StringBuilder();
            int sequenceNumber = 1;
            bool usedSpeechMarksSuccessfully = false;

            // --- Generate SRT Entries ---
            // --- Prioritize Word Marks ---
            if (speechMarks != null && speechMarks.Any(m => m.Type == "word"))
            {
                _logger.LogInfo("--- Attempting SRT generation using Word Marks ---");
                var wordMarks = speechMarks.Where(m => m.Type == "word").OrderBy(m => m.Time).ToList();
                if (wordMarks.Any())
                {
                    _logger.LogDebug($"Processing {wordMarks.Count} word marks.");
                    var currentLine = new StringBuilder();
                    TimeSpan lineStartTime = TimeSpan.Zero;

                    for (int i = 0; i < wordMarks.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var currentWordMark = wordMarks[i];
                        if (currentLine.Length == 0)
                        {
                            lineStartTime = TimeSpan.FromMilliseconds(currentWordMark.Time);
                        }

                        string wordToAdd = currentWordMark.Value;
                        string wordWithSpace = wordToAdd + " ";
                        bool isLastWord = (i == wordMarks.Count - 1);

                        if ((currentLine.Length > 0 && currentLine.Length + wordWithSpace.Length > maxCharsPerLine) || isLastWord)
                        {
                            if (isLastWord && (currentLine.Length + wordToAdd.Length <= maxCharsPerLine || currentLine.Length == 0))
                            {
                                if (currentLine.Length > 0) currentLine.Append(" ");
                                currentLine.Append(wordToAdd);
                            }

                            TimeSpan lineEndTime = isLastWord
                                                ? totalDuration
                                                : TimeSpan.FromMilliseconds(currentWordMark.Time - 50);

                            if (lineEndTime <= lineStartTime) lineEndTime = lineStartTime + TimeSpan.FromMilliseconds(500);
                            if (lineEndTime > totalDuration) lineEndTime = totalDuration;

                            string formattedText = FormatTextForSrt(currentLine.ToString(), maxCharsPerLine); // Format the built line

                            srtBuilder.AppendLine(sequenceNumber.ToString());
                            srtBuilder.AppendLine($"{lineStartTime:hh\\:mm\\:ss\\,fff} --> {lineEndTime:hh\\:mm\\:ss\\,fff}");
                            srtBuilder.AppendLine(formattedText);
                            srtBuilder.AppendLine();
                            sequenceNumber++;

                            if (!isLastWord)
                            {
                                currentLine.Clear().Append(wordToAdd);
                                lineStartTime = TimeSpan.FromMilliseconds(currentWordMark.Time);
                            }
                            else
                            {
                                currentLine.Clear();
                            }
                        }
                        else
                        {
                             if (currentLine.Length > 0) currentLine.Append(" ");
                             currentLine.Append(wordToAdd);
                        }
                    }
                    usedSpeechMarksSuccessfully = true;
                    _logger.LogInfo($"--- Successfully generated {sequenceNumber - 1} SRT entries using Word Marks ---");
                }
                else
                {
                     _logger.LogWarning("Speech marks provided, but no word marks found within them.");
                }
            }
            // --- Fallback to Sentence Marks ---
            else if (speechMarks != null && speechMarks.Any(m => m.Type == "sentence"))
            {
                 _logger.LogInfo("--- No Word Marks found/used. Attempting SRT generation using Sentence Marks ---");
                 var sentenceMarks = speechMarks.Where(m => m.Type == "sentence").OrderBy(m => m.Time).ToList();

                 if (sentenceMarks.Any())
                 {
                     _logger.LogDebug($"Processing {sentenceMarks.Count} sentence marks.");
                     for (int i = 0; i < sentenceMarks.Count; i++)
                     {
                         cancellationToken.ThrowIfCancellationRequested();
                         var currentMark = sentenceMarks[i];
                         var nextMarkTimeMs = (i + 1 < sentenceMarks.Count)
                             ? sentenceMarks[i + 1].Time
                             : (int)totalDuration.TotalMilliseconds;

                         TimeSpan startTime = TimeSpan.FromMilliseconds(currentMark.Time);
                         TimeSpan endTime = TimeSpan.FromMilliseconds(Math.Max(currentMark.Time + 50, nextMarkTimeMs - 50));

                         if (endTime > totalDuration) endTime = totalDuration;
                         if (endTime <= startTime) endTime = startTime + TimeSpan.FromMilliseconds(500);

                         string formattedText = FormatTextForSrt(currentMark.Value, maxCharsPerLine);

                         srtBuilder.AppendLine(sequenceNumber.ToString());
                         srtBuilder.AppendLine($"{startTime:hh\\:mm\\:ss\\,fff} --> {endTime:hh\\:mm\\:ss\\,fff}");
                         srtBuilder.AppendLine(formattedText);
                         srtBuilder.AppendLine();
                         sequenceNumber++;
                     }
                     usedSpeechMarksSuccessfully = true;
                     _logger.LogInfo($"--- Successfully generated {sequenceNumber - 1} SRT entries using Sentence Marks ---");
                 }
                 else
                 {
                      _logger.LogWarning("Speech marks provided, but no sentence marks found either.");
                 }
            }

            // --- Fallback to Estimated Timing ---
            if (!usedSpeechMarksSuccessfully)
            {
                _logger.LogInfo("--- Falling back to SRT generation using Estimated Timing (WPM) ---");
                await GenerateSrtByEstimationAsync(srtBuilder, scriptText, totalDuration, wordsPerMinute, maxCharsPerLine, cancellationToken);
                 _logger.LogInfo($"--- Finished generating {srtBuilder.ToString().Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length} SRT entries using Estimation ---");
            }


            // --- Write File ---
            try
            {
                string? outputDir = Path.GetDirectoryName(outputSrtPath);
                if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);
                await File.WriteAllTextAsync(outputSrtPath, srtBuilder.ToString(), Encoding.UTF8, cancellationToken);
                _logger.LogInfo($"Successfully wrote SRT file: {outputSrtPath}");
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
                double remainingTime = totalDuration.TotalSeconds - currentTimeSeconds;
                estimatedDurationSeconds = Math.Min(estimatedDurationSeconds, Math.Max(0.1, remainingTime - 0.1));

                if (currentTimeSeconds >= totalDuration.TotalSeconds || estimatedDurationSeconds <= 0) break;

                TimeSpan startTime = TimeSpan.FromSeconds(currentTimeSeconds);
                TimeSpan endTime = TimeSpan.FromSeconds(currentTimeSeconds + estimatedDurationSeconds);
                 if (endTime > totalDuration) endTime = totalDuration;

                string formattedText = FormatTextForSrt(cleanedSegment, maxCharsPerLine);

                srtBuilder.AppendLine(sequenceNumber.ToString());
                srtBuilder.AppendLine($"{startTime:hh\\:mm\\:ss\\,fff} --> {endTime:hh\\:mm\\:ss\\,fff}");
                srtBuilder.AppendLine(formattedText);
                srtBuilder.AppendLine();

                sequenceNumber++;
                currentTimeSeconds += estimatedDurationSeconds;
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
            // --- MODIFIED: Limit to max 3 lines ---
            return string.Join(Environment.NewLine, lines.Take(3));
            // --- END MODIFIED ---
        }
    }
}
