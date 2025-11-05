using System.Text.Json.Serialization;

namespace M3U8ConverterApp.Interop;

internal sealed class NativeBridgeResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    public static NativeBridgeResponse Success(string? message = null) => new()
    {
        Ok = true,
        Message = message
    };

    public static NativeBridgeResponse Failure(string? message = null) => new()
    {
        Ok = false,
        Error = string.IsNullOrWhiteSpace(message) ? "Unable to complete native request." : message
    };
}
