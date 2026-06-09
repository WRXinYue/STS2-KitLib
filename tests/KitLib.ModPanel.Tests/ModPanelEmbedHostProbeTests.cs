using System.Text;
using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class ModPanelEmbedHostProbeTests {
    [Fact]
    public void SubmenuFullName_matches_ritsu_contract() {
        Assert.Equal("STS2RitsuLib.Settings.RitsuModSettingsSubmenu", ModPanelEmbedHostProbe.SubmenuFullName);
    }

    [Fact]
    public void Probe_reports_missing_when_assembly_null() {
        var result = ModPanelEmbedHostProbe.Probe(null);
        Assert.Equal(ModPanelEmbedHostStatus.RitsuAssemblyMissing, result.Status);
        Assert.False(result.IsReady);
    }

    [Fact]
    public void Ritsu_dll_embeds_submenu_type_name() {
        var dll = ResolveRitsuDllPath();
        if (dll == null)
            return;
        var hay = Encoding.UTF8.GetString(File.ReadAllBytes(dll));
        Assert.Contains(ModPanelEmbedHostProbe.SubmenuFullName, hay, StringComparison.Ordinal);
    }

    static string? ResolveRitsuDllPath() {
        var modsRoot = Environment.GetEnvironmentVariable("STS2_MODS_ROOT");
        if (string.IsNullOrWhiteSpace(modsRoot)) {
            var sts2Dir = Environment.GetEnvironmentVariable("STS2_DIR");
            if (!string.IsNullOrWhiteSpace(sts2Dir))
                modsRoot = Path.Combine(sts2Dir, "mods");
        }
        if (string.IsNullOrWhiteSpace(modsRoot)) {
            var defaultSteam = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam", "steamapps", "common", "Slay the Spire 2", "mods");
            if (Directory.Exists(defaultSteam))
                modsRoot = defaultSteam;
        }
        if (string.IsNullOrWhiteSpace(modsRoot))
            return null;
        var dll = Path.Combine(modsRoot, "STS2-RitsuLib", "STS2-RitsuLib.dll");
        return File.Exists(dll) ? dll : null;
    }
}
