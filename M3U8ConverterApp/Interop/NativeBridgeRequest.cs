using System;
using System.Text.Json.Serialization;

namespace M3U8ConverterApp.Interop;

internal sealed class NativeBridgeRequest
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("tabTitle")]
    public string? TabTitle { get; init; }

    [JsonPropertyName("pageUrl")]
    public string? PageUrl { get; init; }

    [JsonPropertyName("detectedAt")]
    public long? DetectedAt { get; init; }

    [JsonPropertyName("previewImage")]
    public string? PreviewImage { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    public bool IsEnqueueRequest() =>
        string.IsNullOrWhiteSpace(Type) ||
        Type.Equals("enqueue-link", StringComparison.OrdinalIgnoreCase);
}
