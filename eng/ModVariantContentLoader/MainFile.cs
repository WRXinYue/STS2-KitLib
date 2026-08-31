using System.Reflection;
using KitLib.ModVariantLoader;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.ModVariantLoader.ContentEntry;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public static void Initialize() {
        Assembly host = typeof(MainFile).Assembly;
        string? hostDir = Path.GetDirectoryName(host.Location);
        ModVariantBootstrap.Initialize(new ModVariantBootstrapOptions {
            HostAssembly = host,
            LoaderModDirectory = string.IsNullOrEmpty(hostDir) ? null : hostDir,
        });
    }
}
