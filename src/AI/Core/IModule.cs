namespace KitLib.AI.Core;

/// <summary>
/// A pluggable module within the STS2-AI mod.
/// Modules are registered at startup and have independent lifecycles.
/// </summary>
public interface IModule
{
    /// <summary>Unique identifier for this module.</summary>
    string Id { get; }

    /// <summary>Human-readable name.</summary>
    string Name { get; }

    /// <summary>Whether this module is currently enabled.</summary>
    bool Enabled { get; set; }

    /// <summary>Called once when the mod initializes.</summary>
    void OnInitialize();

    /// <summary>Called when a run starts.</summary>
    void OnRunStarted();

    /// <summary>Called when a run ends.</summary>
    void OnRunEnded();

    /// <summary>Called when the mod is being unloaded.</summary>
    void OnShutdown();
}
