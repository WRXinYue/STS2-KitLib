namespace KitLib.DevPerf;

internal static class DevPerfFrameTimeSampler {
    const double PeakWindowSec = 1.0;
    const double SpikeThresholdMs = 100.0;

    static double _windowElapsed;
    static double _windowPeakMs;
    static double _displayPeakMs;
    static double _lastFrameMs;

    internal static double LastFrameMs => _lastFrameMs;
    internal static double WindowPeakMs => Math.Max(_displayPeakMs, _windowPeakMs);

    internal static void Process(double deltaSeconds) {
        var frameMs = Math.Max(0, deltaSeconds * 1000.0);
        _lastFrameMs = frameMs;
        _windowElapsed += deltaSeconds;
        _windowPeakMs = Math.Max(_windowPeakMs, frameMs);

        if (frameMs >= SpikeThresholdMs)
            DevPerfEventLog.LogFrameSpike(frameMs);

        if (_windowElapsed < PeakWindowSec)
            return;

        _displayPeakMs = _windowPeakMs;
        _windowPeakMs = 0;
        _windowElapsed = 0;
    }
}
