using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Nodes.Debug;

namespace KitLib;

/// <summary>
/// Bridge to the official STS2 DevConsole.
/// Enumerates registered commands and executes raw command strings.
/// </summary>
internal sealed class ConsoleBridge {
    private static readonly FieldInfo? NDevConsoleField =
        typeof(NDevConsole).GetField("_devConsole", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? CommandsField =
        typeof(DevConsole).GetField("_commands", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public record CommandInfo(string Name, string Args, string Description, bool IsOfficial);

    public bool TryGetCommands(out IReadOnlyList<CommandInfo> commands, out string error) {
        commands = Array.Empty<CommandInfo>();
        error = string.Empty;

        if (!TryGetDevConsole(out var devConsole, out error)) return false;

        if (CommandsField?.GetValue(devConsole) is not IDictionary dict) {
            error = I18N.T("console.error.noCommands", "Cannot read command table.");
            return false;
        }

        var list = new List<CommandInfo>();
        foreach (DictionaryEntry entry in dict) {
            if (entry.Value is AbstractConsoleCmd cmd) {
                bool isOfficial = cmd.GetType().Assembly != typeof(MainFile).Assembly;
                string name = entry.Key?.ToString() ?? "?";
                string cmdArgs = "";
                string desc = "";
                try { cmdArgs = cmd.Args ?? ""; } catch { }
                try { desc = cmd.Description ?? ""; } catch { }
                list.Add(new CommandInfo(name, cmdArgs, desc, isOfficial));
            }
        }

        commands = list.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return true;
    }

    public bool TryExecute(string rawCommand, out string message, out bool success) {
        message = string.Empty;
        success = false;

        if (string.IsNullOrWhiteSpace(rawCommand)) {
            message = I18N.T("console.error.empty", "Command cannot be empty.");
            return false;
        }

        if (!TryGetDevConsole(out var devConsole, out var error) || devConsole == null) {
            message = error;
            return false;
        }

        var result = devConsole.ProcessCommand(rawCommand.Trim());
        message = string.IsNullOrWhiteSpace(result.msg)
            ? I18N.T("console.submitted", "Command submitted.")
            : result.msg;
        success = result.success;
        return true;
    }

    private static bool TryGetDevConsole(out DevConsole? devConsole, out string error) {
        devConsole = null;
        error = string.Empty;
        try {
            var instance = NDevConsole.Instance;
            devConsole = NDevConsoleField?.GetValue(instance) as DevConsole;
            if (devConsole == null) {
                error = I18N.T("console.error.notReady", "Console backend not initialized.");
                return false;
            }
            return true;
        }
        catch (Exception ex) {
            error = I18N.T("console.error.failed", "Failed to get console: {0}", ex.Message);
            return false;
        }
    }
}
