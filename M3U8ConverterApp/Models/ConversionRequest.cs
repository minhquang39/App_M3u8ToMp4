namespace M3U8ConverterApp.Models;

internal sealed record ConversionRequest(
    DownloadEngine Engine,
    string? FfmpegPath,
    string SourceUrl,
    string OutputFile,
    bool UseAggressiveHttp,
    string? Nm3u8DlRePath,
    int? Nm3u8ThreadCount);
