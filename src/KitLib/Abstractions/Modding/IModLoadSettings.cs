namespace KitLib.Abstractions.Modding;

/// <summary>Official mod enable/disable settings (<c>ModSettings.ModList</c>); takes effect after restart.</summary>
public interface IModLoadSettings {
    bool IsEnabled(string id, ModEntrySource source);

    void SetEnabled(string id, ModEntrySource source, bool enabled);

    /// <summary>Runtime load state disagrees with saved <c>is_enabled</c> (restart required).</summary>
    bool HasPendingRestartChanges();

    void Persist();
}
