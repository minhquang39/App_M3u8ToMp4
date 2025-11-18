using System;
using System.IO;

namespace M3U8ConverterApp.Services;

internal interface INm3u8DlReLocator
{
    string? TryFind();
}

internal sealed class Nm3u8DlReLocator : INm3u8DlReLocator
{
    public string? TryFind()
    {
        // 1. Tìm trong folder bundled (AppContext.BaseDirectory/N_m3u8DL-RE/)
        var localPath = Path.Combine(AppContext.BaseDirectory, "N_m3u8DL-RE", "N_m3u8DL-RE.exe");
        if (File.Exists(localPath))
        {
            return localPath;
        }

        // 2. Tìm trong PATH environment variable
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

                var candidate = Path.Combine(pathSegment.Trim(), "N_m3u8DL-RE.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return null;
    }
}