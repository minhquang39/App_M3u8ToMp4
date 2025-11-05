using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace M3U8ConverterApp.Interop;

internal sealed class NativeBridgeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly Func<NativeBridgeRequest, Task<NativeBridgeResponse>> _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public NativeBridgeServer(string pipeName, Func<NativeBridgeRequest, Task<NativeBridgeResponse>> handler)
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName) ? "m3u8_converter_bridge" : pipeName;
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _worker = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };

            while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    break;
                }

                if (line is null)
                {
                    break;
                }

                NativeBridgeResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<NativeBridgeRequest>(line, _serializerOptions)
                        ?? throw new InvalidDataException("Failed to parse native bridge payload.");

                    response = await _handler(request).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    response = NativeBridgeResponse.Failure(ex.Message);
                }

                try
                {
                    var responseJson = JsonSerializer.Serialize(response, _serializerOptions);
                    await writer.WriteLineAsync(responseJson).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _worker.Wait(1500);
        }
        catch
        {
            // Ignore shutdown issues.
        }
        _cts.Dispose();
    }
}
