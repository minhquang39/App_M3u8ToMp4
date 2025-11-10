namespace M3U8ConverterApp.Models;

internal sealed class AppSettings
{
    public string? FfmpegPath { get; set; }
    public string? LastOutputDirectory { get; set; }
    public bool UseAggressiveHttp { get; set; }
    public string? Nm3u8DlRePath { get; set; }
    public DownloadEngine PreferredEngine { get; set; } = DownloadEngine.Ffmpeg;
    public int Nm3u8ThreadCount { get; set; } = 16;
}
