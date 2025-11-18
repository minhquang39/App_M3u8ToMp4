using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using M3U8ConverterApp.Models;

namespace M3U8ConverterApp.Services;

internal interface IConversionService
{
    Task ConvertAsync(ConversionRequest request, IProgress<ConversionProgress>? progress, CancellationToken cancellationToken);
}

internal sealed class ConversionService : IConversionService
{
    private static readonly Regex DurationRegex = new(@"Duration:\s(?<h>\d{2}):(?<m>\d{2}):(?<s>\d{2}(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TimeRegex = new(@"time=(?<h>\d{2}):(?<m>\d{2}):(?<s>\d{2}(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Nm3u8PercentageRegex = new(@"(?<percent>\d+(?:\.\d+)?)\s*%", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Task ConvertAsync(ConversionRequest request, IProgress<ConversionProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Engine switch
        {
            DownloadEngine.Ffmpeg => ConvertWithFfmpegAsync(request, progress, cancellationToken),
            DownloadEngine.Nm3u8DlRe => ConvertWithNm3u8DlReAsync(request, progress, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported download engine: {request.Engine}")
        };
    }

    private static async Task ConvertWithFfmpegAsync(
        ConversionRequest request,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FfmpegPath) || !File.Exists(request.FfmpegPath))
        {
            throw new FileNotFoundException("ffmpeg executable not found.", request.FfmpegPath);
        }

        if (string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            throw new ArgumentException("Source URL is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OutputFile))
        {
            throw new ArgumentException("Output path is required.", nameof(request));
        }

        var outputDirectory = Path.GetDirectoryName(request.OutputFile);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (File.Exists(request.OutputFile))
        {
            File.Delete(request.OutputFile);
        }

        var startInfo = BuildFfmpegStartInfo(request);

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start ffmpeg process.");
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
                // Ignore termination errors.
            }
        });

        var totalDuration = (TimeSpan?)null;

        async Task PumpAsync()
        {
            var reader = process.StandardError;

            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                totalDuration ??= TryParseDuration(line);
                var processed = TryParseTime(line);
                double? percentage = null;
                if (totalDuration.HasValue && processed.HasValue && totalDuration.Value.TotalSeconds > 0)
                {
                    percentage = Math.Clamp(processed.Value.TotalSeconds / totalDuration.Value.TotalSeconds * 100d, 0d, 100d);
                }

                progress?.Report(new ConversionProgress(line, totalDuration, processed, percentage));
            }
        }

        await Task.WhenAll(PumpAsync(), process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}.");
        }
    }

    private static async Task ConvertWithNm3u8DlReAsync(
        ConversionRequest request,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nm3u8DlRePath) || !File.Exists(request.Nm3u8DlRePath))
        {
            throw new FileNotFoundException("N_m3u8DL-RE executable not found.", request.Nm3u8DlRePath);
        }

        if (string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            throw new ArgumentException("Source URL is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OutputFile))
        {
            throw new ArgumentException("Output path is required.", nameof(request));
        }

        var outputDirectory = Path.GetDirectoryName(request.OutputFile);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(request));
        }

        Directory.CreateDirectory(outputDirectory);

        if (File.Exists(request.OutputFile))
        {
            File.Delete(request.OutputFile);
        }

        var workingRoot = Path.Combine(Path.GetTempPath(), "M3U8ConverterApp", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var downloadDirectory = Path.Combine(workingRoot, "download");
        var tempDirectory = Path.Combine(workingRoot, "tmp");
        Directory.CreateDirectory(downloadDirectory);
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var baseName = Path.GetFileNameWithoutExtension(request.OutputFile);
            var startInfo = BuildNm3u8StartInfo(request, downloadDirectory, tempDirectory, baseName);

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start N_m3u8DL-RE process.");
            }

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch
                {
                    // Ignore termination errors.
                }
            });

            var outputPump = PumpReaderAsync(process.StandardOutput, progress, cancellationToken);
            var errorPump = PumpReaderAsync(process.StandardError, progress, cancellationToken);

            await Task.WhenAll(outputPump, errorPump, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"N_m3u8DL-RE exited with code {process.ExitCode}.");
            }

            var producedFile = LocateProducedMedia(downloadDirectory, baseName);
            if (producedFile is null)
            {
                throw new InvalidOperationException("N_m3u8DL-RE completed but no media file was produced.");
            }

            // Prefer moving any directly produced MP4 to the requested destination.
            if (File.Exists(request.OutputFile))
            {
                File.Delete(request.OutputFile);
            }

            if (string.Equals(Path.GetExtension(producedFile), Path.GetExtension(request.OutputFile), StringComparison.OrdinalIgnoreCase))
            {
                File.Move(producedFile, request.OutputFile, overwrite: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.FfmpegPath) || !File.Exists(request.FfmpegPath))
            {
                // No ffmpeg available for remuxing; still move result to requested directory.
                var fallbackPath = Path.Combine(outputDirectory, Path.GetFileName(producedFile));
                File.Move(producedFile, fallbackPath, overwrite: true);
                throw new InvalidOperationException($"N_m3u8DL-RE produced '{Path.GetFileName(producedFile)}'. Provide an ffmpeg path to remux into {Path.GetExtension(request.OutputFile)}.");
            }

            await RemuxWithFfmpegAsync(request.FfmpegPath, producedFile, request.OutputFile, progress, cancellationToken).ConfigureAwait(false);
            TryDeleteFile(producedFile);
        }
        finally
        {
            TryDeleteDirectory(workingRoot);
        }
    }

    private static ProcessStartInfo BuildFfmpegStartInfo(ConversionRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FfmpegPath!,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8
        };

        AddFfmpegArguments(startInfo.ArgumentList, request);

        return startInfo;
    }

    private static void AddFfmpegArguments(IList<string> args, ConversionRequest request)
    {
        args.Add("-y");
        args.Add("-hide_banner");
        args.Add("-loglevel");
        args.Add("info");
        args.Add("-protocol_whitelist");
        args.Add("file,http,https,tcp,tls,crypto");

        if (request.UseAggressiveHttp)
        {
            args.Add("-multiple_requests");
            args.Add("1");
            args.Add("-http_persistent");
            args.Add("1");
        }

        args.Add("-i");
        args.Add(request.SourceUrl);
        args.Add("-c");
        args.Add("copy");
        args.Add("-bsf:a");
        args.Add("aac_adtstoasc");
        args.Add(request.OutputFile);
    }

    private static ProcessStartInfo BuildNm3u8StartInfo(
        ConversionRequest request,
        string downloadDirectory,
        string tempDirectory,
        string baseName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.Nm3u8DlRePath!,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = downloadDirectory
        };

        // Add FFmpeg directory to PATH so N_m3u8DL-RE can find it for remuxing
        if (!string.IsNullOrWhiteSpace(request.FfmpegPath) && File.Exists(request.FfmpegPath))
        {
            var ffmpegDir = Path.GetDirectoryName(request.FfmpegPath);
            if (!string.IsNullOrWhiteSpace(ffmpegDir))
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                startInfo.Environment["PATH"] = $"{ffmpegDir};{currentPath}";
            }
        }

        var args = startInfo.ArgumentList;
        args.Add(request.SourceUrl);
        args.Add("--save-dir");
        args.Add(downloadDirectory);
        args.Add("--tmp-dir");
        args.Add(tempDirectory);
        args.Add("--save-name");
        args.Add(baseName);

        if (request.Nm3u8ThreadCount.HasValue && request.Nm3u8ThreadCount.Value > 0)
        {
            args.Add("--thread-count");
            args.Add(request.Nm3u8ThreadCount.Value.ToString(CultureInfo.InvariantCulture));
        }

        args.Add("--auto-select");
        args.Add("--select-video");
        args.Add("best");
        args.Add("--select-audio");
        args.Add("best");
        args.Add("--select-subtitle");
        args.Add("none");

        return startInfo;
    }

    private static async Task PumpReaderAsync(StreamReader reader, IProgress<ConversionProgress>? progress, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var percentage = TryParseNm3u8Percentage(line);
            progress?.Report(new ConversionProgress(line, null, null, percentage));
        }
    }

    private static string? LocateProducedMedia(string directory, string baseName)
    {
        static bool IsMediaExtension(string extension) =>
            extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ts", StringComparison.OrdinalIgnoreCase);

        static int Rank(string extension) => extension.ToLowerInvariant() switch
        {
            ".mp4" => 0,
            ".mkv" => 1,
            ".ts" => 2,
            _ => 10
        };

        static bool IsPreferredFallback(string extension) =>
            extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);

        bool MatchesBaseName(string file)
        {
            var extension = Path.GetExtension(file);
            if (string.IsNullOrWhiteSpace(extension) || !IsMediaExtension(extension))
            {
                return false;
            }

            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fileBase = Path.GetFileNameWithoutExtension(file);
            return fileBase.Equals(baseName, StringComparison.OrdinalIgnoreCase);
        }

        var candidates = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(MatchesBaseName)
            .OrderBy(file => Rank(Path.GetExtension(file)))
            .ThenBy(File.GetLastWriteTimeUtc)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(file =>
                {
                    var extension = Path.GetExtension(file);
                    if (string.IsNullOrWhiteSpace(extension) || !IsPreferredFallback(extension))
                    {
                        return false;
                    }

                    var fileName = Path.GetFileName(file);
                    return !fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) &&
                           !fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(file => Rank(Path.GetExtension(file)))
                .ThenBy(File.GetLastWriteTimeUtc)
                .ToList();
        }

        return candidates.FirstOrDefault();
    }

    private static async Task RemuxWithFfmpegAsync(
        string ffmpegPath,
        string inputFile,
        string outputFile,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-y");       
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("info");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputFile);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("copy");
        startInfo.ArgumentList.Add("-bsf:a");
        startInfo.ArgumentList.Add("aac_adtstoasc");
        startInfo.ArgumentList.Add(outputFile);

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start ffmpeg for remux.");
        }

        var pumpStdOut = PumpReaderAsync(process.StandardOutput, progress, cancellationToken);
        var pumpStdErr = PumpReaderAsync(process.StandardError, progress, cancellationToken);

        await Task.WhenAll(pumpStdOut, pumpStdErr, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg remux exited with code {process.ExitCode}.");
        }
    }

    private static TimeSpan? TryParseDuration(string input)
    {
        var match = DurationRegex.Match(input);
        return match.Success ? ParseTime(match) : null;
    }

    private static TimeSpan? TryParseTime(string input)
    {
        var match = TimeRegex.Match(input);
        return match.Success ? ParseTime(match) : null;
    }

    private static TimeSpan ParseTime(Match match)
    {
        var hours = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        var seconds = double.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);

        return TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
    }

    private static double? TryParseNm3u8Percentage(string input)
    {
        var match = Nm3u8PercentageRegex.Match(input);
        if (!match.Success)
        {
            return null;
        }

        if (double.TryParse(match.Groups["percent"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return Math.Clamp(value, 0d, 100d);
        }

        return null;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Swallow cleanup errors.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }
}
