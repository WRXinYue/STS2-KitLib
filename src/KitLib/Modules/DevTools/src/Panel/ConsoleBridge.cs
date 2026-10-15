using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitLib.Modding;
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

    private static readonly Assembly KitLibAssembly = typeof(MainFile).Assembly;

    public enum CommandSourceKind {
        Official = 0,
        KitLib = 1,
        Mod = 2,
    }

    public record CommandInfo(
        string Name,
        string Args,
        string Description,
        CommandSourceKind SourceKind,
        string SourceLabel);

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
            if (entry.Value is not AbstractConsoleCmd cmd) continue;

            string name = entry.Key?.ToString() ?? "?";
            string cmdArgs = "";
            string desc = "";
            try { cmdArgs = cmd.Args ?? ""; } catch { }
            try { desc = cmd.Description ?? ""; } catch { }

            var (kind, label) = ClassifySource(cmd.GetType().Assembly);
            list.Add(new CommandInfo(name, cmdArgs, desc, kind, label));
        }

        commands = list.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return true;
    }

    public static string LocalizeDescription(CommandInfo cmd) {
        string fallback = StripLegacyKitLibPrefix(cmd.Description);
        string key = $"console.cmd.{cmd.Name.ToLowerInvariant()}.desc";
        return I18N.T(key, fallback);
    }

    public static string SourceBadge(CommandInfo cmd) => cmd.SourceKind switch {
        CommandSourceKind.Official => I18N.T("console.badge.official", "Official"),
        CommandSourceKind.KitLib => I18N.T("console.badge.kitlib", "KitLib"),
        CommandSourceKind.Mod => cmd.SourceLabel,
        _ => "?",
    };

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

    private static (CommandSourceKind Kind, string Label) ClassifySource(Assembly? assembly) {
        if (assembly == null)
            return (CommandSourceKind.Mod, I18N.T("console.source.unknownMod", "Mod"));

        if (IsKitLibAssembly(assembly))
            return (CommandSourceKind.KitLib, I18N.T("console.badge.kitlib", "KitLib"));

        string? simpleName = assembly.GetName().Name;
        if (IsOfficialAssembly(simpleName))
            return (CommandSourceKind.Official, I18N.T("console.badge.official", "Official"));

        if (!string.IsNullOrEmpty(simpleName)
            && ModAssemblyLookup.TryGetByAssemblySimpleName(simpleName, out var modInfo)
            && !string.IsNullOrWhiteSpace(modInfo.DisplayName))
            return (CommandSourceKind.Mod, modInfo.DisplayName);

        return (CommandSourceKind.Mod, simpleName ?? I18N.T("console.source.unknownMod", "Mod"));
    }

    private static bool IsKitLibAssembly(Assembly assembly) {
        if (ReferenceEquals(assembly, KitLibAssembly))
            return true;

        string? simpleName = assembly.GetName().Name;
        return !string.IsNullOrEmpty(simpleName)
            && simpleName.StartsWith("KitLib", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOfficialAssembly(string? simpleName) {
        if (string.IsNullOrEmpty(simpleName))
            return false;

        if (ModAssemblyLookup.IsGameCoreAssembly(simpleName))
            return true;

        return simpleName.StartsWith("MegaCrit.Sts2", StringComparison.Ordinal);
    }

    private static string StripLegacyKitLibPrefix(string description) {
        const string prefix = "[KitLib] ";
        return description.StartsWith(prefix, StringComparison.Ordinal)
            ? description[prefix.Length..]
            : description;
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
