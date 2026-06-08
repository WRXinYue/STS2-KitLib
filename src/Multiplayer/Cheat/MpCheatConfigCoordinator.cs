using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Client → host config publish requests (Tier 1b).</summary>
internal static class MpCheatConfigCoordinator {
    private static readonly object Gate = new();
    private static readonly Dictionary<ulong, TaskCompletionSource<string>> ClientCompletions = new();
    private static ulong _nextClientRequestId;

    private const int ClientRequestTimeoutMs = 25000;

    public static async Task<string> TryClientPublishConfigAsync() {
        if (MpCheatSession.IsHost) {
            MpCheatSync.HostPublishFromKitLibState("ui");
            return I18N.T("mpcheat.config.hostPublished", "Cheat config published.");
        }

        if (!MpCheatSession.CanUseMultiplayerCheats)
            return I18N.T(
                "mpcheat.blocked",
                "Multiplayer cheat inactive: {0}",
                MpCheatSession.LastBlockReason ?? "unknown");

        var localNetId = MpCheatSession.ResolveLocalPlayerNetId();
        if (localNetId == 0)
            return I18N.T("mpcheat.config.noNetId", "No local net id for config sync.");

        var patch = MpCheatConfig.BuildClientPlayerPatch(localNetId);
        var configJson = MpCheatNetJson.SerializeConfig(patch);

        var clientRequestId = Interlocked.Increment(ref _nextClientRequestId);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (Gate) {
            ClientCompletions[clientRequestId] = completion;
        }

        MpCheatNetBus.ClientSendConfigRequest(new MpCheatConfigClientRequestMessage {
            ClientRequestId = clientRequestId,
            RequesterNetId = localNetId,
            ConfigJson = configJson,
        });
        MainFile.Logger.Info($"[MpCheat] Config client request id={clientRequestId}.");

        try {
            using var cts = new CancellationTokenSource(ClientRequestTimeoutMs);
            return await completion.Task.WaitAsync(cts.Token);
        }
        catch (TaskCanceledException) {
            return I18N.T(
                "mpcheat.config.clientRequestTimeout",
                "Host did not respond to config request in time.");
        }
        finally {
            lock (Gate) {
                ClientCompletions.Remove(clientRequestId);
            }
        }
    }

    public static void OnClientConfigRequestReceived(MpCheatConfigClientRequestMessage request, ulong senderId) {
        if (!MpCheatSession.IsHost) return;

        void Reply(bool success, string message) =>
            MpCheatNetBus.HostSendConfigRequestResult(senderId, new MpCheatAddCardClientResultMessage {
                ClientRequestId = request.ClientRequestId,
                Success = success,
                Message = message,
            });

        if (!MpCheatSession.CanEditMultiplayerCheats) {
            Reply(false, I18N.T("mpcheat.config.hostOnly", "Only the host can publish cheat config."));
            return;
        }

        if (request.RequesterNetId != senderId) {
            Reply(false, I18N.T("mpcheat.config.requestMismatch", "Config request sender mismatch."));
            return;
        }

        var config = MpCheatNetJson.DeserializeConfig(request.ConfigJson);
        if (config == null) {
            Reply(false, I18N.T("mpcheat.config.invalidJson", "Invalid cheat config JSON."));
            return;
        }

        var merged = MpCheatState.Config.MergeClientPlayerPatch(config, senderId);
        MpCheatNetBus.HostPublishConfig(merged, $"client_request:{senderId}");
        MainFile.Logger.Info($"[MpCheat] Per-player config merged from client {senderId}.");
        Reply(true, I18N.T("mpcheat.config.published", "Cheat config synced."));
    }

    public static void OnClientConfigResultReceived(MpCheatAddCardClientResultMessage result) {
        if (MpCheatSession.IsHost) return;

        TaskCompletionSource<string>? completion;
        lock (Gate) {
            ClientCompletions.TryGetValue(result.ClientRequestId, out completion);
        }

        if (completion == null) return;
        completion.TrySetResult(result.Message);
    }

    internal static void Reset() {
        lock (Gate) {
            foreach (var tcs in ClientCompletions.Values)
                tcs.TrySetResult(I18N.T("mpcheat.config.cancelled", "Config request cancelled (run ended)."));
            ClientCompletions.Clear();
        }
    }
}
