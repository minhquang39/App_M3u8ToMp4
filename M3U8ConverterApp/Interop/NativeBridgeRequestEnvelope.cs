using System.Text.Json.Serialization;

namespace M3U8ConverterApp.Interop;

internal sealed class NativeBridgeRequestEnvelope
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

    [JsonPropertyName("message")]
    public NativeBridgeRequest? Message { get; init; }

    public NativeBridgeRequest Normalize()
    {
        if (Message is null)
        {
            return new NativeBridgeRequest
            {
                Type = Type,
                Url = Url,
                TabTitle = TabTitle,
                PageUrl = PageUrl,
                DetectedAt = DetectedAt,
                PreviewImage = PreviewImage,
                Source = Source
            };
        }

        return new NativeBridgeRequest
        {
            Type = Message.Type ?? Type,
            Url = Message.Url ?? Url,
            TabTitle = Message.TabTitle ?? TabTitle,
            PageUrl = Message.PageUrl ?? PageUrl,
            DetectedAt = Message.DetectedAt ?? DetectedAt,
            PreviewImage = Message.PreviewImage ?? PreviewImage,
            Source = Message.Source ?? Source
        };
    }
}
