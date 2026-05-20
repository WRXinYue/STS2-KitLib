using System.Collections.Generic;
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
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;
using DevMode.AI.Sts2.Helpers;
using DevMode.AI.Sts2.Snapshots;

namespace DevMode.AI.Sts2;

public sealed class Sts2StateProvider : IGameStateProvider
{
    public bool IsRunActive =>
        RunManager.Instance?.DebugOnlyGetState() != null;

    public GamePhase CurrentPhase
    {
        get
        {
            var overlay = NOverlayStack.Instance?.Peek();
            if (overlay != null)
            {
                // Terminal NRewardsScreen stays on stack until next room — skip to map if open.
                if (overlay is NRewardsScreen && NMapScreen.Instance is { IsOpen: true })
                    return GamePhase.MapSelection;

                return overlay switch
                {
                    NChooseARelicSelection => GamePhase.RelicSelection,
                    NRewardsScreen        => GamePhase.RewardScreen,
                    NCardRewardSelectionScreen => GamePhase.CardReward,
                    NGameOverScreen       => GamePhase.GameOver,
                    _                     => GamePhase.Unknown,
                };
            }

            if (!TryGetRunAndPlayer(out var state, out _))
                return GamePhase.None;

            var cm = CombatManager.Instance;
            if (cm is { IsInProgress: true, IsPlayPhase: true })
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
        return GameSnapshot.Capture(state, player);
    }

    public bool TryGetRunAndPlayer(out RunState state, out Player player)
    {
        state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null)
        {
            player = null!;
            return false;
        }
        player = LocalContext.GetMe((IEnumerable<Player>)state.Players)
            ?? state.Players.FirstOrDefault();
        return player != null;
    }
}
