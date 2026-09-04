namespace KitLib.Abstractions.Host;

public interface IKitLibModule {
    string Id { get; }
    void OnInitialize();
}
