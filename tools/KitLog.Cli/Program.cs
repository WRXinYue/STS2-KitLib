using System.CommandLine;
using KitLib.Logging;
using KitLog.Cli.Services;
using Spectre.Console;

WindowsAnsiBootstrap.EnableIfNeeded();

var pidOption = new Option<int?>("--pid") {
    Description = "STS2 process id (instances/{pid}/session.log). Default: latest session.",
};

var followOption = new Option<bool>("-f", "--follow") {
    Description = "Keep reading as the log stream grows.",
};
var tailLinesOption = new Option<int>("--tail") {
    Description = "Existing lines to show first (0 = full session with --sync-viewer).",
    DefaultValueFactory = _ => 40,
};
var fileOption = new Option<string?>("--file") {
    Description = "Explicit log file path.",
};
var filterOption = new Option<string?>("--filter") {
    Description = "Regex or preset (ai).",
};
var levelOption = new Option<string?>("--level") {
    Description = "Minimum level: warn, error.",
};
var noColorOption = new Option<bool>("--no-color") {
    Description = "Disable colored output.",
};
var syncViewerOption = new Option<bool>("--sync-viewer") {
    Description = "Mirror in-game log viewer filters (level, text, mod source, suppress rules).",
};
var noFallbackOption = new Option<bool>("--no-fallback") {
    Description = "Do not fall back to session.log when the pipe is unavailable.",
};

var pathCmd = new Command("path", "Print resolved session.log path") {
    pidOption,
};
pathCmd.SetAction(async (parseResult, ct) => {
    var pid = parseResult.GetValue(pidOption);
    var path = Sts2LogPathResolver.ResolveSessionLogPath(pid)
               ?? Sts2LogPathResolver.ResolveGodotLogPath();
    if (path == null) {
        AnsiConsole.MarkupLine("[red]No session log found.[/]");
        return 1;
    }

    Console.WriteLine(path);
    if (TryResolvePid(pid, out var resolvedPid))
        Console.WriteLine($"pipe: {LogStreamContract.PipeName(resolvedPid)}");
    return 0;
});

var listCmd = new Command("list", "List KitLib instance session logs");
listCmd.SetAction(async (_, ct) => {
    var entries = Sts2LogPathResolver.ListSessionLogs();
    if (entries.Count == 0) {
        AnsiConsole.MarkupLine("[yellow]No KitLib instance logs found.[/]");
        var godot = Sts2LogPathResolver.ResolveGodotLogPath();
        if (godot != null)
            AnsiConsole.MarkupLine($"[grey]Fallback:[/] {Markup.Escape(godot)}");
        return 1;
    }

    foreach (var entry in entries) {
        var tag = entry.IsLatest ? " [green](latest)[/]" : "";
        var pipe = entry.Pid is int p ? LogStreamContract.PipeName(p) : "";
        AnsiConsole.MarkupLine($"[grey]pid={entry.Pid}[/]{tag} {Markup.Escape(entry.Path)}");
        if (!string.IsNullOrEmpty(pipe))
            AnsiConsole.MarkupLine($"  [grey]pipe:[/] {Markup.Escape(pipe)}");
    }

    return 0;
});

var attachCmd = new Command("attach", "Attach to the structured KitLib log pipe (falls back to session.log)") {
    pidOption,
    followOption,
    tailLinesOption,
    filterOption,
    levelOption,
    noColorOption,
    syncViewerOption,
    noFallbackOption,
};
attachCmd.SetAction(async (parseResult, ct) => {
    if (!TryResolvePid(parseResult.GetValue(pidOption), out var pid)) {
        AnsiConsole.MarkupLine("[red]No KitLib session found.[/] Start the game or pass --pid.");
        return 1;
    }

    var minLevel = ParseMinimumLevel(parseResult.GetValue(levelOption), out var levelError);
    if (levelError != null) {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(levelError)}[/]");
        return 1;
    }

    return await LogStreamTailer.RunAsync(new LogAttachOptions {
        Pid = pid,
        Follow = parseResult.GetValue(followOption),
        FallbackTailLines = parseResult.GetValue(tailLinesOption),
        FilterPattern = FilterPresets.Resolve(parseResult.GetValue(filterOption)),
        MinimumLevel = minLevel,
        Color = !parseResult.GetValue(noColorOption),
        SyncViewer = parseResult.GetValue(syncViewerOption),
        AllowFallback = !parseResult.GetValue(noFallbackOption),
    }, ct);
});

var tailCmd = new Command("tail", "[legacy] Tail a KitLib session log file") {
    pidOption,
    followOption,
    tailLinesOption,
    fileOption,
    filterOption,
    levelOption,
    noColorOption,
    syncViewerOption,
};
tailCmd.SetAction(async (parseResult, ct) => {
    var pid = parseResult.GetValue(pidOption);
    var file = parseResult.GetValue(fileOption);
    var path = Sts2LogPathResolver.ResolveLogPath(pid, file);
    if (path == null) {
        AnsiConsole.MarkupLine("[red]No log file found.[/] Use [grey]kitlog path[/] to inspect resolution.");
        return 1;
    }

    var minLevel = ParseMinimumLevel(parseResult.GetValue(levelOption), out var levelError);
    if (levelError != null) {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(levelError)}[/]");
        return 1;
    }

    return await LogTailer.RunAsync(new LogTailOptions {
        FilePath = path,
        Follow = parseResult.GetValue(followOption),
        TailLines = parseResult.GetValue(tailLinesOption),
        FilterPattern = FilterPresets.Resolve(parseResult.GetValue(filterOption)),
        MinimumLevel = minLevel,
        Color = !parseResult.GetValue(noColorOption),
        SyncViewer = parseResult.GetValue(syncViewerOption),
        Pid = pid,
    }, ct);
});

var root = new RootCommand("KitLib session log viewer for Slay the Spire 2") {
    pathCmd,
    listCmd,
    attachCmd,
    tailCmd,
};

return await root.Parse(args).InvokeAsync();

static bool TryResolvePid(int? pid, out int resolvedPid) {
    if (pid is int explicitPid) {
        resolvedPid = explicitPid;
        return true;
    }

    var latest = Sts2LogPathResolver.ListSessionLogs().FirstOrDefault(e => e.IsLatest);
    if (latest?.Pid is int latestPid) {
        resolvedPid = latestPid;
        return true;
    }

    resolvedPid = 0;
    return false;
}

static ParsedLogLevel? ParseMinimumLevel(string? levelToken, out string? error) {
    error = null;
    var token = levelToken?.ToLowerInvariant();
    if (string.IsNullOrEmpty(token))
        return null;

    if (token is "error")
        return ParsedLogLevel.Error;
    if (token is "warn" or "warning")
        return ParsedLogLevel.Warn;

    error = $"Unknown level: {levelToken} (use warn or error)";
    return null;
}
