using System;
using System.IO;
using System.Linq;

namespace M3U8ConverterApp.Services;

internal interface IFfmpegLocator
{
    string? TryFind();
}

internal sealed class FfmpegLocator : IFfmpegLocator
{
    public string? TryFind()
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(localPath))
        {
            return localPath;
        }

        var environmentPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(environmentPath))
        {
            return null;
        }

        foreach (var pathSegment in environmentPath.Split(Path.PathSeparator))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pathSegment))
                {
                    continue;
                }

                var candidate = Path.Combine(pathSegment.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed segments and keep scanning.
            }
        }

        return null;
    }
}
