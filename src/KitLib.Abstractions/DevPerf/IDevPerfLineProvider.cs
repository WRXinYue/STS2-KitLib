namespace KitLib.DevPerf;

public interface IDevPerfLineProvider {
    int Order { get; }
    void AppendLines(List<DevPerfLine> lines);
}
