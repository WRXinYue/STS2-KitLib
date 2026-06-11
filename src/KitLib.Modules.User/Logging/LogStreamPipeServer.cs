using System.IO.Pipes;
using System.Threading.Channels;
using System.Threading.Tasks;
using KitLib.Logging;

namespace KitLib;

/// <summary>
/// Streams structured <see cref="LogStreamEntry"/> frames to <c>kitlog attach</c> over a per-process named pipe.
/// </summary>
internal static class LogStreamPipeServer {
    static readonly object Gate = new();
    static CancellationTokenSource? _cts;
    static Task? _acceptLoop;

    public static void Start() {
        lock (Gate) {
            if (_acceptLoop != null)
                return;

            _cts = new CancellationTokenSource();
            _acceptLoop = Task.Run(() => AcceptLoop(_cts.Token));
        }
    }

    public static void Stop() {
        CancellationTokenSource? cts;
        lock (Gate) {
            cts = _cts;
            _acceptLoop = null;
            _cts = null;
        }

        cts?.Cancel();
        cts?.Dispose();
    }

    static async Task AcceptLoop(CancellationToken ct) {
        var pipeName = LogStreamContract.PipeName(KitLibInstance.ProcessId);

        while (!ct.IsCancellationRequested) {
            await using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.Out,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try {
                await server.WaitForConnectionAsync(ct);
            }
            catch (OperationCanceledException) {
                break;
            }
            catch (Exception ex) {
                KitLog.Warn("LogStream", $"Pipe accept failed: {ex.Message}");
                try {
                    await Task.Delay(250, ct);
                }
                catch (OperationCanceledException) {
                    break;
                }

                continue;
            }

            try {
                await StreamClientAsync(server, ct);
            }
            catch (OperationCanceledException) {
                break;
            }
            catch (Exception ex) {
                KitLog.Debug("LogStream", $"Pipe client disconnected: {ex.Message}");
            }
        }
    }

    static async Task StreamClientAsync(NamedPipeServerStream server, CancellationToken ct) {
        var outgoing = Channel.CreateUnbounded<LogStreamEntry>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = false,
        });

        var writerTask = PumpFramesAsync(server, outgoing.Reader, ct);

        void Handler(LogStreamEntry entry) {
            outgoing.Writer.TryWrite(entry);
        }

        LogStreamHub.SubscribeWithReplay(Handler);
        try {
            while (server.IsConnected && !ct.IsCancellationRequested)
                await Task.Delay(100, ct);
        }
        catch (OperationCanceledException) {
            // Expected on shutdown.
        }
        finally {
            LogStreamHub.Unsubscribe(Handler);
            outgoing.Writer.TryComplete();
            try {
                await writerTask;
            }
            catch (OperationCanceledException) {
                // Expected on shutdown.
            }
        }
    }

    static async Task PumpFramesAsync(
        Stream stream,
        ChannelReader<LogStreamEntry> reader,
        CancellationToken ct) {
        await foreach (var entry in reader.ReadAllAsync(ct))
            await LogStreamFraming.WriteFrameAsync(stream, entry, ct);
    }
}
