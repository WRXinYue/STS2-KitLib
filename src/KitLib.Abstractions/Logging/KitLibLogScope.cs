namespace KitLib.Logging;

/// <summary>Fixed scope handle for repeated <c>[mod][scope]</c> logging.</summary>
public readonly struct KitLibLogScope {
    readonly string _scope;

    public KitLibLogScope(string scope) {
        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Scope is required.", nameof(scope));
        _scope = scope.Trim();
    }

    public void Debug(string message) => KitLibLog.Write(KitLogLevel.Debug, _scope, message);
    public void Info(string message) => KitLibLog.Write(KitLogLevel.Info, _scope, message);
    public void Warn(string message) => KitLibLog.Write(KitLogLevel.Warn, _scope, message);
    public void Error(string message) => KitLibLog.Write(KitLogLevel.Error, _scope, message);
}
