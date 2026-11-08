using System.Threading.Tasks;
using KitLib.Screenshots;

namespace KitLib.UI;

/// <summary>Hides KitLib chrome for one frame and captures the game viewport as PNG.</summary>
internal static class FeedbackScreenshotCapture {
    internal static async Task<byte[]?> TryCapturePngAsync() {
        var capture = await ViewportScreenshot.CapturePngAsync(hideKitLibChrome: true);
        return capture?.Bytes;
    }
}
