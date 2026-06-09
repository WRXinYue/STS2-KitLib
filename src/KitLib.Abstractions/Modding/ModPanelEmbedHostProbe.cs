using System;
using System.Reflection;

namespace KitLib.Abstractions.Modding;

public enum ModPanelEmbedHostStatus {
    RitsuAssemblyMissing,
    SubmenuTypeNotFound,
    SubmenuTypeNotNode,
    MissingParameterlessCtor,
    Ready,
}

public readonly record struct ModPanelEmbedHostProbeResult(
    ModPanelEmbedHostStatus Status,
    string? AssemblyName,
    string? ResolvedTypeName,
    string? Detail) {
    public bool IsReady => Status == ModPanelEmbedHostStatus.Ready;
}

/// <summary>Reflection-only probe for <c>RitsuModSettingsSubmenu</c> (no Godot instantiation).</summary>
public static class ModPanelEmbedHostProbe {
    public const string SubmenuFullName = "STS2RitsuLib.Settings.RitsuModSettingsSubmenu";

    public static ModPanelEmbedHostProbeResult Probe(Assembly? ritsuAssembly) {
        if (ritsuAssembly == null) {
            return new(ModPanelEmbedHostStatus.RitsuAssemblyMissing, null, SubmenuFullName,
                "STS2-RitsuLib assembly not in AppDomain");
        }
        var submenuType = ritsuAssembly.GetType(SubmenuFullName);
        if (submenuType == null) {
            return new(ModPanelEmbedHostStatus.SubmenuTypeNotFound, ritsuAssembly.GetName().Name, SubmenuFullName,
                $"Type not found in {ritsuAssembly.Location}");
        }
        if (!IsGodotNodeType(submenuType)) {
            return new(ModPanelEmbedHostStatus.SubmenuTypeNotNode, ritsuAssembly.GetName().Name, submenuType.FullName,
                $"Resolved type base is {submenuType.BaseType?.FullName ?? "unknown"}, expected Godot.Node");
        }
        if (submenuType.GetConstructor(Type.EmptyTypes) == null) {
            return new(ModPanelEmbedHostStatus.MissingParameterlessCtor, ritsuAssembly.GetName().Name,
                submenuType.FullName, "No public parameterless constructor");
        }
        return new(ModPanelEmbedHostStatus.Ready, ritsuAssembly.GetName().Name, submenuType.FullName, null);
    }

    static bool IsGodotNodeType(Type type) {
        for (var t = type; t != null; t = t.BaseType) {
            if (string.Equals(t.FullName, "Godot.Node", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
