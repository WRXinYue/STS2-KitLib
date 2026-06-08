using System;
using System.Threading.Tasks;
using Godot;

namespace KitLib.AI;

/// <summary>Runs combat snapshot / game API calls on the Godot main thread (AI poll runs on a threadpool thread).</summary>
internal static class AiMainThread {
    public static Task<T> InvokeAsync<T>(Func<T> action) {
        if (Engine.GetMainLoop() is not SceneTree)
            return Task.FromResult(action());

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Callable.From(() => {
            try {
                tcs.SetResult(action());
            }
            catch (Exception ex) {
                tcs.SetException(ex);
            }
        }).CallDeferred();
        return tcs.Task;
    }

    public static Task<T> InvokeAsync<T>(Func<Task<T>> action) {
        if (Engine.GetMainLoop() is not SceneTree)
            return action();

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Callable.From(() => Dispatch(action, tcs)).CallDeferred();
        return tcs.Task;
    }

    static async void Dispatch<T>(Func<Task<T>> action, TaskCompletionSource<T> tcs) {
        try {
            tcs.SetResult(await action());
        }
        catch (Exception ex) {
            tcs.SetException(ex);
        }
    }
}
