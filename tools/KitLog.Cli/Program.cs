using System.CommandLine;
using KitLog.Cli.Services;
using Spectre.Console;

var pidOption = new Option<int?>("--pid") {
    Description = "STS2 process id (instances/{pid}/session.log). Default: latest session.",
};

var followOption = new Option<bool>("-f", "--follow") {
    Description = "Keep reading as the file grows.",
};
var tailLinesOption = new Option<int>("--tail") {
    Description = "Number of existing lines to show first.",
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
        AnsiConsole.MarkupLine($"[grey]pid={entry.Pid}[/]{tag} {Markup.Escape(entry.Path)}");
    }

    return 0;
});

var tailCmd = new Command("tail", "Tail a KitLib session log") {
    pidOption,
    followOption,
    tailLinesOption,
    fileOption,
    filterOption,
    levelOption,
    noColorOption,
};
tailCmd.SetAction(async (parseResult, ct) => {
    var pid = parseResult.GetValue(pidOption);
    var file = parseResult.GetValue(fileOption);
    var path = Sts2LogPathResolver.ResolveLogPath(pid, file);
    if (path == null) {
        AnsiConsole.MarkupLine("[red]No log file found.[/] Use [grey]kitlog path[/] to inspect resolution.");
        return 1;
    }

    var levelToken = parseResult.GetValue(levelOption)?.ToLowerInvariant();
    ParsedLogLevel? minLevel = levelToken switch {
        null or "" => null,
        "error" => ParsedLogLevel.Error,
        "warn" or "warning" => ParsedLogLevel.Warn,
        _ => null,
    };
    if (levelToken is { Length: > 0 } && minLevel == null) {
        AnsiConsole.MarkupLine($"[red]Unknown level:[/] {Markup.Escape(levelToken)} (use warn or error)");
        return 1;
    }

    return await LogTailer.RunAsync(new LogTailOptions {
        FilePath = path,
        Follow = parseResult.GetValue(followOption),
        TailLines = parseResult.GetValue(tailLinesOption),
        FilterPattern = FilterPresets.Resolve(parseResult.GetValue(filterOption)),
        MinimumLevel = minLevel,
        Color = !parseResult.GetValue(noColorOption),
    }, ct);
});

var root = new RootCommand("KitLib session log viewer for Slay the Spire 2") {
    pathCmd,
    listCmd,
    tailCmd,
};

return await root.Parse(args).InvokeAsync();
