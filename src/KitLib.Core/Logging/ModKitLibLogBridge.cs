using System.Diagnostics;
using System.Reflection;
using KitLib.Logging;
using KitLib.Modding;
using KitLogLevel = KitLib.Logging.KitLogLevel;

namespace KitLib;

internal static class ModKitLibLogBridge {
    static readonly HashSet<string> IgnoredAssemblyNames = new(StringComparer.OrdinalIgnoreCase) {
        "KitLib",
        "KitLib.Abstractions",
        "mscorlib",
        "netstandard",
        "System",
        "System.Private.CoreLib",
        "System.Runtime",
    };

    internal static void Initialize() => KitLibLog.Bind(Write);

    static void Write(KitLogLevel level, string? scope, string message) {
        var caller = ResolveCallerAssembly();
        var asmName = caller?.GetName().Name;

        string modId;
        string? autoScope = null;

        if (!string.IsNullOrEmpty(asmName)
            && ModAssemblyLookup.TryGetByAssemblySimpleName(asmName, out var mod)) {
            modId = mod.Id;
            autoScope = ModAssemblyLookup.TryDeriveScope(asmName, mod);
        }
        else
            modId = string.IsNullOrEmpty(asmName) ? "Unknown" : asmName!;

        var effectiveScope = NormalizeScope(scope) ?? autoScope;
        KitLog.WriteMod(level, modId, effectiveScope, message);
    }

    static string? NormalizeScope(string? scope) {
        if (string.IsNullOrWhiteSpace(scope))
            return null;
        return scope.Trim();
    }

    static Assembly? ResolveCallerAssembly() {
        var trace = new StackTrace(false);
        for (var i = 0; i < trace.FrameCount; i++) {
            var method = trace.GetFrame(i)?.GetMethod();
            var declaring = method?.DeclaringType;
            if (declaring == null)
                continue;

            if (declaring == typeof(KitLibLog) || declaring == typeof(KitLibLogScope)
                || declaring == typeof(ModLog) || declaring == typeof(ModLogScope))
                continue;

            var asm = declaring.Assembly;
            var name = asm.GetName().Name;
            if (string.IsNullOrEmpty(name) || IgnoredAssemblyNames.Contains(name))
                continue;

            if (name.StartsWith("System.", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.", StringComparison.Ordinal)
                || name.StartsWith("Godot", StringComparison.Ordinal))
                continue;

            return asm;
        }

        return null;
    }
}
