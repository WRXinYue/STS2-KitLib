using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using KitLib.AI;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.Host;
using KitLib.AI.Sts2.Helpers;
using KitLib.AI.Sts2.Snapshots;

namespace KitLib.AI.Sts2;

public sealed class Sts2StateProvider : IGameStateProvider
{
    public bool IsRunActive =>
        RunManager.Instance?.DebugOnlyGetState() != null;

    public GamePhase CurrentPhase
    {
        get
        {
            // Card / relic picks must win over background map even when NMapScreen is open (LAN Neow).
            if (OverlayPhaseHelper.HasActiveCardRewardScreen())
                return GamePhase.CardReward;
            if (OverlayPhaseHelper.HasActiveRelicSelectionScreen())
                return GamePhase.RelicSelection;

            var cm = CombatManager.Instance;

            var overlay = NOverlayStack.Instance?.Peek();
            if (overlay != null)
            {
                if (overlay is NRewardsScreen rewardsScreen) {
                    Player? rewardPlayer = null;
                    TryGetRunAndPlayer(out var runState, out rewardPlayer);

                    JsonObject? rewardSnap = null;
                    if (rewardPlayer is { HasOpenPotionSlots: false }) {
                        // Capture with an explicit phase — TakeSnapshot() reads CurrentPhase and would recurse here.
                        rewardSnap = GameSnapshot.Capture(runState, rewardPlayer, GamePhase.RewardScreen);
                    }

                    var hasCollectable = OverlayPhaseHelper.HasClickableRewards(
                        rewardsScreen, rewardPlayer?.HasOpenPotionSlots ?? false, rewardSnap);

                    if (OverlayPhaseHelper.RewardsReadyForMap(rewardsScreen, rewardPlayer, rewardSnap))
                        return GamePhase.MapSelection;

                    // Rest heal extras (e.g. TinyMailbox potions) must collect before RestScorer proceeds.
                    if (hasCollectable)
                        return GamePhase.RewardScreen;

                    // Terminal loot screen stays on the stack after Proceed; prefer room phase when drained.
                    if (TryGetInRoomPhase(runState.CurrentRoom?.RoomType) is { } inRoomPhase)
                        return inRoomPhase;

                    return GamePhase.RewardScreen;
                }

                return overlay switch
                {
                    NChooseARelicSelection => GamePhase.RelicSelection,
                    NCardRewardSelectionScreen => GamePhase.CardReward,
                    NDeckCardSelectScreen => GamePhase.CardReward,
                    NChooseACardSelectionScreen => GamePhase.CardReward,
                    NGameOverScreen       => GamePhase.GameOver,
                    _                     => cm is { IsInProgress: true }
                        ? GamePhase.Combat
                        : GamePhase.Unknown,
                };
            }

            if (!TryGetRunAndPlayer(out var state, out _))
                return GamePhase.None;

            if (cm is { IsInProgress: true })
                return GamePhase.Combat;

            // Post-combat: wait for rewards overlay to appear
            if (cm != null && !cm.IsInProgress
                && state.CurrentRoom?.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss)
            {
                var stack = NOverlayStack.Instance;
                if (stack == null || stack.ScreenCount == 0)
                {
                    if (NMapScreen.Instance is not { IsOpen: true })
                        return GamePhase.PostCombatTransition;
                }
            }

            if (NMapScreen.Instance is { IsOpen: true })
                return GamePhase.MapSelection;

            var room = state.CurrentRoom;
            if (room != null)
            {
                return room.RoomType switch
                {
                    RoomType.Event    => GamePhase.EventChoice,
                    RoomType.Shop     => GamePhase.Shop,
                    RoomType.RestSite => GamePhase.RestSite,
                    RoomType.Treasure => GamePhase.TreasureRoom,
                    _              => GamePhase.Unknown,
                };
            }

            return GamePhase.Unknown;
        }
    }

    public JsonObject TakeSnapshot()
    {
        if (!TryGetRunAndPlayer(out var state, out var player))
            return new JsonObject();
        return GameSnapshot.Capture(state, player, CurrentPhase);
    }

    public Task<JsonObject> TakeSnapshotAsync()
    {
        if (!TryGetRunAndPlayer(out var state, out var player))
            return Task.FromResult(new JsonObject());

        if (player.PlayerCombatState != null) {
            return AiMainThread.InvokeAsync(() => {
                var phase = CurrentPhase;
                return GameSnapshot.Capture(state, player, phase);
            });
        }

        return Task.FromResult(GameSnapshot.Capture(state, player, CurrentPhase));
    }

    public bool TryGetRunAndPlayer(out RunState state, out Player player)
    {
        state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null)
        {
            player = null!;
            return false;
        }
        if (AiHostContext.TryGetControlledPlayer(state, out player))
            return true;

        player = LocalContext.GetMe((IEnumerable<Player>)state.Players);
        if (player != null)
            return true;

        // Never guess player 1 in multiplayer — causes cross-driving between LanLocal / MpAi / Companion.
        if (MultiplayerRunProbe.InMultiplayerRun)
            return false;

        player = state.Players.FirstOrDefault();
        return player != null;
    }

    static GamePhase? TryGetInRoomPhase(RoomType? roomType) =>
        roomType switch {
            RoomType.RestSite => GamePhase.RestSite,
            RoomType.Shop => GamePhase.Shop,
            RoomType.Event => GamePhase.EventChoice,
            RoomType.Treasure => GamePhase.TreasureRoom,
            _ => null,
        };
}
