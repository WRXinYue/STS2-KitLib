using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Helpers;

namespace KitLib.Panels;

/// <summary>Serializes async DevPanel actions (shared across Panel and Cheat action handlers).</summary>
public sealed class DevPanelActionSession {
    int _gen;
    public bool IsBusy { get; private set; }

    public void Cancel() {
        _gen++;
        IsBusy = false;
    }

    public void Run(Func<Task> work, string label, Action onCompleted) {
        IsBusy = true;
        int myGen = ++_gen;
        TaskHelper.RunSafely(Execute(work, label, myGen, onCompleted));
    }

    async Task Execute(Func<Task> work, string label, int myGen, Action onCompleted) {
        try { await work(); }
        catch (Exception ex) { MainFile.Logger.Warn($"DevPanel: {label} failed: {ex.Message}"); }
        finally {
            IsBusy = false;
            if (_gen == myGen)
                onCompleted();
        }
    }
}
