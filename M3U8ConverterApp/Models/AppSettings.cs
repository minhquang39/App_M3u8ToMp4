namespace M3U8ConverterApp.Models;

internal sealed class AppSettings
{
    public string? FfmpegPath { get; set; }
    public string? LastOutputDirectory { get; set; }
    public string? LastUrl { get; set; }
    public bool UseAggressiveHttp { get; set; }
    public string? Nm3u8DlRePath { get; set; }
    public DownloadEngine PreferredEngine { get; set; } = DownloadEngine.Ffmpeg;
    public int Nm3u8ThreadCount { get; set; } = 16;
    public bool Nm3u8AutoSelect { get; set; } = true;
    public string? Nm3u8VideoSelection { get; set; } = "best";
    public string? Nm3u8AudioSelection { get; set; } = "best";
    public string? Nm3u8SubtitleSelection { get; set; } = "all";
    public bool Nm3u8IncludeSubtitles { get; set; }
}
