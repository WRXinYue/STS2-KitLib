using System.IO.Pipes;
using KitLib.Logging;

namespace KitLog.Cli.Services;

internal static class LogPipeClient {
    public static async Task<NamedPipeClientStream?> TryConnectAsync(
        int pid,
        TimeSpan timeout,
        CancellationToken ct) {
        var pipe = new NamedPipeClientStream(
            ".",
            LogStreamContract.PipeName(pid),
            PipeDirection.In,
            PipeOptions.Asynchronous);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);

        try {
            await pipe.ConnectAsync(linked.Token);
            return pipe;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            await pipe.DisposeAsync();
            return null;
        }
        catch (TimeoutException) {
            await pipe.DisposeAsync();
            return null;
        }
        catch {
            await pipe.DisposeAsync();
            return null;
        }
    }
}
