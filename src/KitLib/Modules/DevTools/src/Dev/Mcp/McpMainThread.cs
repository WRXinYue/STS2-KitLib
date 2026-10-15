using System;
using System.Threading.Tasks;
using Godot;

namespace KitLib.Mcp;

/// <summary>
/// Runs MCP tool handlers on the Godot main thread. HTTP listener threads must not call game APIs directly.
/// </summary>
internal static class McpMainThread {
    public static Task<T> InvokeAsync<T>(Func<Task<T>> action) {
        if (Engine.GetMainLoop() is not SceneTree)
            return Task.FromException<T>(new InvalidOperationException(
                "Godot scene tree is not available. Is the game running?"));

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Callable.From must wrap a void lambda — async () => Task breaks Godot Variant marshalling.
        Callable.From(() => Dispatch(action, tcs)).CallDeferred();
        return tcs.Task;
    }

    private static async void Dispatch<T>(Func<Task<T>> action, TaskCompletionSource<T> tcs) {
        try {
            tcs.SetResult(await action());
        }
        catch (Exception ex) {
            tcs.SetException(ex);
        }
    }
}
