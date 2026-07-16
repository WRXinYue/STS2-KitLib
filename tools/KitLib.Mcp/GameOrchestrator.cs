using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace KitLib.McpProxy;

/// <summary>Launch/wait/stop STS2 outside the in-game MCP bridge.</summary>
internal sealed class GameOrchestrator : IDisposable {
    private static readonly string[] Sts2ProcessNames = ["SlayTheSpire2", "Slay the Spire 2"];
    private static readonly TimeSpan PythonProbeTimeout = TimeSpan.FromSeconds(5);

    private static string? _cachedPython;

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public JsonObject Launch() {
        var started = DateTime.UtcNow;
        string? repoRoot = ResolveRepoRoot();
        if (repoRoot == null) {
            return Error("Could not find KitLib repo root. Set KITLIB_REPO_ROOT in MCP env.");
        }

        string python = ResolvePython();
        string launchScript = ResolveLaunchScript(repoRoot);
        if (!File.Exists(launchScript))
            return Error($"Missing launch script: {launchScript}");

        int? launchPid = StartLaunchProcess(python, repoRoot, launchScript, repoRoot);
        if (launchPid == null)
            return Error($"Failed to start launch script: {launchScript}");

        return new JsonObject {
            ["success"] = true,
            ["launched"] = true,
            ["launchPid"] = launchPid.Value,
            ["repoRoot"] = repoRoot,
            ["launchScript"] = launchScript,
            ["elapsedMs"] = (int)(DateTime.UtcNow - started).TotalMilliseconds,
            ["next"] = "Call dev_wait_bridge to poll until GET /health returns ok.",
        };
    }

    private static int? StartLaunchProcess(string python, string repoRoot, string launchScript, string repoRootArg) {
        var psi = new ProcessStartInfo {
            FileName = python,
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(launchScript);
        psi.ArgumentList.Add("--repo-root");
        psi.ArgumentList.Add(repoRootArg);
        return Process.Start(psi)?.Id;
    }

    public async Task<JsonObject> WaitBridgeAsync(int port, double timeoutSec, CancellationToken ct) {
        string url = $"http://127.0.0.1:{port}/health";
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, timeoutSec));
        string lastError = "";

        while (DateTime.UtcNow < deadline) {
            ct.ThrowIfCancellationRequested();
            try {
                using var response = await _http.GetAsync(url, ct);
                string body = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode && body.Contains("ok", StringComparison.OrdinalIgnoreCase)) {
                    return new JsonObject {
                        ["ready"] = true,
                        ["port"] = port,
                        ["healthUrl"] = url,
                        ["body"] = body.Trim(),
                    };
                }
                lastError = $"HTTP {(int)response.StatusCode}: {body}";
            }
            catch (Exception ex) {
                lastError = ex.Message;
            }

            await Task.Delay(1000, ct);
        }

        return new JsonObject {
            ["ready"] = false,
            ["port"] = port,
            ["healthUrl"] = url,
            ["error"] = $"Timed out after {timeoutSec:0}s. Last error: {lastError}",
        };
    }

    public JsonObject StopGame() {
        var stopped = new JsonArray();
        foreach (int pid in ListRunningSts2ProcessIds()) {
            try {
                var proc = Process.GetProcessById(pid);
                string name = proc.ProcessName;
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
                stopped.Add(new JsonObject {
                    ["pid"] = pid,
                    ["name"] = name,
                });
            }
            catch (Exception ex) {
                stopped.Add(new JsonObject {
                    ["pid"] = pid,
                    ["error"] = ex.Message,
                });
            }
        }

        return new JsonObject {
            ["stopped"] = stopped,
            ["message"] = stopped.Count == 0 ? "No running STS2 processes found." : null,
        };
    }

    private static IReadOnlyList<int> ListRunningSts2ProcessIds() {
        var ids = new HashSet<int>();
        foreach (string name in Sts2ProcessNames) {
            Process[] processes;
            try {
                processes = Process.GetProcessesByName(name);
            }
            catch {
                continue;
            }

            foreach (var process in processes) {
                try {
                    ids.Add(process.Id);
                }
                finally {
                    process.Dispose();
                }
            }
        }

        return ids.OrderBy(id => id).ToArray();
    }

    private static JsonObject Error(string message) => new() {
        ["success"] = false,
        ["error"] = message,
    };

    private static string ResolveLaunchScript(string repoRoot) {
        string? fromEnv = Environment.GetEnvironmentVariable("KITLIB_LAUNCH_SCRIPT");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return Path.GetFullPath(fromEnv);

        return Path.Combine(repoRoot, "scripts", "launch_sts2.py");
    }

    private static string? ResolveRepoRoot() {
        string? fromEnv = Environment.GetEnvironmentVariable("KITLIB_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
            return Path.GetFullPath(fromEnv);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir != null; i++) {
            string scripts = Path.Combine(dir.FullName, "scripts", "launch_sts2.py");
            if (File.Exists(scripts))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    private static string ResolvePython() {
        if (_cachedPython != null)
            return _cachedPython;

        foreach (string candidate in new[] { "python", "python3", "py" }) {
            try {
                var (code, _, timedOut) = RunProcess(candidate, Environment.CurrentDirectory, PythonProbeTimeout, "--version");
                if (!timedOut && code == 0) {
                    _cachedPython = candidate;
                    return candidate;
                }
            }
            catch {
                // try next
            }
        }

        _cachedPython = "python";
        return _cachedPython;
    }

    private static (int exitCode, string output, bool timedOut) RunProcess(
        string fileName,
        string workingDirectory,
        TimeSpan timeout,
        params string[] arguments) {
        var psi = new ProcessStartInfo {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string arg in arguments)
            psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        bool exited = proc.WaitForExit((int)timeout.TotalMilliseconds);
        if (!exited) {
            try {
                proc.Kill(entireProcessTree: true);
            }
            catch {
                // best effort
            }
            return (-1, CombineOutput(stdout, stderr), true);
        }

        proc.WaitForExit();
        return (proc.ExitCode, CombineOutput(stdout, stderr), false);
    }

    private static string CombineOutput(StringBuilder stdout, StringBuilder stderr) {
        string outText = stdout.ToString().TrimEnd();
        string errText = stderr.ToString().TrimEnd();
        return string.IsNullOrWhiteSpace(errText) ? outText : $"{outText}\n{errText}".Trim();
    }

    public void Dispose() => _http.Dispose();
}
