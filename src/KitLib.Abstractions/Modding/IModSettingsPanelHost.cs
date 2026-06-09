namespace KitLib.Abstractions.Modding;

/// <summary>Runtime availability of the KitLib mod settings panel satellite (no UI types).</summary>
public interface IModSettingsPanelHost {
    /// <summary><c>KitLib.ModPanel</c> initialized successfully in this process.</summary>
    bool IsModuleLoaded { get; }

    /// <summary>STS2-RitsuLib is loaded and the settings bridge can enumerate pages.</summary>
    bool IsRitsuBridgeAvailable { get; }
}
