using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using KitLib.Screenshots;

namespace KitLib.Mcp.Tools;

/// <summary>Captures the game screen as an MCP image content block. Deliberately minimal: callers
/// decide what/when to capture, and cropping uses a node name rather than any heuristic.</summary>
internal sealed class DevScreenshotTool : IMcpResultTool {
    public string Name => "dev_screenshot";

    public string Description =>
        "Capture the game screen as an MCP image.Use when the answer is visual (layout, art, HUD, tooltips); " +
        "text/state tools are cheaper. Optional node crops to that Control's rect, format is png|jpeg, " +
        "hide_kitlib_chrome hides KitLib overlays, save writes the file under user://mcp-screenshots.";

    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "node": {
                "type": "string",
                "description": "Optional Control node name to crop to (exact name first, then substring), e.g. 'TopBar' or 'CombatRings'. Omit to capture the whole screen."
            },
            "format": {
                "type": "string",
                "enum": ["png", "jpeg"],
                "default": "png",
                "description": "Image format. jpeg is smaller for large screenshots."
            },
            "hide_kitlib_chrome": {
                "type": "boolean",
                "default": false,
                "description": "Hide KitLib's own overlays before capturing, to see only the game UI."
            },
            "save": {
                "type": "boolean",
                "default": false,
                "description": "Also write the image under user://mcp-screenshots and return the file path."
            }
        },
        "required": []
    }
    """;

    /// <summary><c>dev_batch</c> composes tools as text results; an image cannot travel through it.</summary>
    public Task<JsonNode> ExecuteAsync(JsonObject args) => Task.FromResult<JsonNode>(new JsonObject {
        ["ok"] = false,
        ["error"] = "dev_screenshot returns image content; call it directly, not through dev_batch.",
    });

    public async Task<JsonObject> ExecuteResultAsync(JsonObject args) {
        var format = args["format"]?.GetValue<string>()?.Trim();
        var node = args["node"]?.GetValue<string>()?.Trim();
        var hideKitLibChrome = args["hide_kitlib_chrome"]?.GetValue<bool>() ?? false;
        var save = args["save"]?.GetValue<bool>() ?? false;

        var capture = await ViewportScreenshot.CaptureAsync(format, hideKitLibChrome, node);
        if (capture is null)
            return McpToolRegistry.ErrorResult("Screenshot capture failed (viewport unavailable or too small).");

        var savedPath = save ? TrySave(capture) : null;
        var mimeType = capture.Format == ViewportScreenshot.JpegFormat ? "image/jpeg" : "image/png";
        var summary = $"Captured {capture.Width}x{capture.Height} {capture.Format} " +
                      $"({capture.Bytes.Length} bytes){(savedPath is null ? "" : $"; saved to {savedPath}")}.";

        var structured = new JsonObject {
            ["format"] = capture.Format,
            ["width"] = capture.Width,
            ["height"] = capture.Height,
            ["bytes"] = capture.Bytes.Length,
            ["node"] = capture.Node,
            ["hiddenKitLibChrome"] = hideKitLibChrome,
        };
        if (savedPath != null)
            structured["savedPath"] = savedPath;

        return new JsonObject {
            ["content"] = new JsonArray {
                new JsonObject {
                    ["type"] = "image",
                    ["data"] = Convert.ToBase64String(capture.Bytes),
                    ["mimeType"] = mimeType,
                },
                new JsonObject {
                    ["type"] = "text",
                    ["text"] = summary,
                },
            },
            ["structuredContent"] = structured,
        };
    }

    private static string? TrySave(ScreenshotCapture capture) {
        try {
            var directory = ProjectSettings.GlobalizePath("user://mcp-screenshots");
            Directory.CreateDirectory(directory);
            var extension = capture.Format == ViewportScreenshot.JpegFormat ? "jpg" : "png";
            var path = Path.Combine(directory, $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.{extension}");
            File.WriteAllBytes(path, capture.Bytes);
            return path;
        }
        catch (Exception ex) {
            KitLog.Warn("Mcp", $"Screenshot save failed: {ex.Message}");
            return null;
        }
    }
}
