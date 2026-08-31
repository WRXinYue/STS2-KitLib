using System.Reflection;

namespace KitLib.ModVariantLoader;

public sealed class ModVariantBootstrapOptions {
    public string? ModId { get; init; }

    public string? ImplementationAssemblyFileName { get; init; }

    public string? LogPrefix { get; init; }

    public string? HarmonyId { get; init; }

    /// <summary>
    /// Thin loader assembly. Prefer <c>typeof(MainFile).Assembly</c> from
    /// <c>eng/ModVariantContentLoader</c>. Do not use <c>Assembly.GetCallingAssembly()</c>
    /// (JIT inlining can resolve to sts2.dll).
    /// </summary>
    public Assembly? HostAssembly { get; init; }

    /// <summary>
    /// Optional override when the thin loader DLL is not colocated with <c>lib/</c>.
    /// </summary>
    public string? LoaderModDirectory { get; init; }
}
