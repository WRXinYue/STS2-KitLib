using System;
using System.Reflection;
using System.Text.Json;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Optional local file persistence for reconnect (mod_data/KitLib).</summary>
internal static class MpCheatRunSavedData {
    private const string StoreTypeName = "STS2RitsuLib.RunData.RunSavedDataStore";
    private const string SlotKey = "mp_cheat_config";

    public static void TryWrite(MpCheatConfig config) {
        try {
            var storeType = ResolveType(StoreTypeName);
            if (storeType == null) return;

            var forMethod = storeType.GetMethod("For", BindingFlags.Public | BindingFlags.Static);
            if (forMethod == null) return;

            var store = forMethod.Invoke(null, [MainFile.ModID]);
            if (store == null) return;

            var register = store.GetType().GetMethod("Register", BindingFlags.Public | BindingFlags.Instance);
            if (register == null) return;

            var generic = register.MakeGenericMethod(typeof(MpCheatConfig));
            generic.Invoke(store, [SlotKey, null, null]);

            var modifyMethod = store.GetType().Assembly
                .GetType("STS2RitsuLib.RunData.RunSavedData`1")?
                .MakeGenericType(typeof(MpCheatConfig))
                ?.GetMethod("Modify", BindingFlags.Public | BindingFlags.Instance);

            // Best-effort: slot may already be registered statically elsewhere in future.
            var json = JsonSerializer.Serialize(config);
            MainFile.Logger.Debug($"[MpCheat] RunSavedData write skipped (register-only bootstrap); json len={json.Length}");
        }
        catch (Exception ex) {
            MainFile.Logger.Debug($"[MpCheat] RunSavedData unavailable: {ex.Message}");
        }
    }

    private static Type? ResolveType(string name) {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            var t = asm.GetType(name, throwOnError: false);
            if (t != null) return t;
        }
        return null;
    }
}
