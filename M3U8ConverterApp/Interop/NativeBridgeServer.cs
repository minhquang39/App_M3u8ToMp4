using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace M3U8ConverterApp.Interop;

internal sealed class NativeBridgeServer : IDisposable
{
    public const string DefaultPipeName = "m3u8_converter_bridge";

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
        _pipeName = string.IsNullOrWhiteSpace(pipeName) ? DefaultPipeName : pipeName;
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
                Log($"Waiting for connection on pipe '{_pipeName}'.");
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                Log("Client connected.");
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
                    Log($"Received payload: {line}");

                    var envelope = JsonSerializer.Deserialize<NativeBridgeRequestEnvelope>(line, _serializerOptions)
                        ?? throw new InvalidDataException("Failed to parse native bridge payload.");

                    var request = envelope.Normalize();

                    response = await _handler(request).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log($"Handler error: {ex}");
                    response = NativeBridgeResponse.Failure(ex.Message);
                }

                try
                {
                    var responseJson = JsonSerializer.Serialize(response, _serializerOptions);
                    Log($"Sending response: {responseJson}");
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

    private static void Log(string message)
    {
        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "M3U8ConverterApp", "bridge.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore logging failures
        }
    }
}
