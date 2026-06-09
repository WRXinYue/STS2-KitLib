namespace KitLib.ModPanel.Tests;

/// <summary>In-memory sink for asserting ModPanel diagnostic log lines in unit tests.</summary>
internal sealed class ModPanelDiagnosticLogStub {
    readonly List<string> _info = [];
    readonly List<string> _warn = [];

    public IReadOnlyList<string> InfoLines => _info;
    public IReadOnlyList<string> WarnLines => _warn;

    public void Info(string line) => _info.Add(line);
    public void Warn(string line) => _warn.Add(line);

    public void EmitOpen(KitLib.Abstractions.Modding.ModPanelOpenReport report) {
        Info(KitLib.Abstractions.Modding.ModPanelDiagnosticLog.FormatOpen(report));
        foreach (var warning in KitLib.Abstractions.Modding.ModPanelDiagnosticLog.CollectOpenWarnings(report))
            Warn(warning);
    }

    public void EmitLayout(
        KitLib.Abstractions.Modding.ModPanelOpenReport openReport,
        KitLib.Abstractions.Modding.ModPanelLayoutSnapshot layout) {
        Info(KitLib.Abstractions.Modding.ModPanelDiagnosticLog.FormatLayout(layout));
        foreach (var warning in KitLib.Abstractions.Modding.ModPanelDiagnosticLog.CollectLayoutWarnings(
                     openReport, layout))
            Warn(warning);
    }
}
