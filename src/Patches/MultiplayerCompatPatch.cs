using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Timeline;

namespace KitLib.Patches;

/// <summary>
/// Multiplayer compatibility patches.
/// Filters KitLib modules from mod signature lists and normalizes ModelDb hash
/// so the mod doesn't break multiplayer handshakes.
/// </summary>
internal static class MultiplayerCompatRules {
    public const string MpCheatCapabilitySignature = "KitLib:MpCheat";

    private static readonly string[] IgnoredPrefixes = ["KitLib"];
    private static bool? _hasCustomModelTypes;
    private static bool _loggedModelTypeCheck;
    private static bool _loggedHashNormalization;
    private static bool _loggedModFilter;

    public static List<string>? FilterIgnoredModSignatures(List<string>? signatures, out int removedCount) {
        removedCount = 0;
        if (signatures == null || signatures.Count == 0) return signatures;

        var filtered = signatures.Where(s => !ShouldIgnore(s)).ToList();
        removedCount = signatures.Count - filtered.Count;
        return filtered.Count > 0 ? filtered : null;
    }

    public static bool ShouldIgnore(string? sig) {
        if (string.IsNullOrWhiteSpace(sig)) return false;
        // MpCheat opt-in must not enter vanilla mod diff (causes "host has KitLib:MpCheat" kick).
        if (sig.Equals(MpCheatCapabilitySignature, StringComparison.OrdinalIgnoreCase))
            return true;
        return IgnoredPrefixes.Any(p => sig.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    public static void NormalizeInitialGameInfoMessage(ref InitialGameInfoMessage message) {
        message.mods = FilterIgnoredModSignatures(message.mods, out var removed);
        if (removed > 0 && !_loggedModFilter) {
            _loggedModFilter = true;
            MainFile.Logger.Info($"Multiplayer: filtered {removed} mod signature(s) from handshake.");
        }

        if (CanNormalizeModelIdHash() && message.idDatabaseHash != ModelIdSerializationCache.Hash) {
            message.idDatabaseHash = ModelIdSerializationCache.Hash;
            if (!_loggedHashNormalization) {
                _loggedHashNormalization = true;
                MainFile.Logger.Warn("Normalized multiplayer ModelDb hash for KitLib compatibility.");
            }
        }
    }

    private static bool CanNormalizeModelIdHash() {
        bool hasCustom = HasCustomModelTypes();
        if (!_loggedModelTypeCheck) {
            _loggedModelTypeCheck = true;
            if (hasCustom)
                MainFile.Logger.Warn("KitLib assembly contains custom model types. ModelDb hash bypass disabled.");
            else
                MainFile.Logger.Info("KitLib assembly has no custom model types. ModelDb hash bypass enabled.");
        }
        return !hasCustom;
    }

    private static bool HasCustomModelTypes() {
        if (_hasCustomModelTypes.HasValue) return _hasCustomModelTypes.Value;
        try {
            var types = typeof(MainFile).Assembly.GetTypes();
            _hasCustomModelTypes = types.Any(IsCustomModelType);
        }
        catch (ReflectionTypeLoadException ex) {
            _hasCustomModelTypes = ex.Types.Where(t => t != null).Any(IsCustomModelType);
        }
        return _hasCustomModelTypes ?? false;
    }

    private static bool IsCustomModelType(Type? type) {
        if (type == null || type.Assembly != typeof(MainFile).Assembly || type.IsAbstract || type.IsInterface)
            return false;
        return typeof(AbstractModel).IsAssignableFrom(type) || typeof(EpochModel).IsAssignableFrom(type);
    }
}

/// <summary>Filter KitLib modules from multiplayer mod signature list.</summary>
[HarmonyPatch(typeof(ModManager), nameof(ModManager.GetGameplayRelevantModNameList))]
public static class MultiplayerModSyncPatch {
    public static void Postfix(ref List<string> __result) {
        if (__result == null) return;
        __result = MultiplayerCompatRules.FilterIgnoredModSignatures(__result, out _) ?? new List<string>();
    }
}

/// <summary>Normalize incoming multiplayer handshake via reflection (JoinFlow may not exist in all versions).</summary>
[HarmonyPatch]
public static class JoinFlowCompatPatch {
    [HarmonyTargetMethod]
    private static System.Reflection.MethodBase? TargetMethod() {
        // Try to find JoinFlow.HandleInitialGameInfoMessage via reflection
        var joinFlowType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == "JoinFlow");

        if (joinFlowType == null) return null;

        return AccessTools.Method(joinFlowType, "HandleInitialGameInfoMessage");
    }

    public static void Prefix(ref InitialGameInfoMessage message) {
        MultiplayerCompatRules.NormalizeInitialGameInfoMessage(ref message);
    }
}
