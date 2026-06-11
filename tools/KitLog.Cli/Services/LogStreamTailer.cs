using System.Text.RegularExpressions;
using KitLib.Logging;
using KitLog.Cli.Rendering;
using Spectre.Console;

namespace KitLog.Cli.Services;

internal sealed class LogAttachOptions {
    public required int Pid { get; init; }
    public bool Follow { get; init; } = true;
    public bool Color { get; init; } = true;
    public bool SyncViewer { get; init; }
    public string? FilterPattern { get; init; }
    public ParsedLogLevel? MinimumLevel { get; init; }
    public bool AllowFallback { get; init; } = true;
    public int? FallbackTailLines { get; init; }
}

internal static class LogStreamTailer {
    public static Task<int> RunAsync(LogAttachOptions options, CancellationToken ct)
        => RunAsync(options, initialAttempt: true, ct);

    static async Task<int> RunAsync(LogAttachOptions options, bool initialAttempt, CancellationToken ct) {
        Regex? filter = null;
        if (!string.IsNullOrWhiteSpace(options.FilterPattern)) {
            try {
                filter = new Regex(options.FilterPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            }
            catch (Exception ex) {
                AnsiConsole.MarkupLine($"[red]Invalid filter regex:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }
        }

        LogViewerFilterWatcher? viewerWatcher = null;
        if (options.SyncViewer) {
            var profilePath = Sts2LogPathResolver.ResolveFilterProfilePath(options.Pid, null);
            viewerWatcher = new LogViewerFilterWatcher(profilePath);
            if (viewerWatcher.PollForChanges(out _))
                AnsiCodes.WriteStatusLine("Synced in-game log viewer filters.");
        }

        while (!ct.IsCancellationRequested) {
            await using var pipe = await LogPipeClient.TryConnectAsync(
                options.Pid,
                TimeSpan.FromSeconds(3),
                ct);

            if (pipe == null) {
                if (initialAttempt && options.AllowFallback)
                    return await FallbackToSessionLogAsync(options, ct);

                if (!options.Follow)
                    return 1;

                AnsiCodes.WriteStatusLine($"Waiting for pipe {LogStreamContract.PipeName(options.Pid)}…");
                await Task.Delay(500, ct);
                initialAttempt = false;
                continue;
            }

            if (initialAttempt) {
                WindowsAnsiBootstrap.EnableIfNeeded();
                AnsiCodes.WriteStatusLine($"Attached to {LogStreamContract.PipeName(options.Pid)} (structured stream).");
            }

            initialAttempt = false;
            var disconnected = await ReadUntilDisconnectAsync(pipe, filter, options, viewerWatcher, ct);
            if (!options.Follow || ct.IsCancellationRequested)
                return disconnected ? 0 : 0;

            AnsiCodes.WriteStatusLine("Pipe disconnected; reconnecting…");
            await Task.Delay(250, ct);
        }

        return 0;
    }

    static async Task<bool> ReadUntilDisconnectAsync(
        Stream pipe,
        Regex? filter,
        LogAttachOptions options,
        LogViewerFilterWatcher? viewerWatcher,
        CancellationToken ct) {
        try {
            while (!ct.IsCancellationRequested) {
                if (viewerWatcher?.PollForChanges(out var changed) == true && changed)
                    AnsiCodes.WriteStatusLine("── log viewer filters updated ──");

                var entry = await LogStreamFraming.ReadFrameAsync(pipe, ct);
                if (entry == null)
                    return true;

                if (!ShouldEmit(entry, filter, options, viewerWatcher))
                    continue;

                LogStreamLineRenderer.WriteLine(entry, options.Color, viewerWatcher?.Current);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            return false;
        }
        catch (IOException) {
            return true;
        }

        return true;
    }

    static async Task<int> FallbackToSessionLogAsync(LogAttachOptions options, CancellationToken ct) {
        AnsiConsole.MarkupLine(
            $"[yellow]Pipe unavailable; falling back to session.log[/] ({Markup.Escape(LogStreamContract.PipeName(options.Pid))})");

        var path = Sts2LogPathResolver.ResolveSessionLogPath(options.Pid);
        if (path == null) {
            AnsiConsole.MarkupLine("[red]No session log found for fallback.[/]");
            return 1;
        }

        return await LogTailer.RunAsync(new LogTailOptions {
            FilePath = path,
            Follow = options.Follow,
            TailLines = options.FallbackTailLines ?? 0,
            FilterPattern = options.FilterPattern,
            MinimumLevel = options.MinimumLevel,
            Color = options.Color,
            SyncViewer = options.SyncViewer,
            Pid = options.Pid,
        }, ct);
    }

    static bool ShouldEmit(
        LogStreamEntry entry,
        Regex? regexFilter,
        LogAttachOptions options,
        LogViewerFilterWatcher? viewerWatcher) {
        var plain = LogStreamLineRenderer.FormatPlain(entry);
        if (regexFilter != null && !regexFilter.IsMatch(plain))
            return false;

        if (options.MinimumLevel is ParsedLogLevel min
            && !LogLineParser.MeetsMinimumLevel(LogStreamLineAdapter.ParseLevel(entry.Lvl), min))
            return false;

        if (options.SyncViewer && viewerWatcher != null) {
            var parsed = LogStreamLineAdapter.ToParsedLine(entry);
            return LogViewerFilterMatcher.ShouldShow(
                plain,
                parsed,
                viewerWatcher.Current,
                applyViewerFilters: true);
        }

        return true;
    }
}
