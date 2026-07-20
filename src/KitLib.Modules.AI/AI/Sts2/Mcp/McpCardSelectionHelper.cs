using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using KitLib.AI.Sts2.Helpers;
using KitLib.AI.Sts2.Snapshots;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace KitLib.AI.Sts2.Mcp;

internal static class McpCardSelectionHelper {
    const int ScreenReadyDelayMs = 400;

    public static bool IsActive() =>
        FindChoiceScreen() != null || NPlayerHand.Instance is { IsInCardSelection: true };

    public static JsonObject CaptureState() {
        if (NPlayerHand.Instance is { IsInCardSelection: true } hand) {
            var holders = VisibleHolders(hand.ActiveHolders.Cast<Node>());
            return new JsonObject {
                ["active"] = true,
                ["screenType"] = "hand",
                ["options"] = SerializeHolders(holders),
                ["confirmAvailable"] = hand.GetNodeOrNull<NConfirmButton>("%SelectModeConfirmButton") is { IsEnabled: true },
            };
        }

        var screen = FindChoiceScreen();
        if (screen == null)
            return new JsonObject { ["active"] = false };

        var screenType = screen switch {
            NCombatPileCardSelectScreen => "combat_pile",
            NChooseACardSelectionScreen => "choose_a_card",
            NDeckCardSelectScreen => "deck",
            NSimpleCardSelectScreen => "simple",
            NCardGridSelectionScreen => "grid",
            _ => screen.GetType().Name,
        };

        var options = VisibleHolders(UIHelper.FindAll<NCardHolder>(screen));
        var confirm = UIHelper.FindFirst<NConfirmButton>(screen) as NClickableControl
                      ?? UIHelper.FindFirst<NProceedButton>(screen);

        return new JsonObject {
            ["active"] = true,
            ["screenType"] = screenType,
            ["options"] = SerializeHolders(options),
            ["confirmAvailable"] = confirm is { IsEnabled: true },
        };
    }

    public static async Task<JsonObject> PickAsync(JsonObject args) {
        if (!IsActive())
            return Fail("No card selection screen is open.");

        await Task.Delay(ScreenReadyDelayMs);

        var indices = ParseIndices(args);
        var cardId = args["card_id"]?.GetValue<string>()?.Trim();
        var confirm = args["confirm"]?.GetValue<bool>() ?? true;

        if (NPlayerHand.Instance is { IsInCardSelection: true } hand)
            return await PickHandAsync(hand, indices, cardId, confirm);

        var screen = FindChoiceScreen();
        if (screen == null)
            return Fail("No card selection screen is open.");

        var holders = VisibleHolders(UIHelper.FindAll<NCardHolder>(screen));
        if (holders.Count == 0)
            return Fail("Selection screen has no visible cards.");

        var picked = ResolveHolders(holders, indices, cardId);
        if (picked.Count == 0)
            return Fail("No matching card to select. Provide card_index, card_indices, or card_id.");

        foreach (var holder in picked) {
            holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
            await Task.Delay(40);
        }

        if (confirm) {
            var confirmBtn = UIHelper.FindFirst<NConfirmButton>(screen) as NClickableControl
                             ?? UIHelper.FindFirst<NProceedButton>(screen);
            if (confirmBtn is { IsEnabled: true })
                await UIHelper.Click(confirmBtn);
        }

        await Task.Delay(50);
        return new JsonObject {
            ["ok"] = true,
            ["pickedCount"] = picked.Count,
            ["selectionActive"] = IsActive(),
        };
    }

    public static async Task<bool> TryAutoPickAsync(string? cardId, int? cardIndex, TimeSpan waitForScreen) {
        var deadline = DateTime.UtcNow + waitForScreen;
        while (DateTime.UtcNow < deadline) {
            if (IsActive()) {
                var args = new JsonObject();
                if (!string.IsNullOrWhiteSpace(cardId))
                    args["card_id"] = cardId;
                if (cardIndex.HasValue)
                    args["card_index"] = cardIndex.Value;
                args["confirm"] = true;
                var result = await PickAsync(args);
                return result["ok"]?.GetValue<bool>() == true;
            }

            if (NGame.Instance != null)
                await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
            else
                await Task.Delay(16);
        }

        return false;
    }

    static async Task<JsonObject> PickHandAsync(
        NPlayerHand hand,
        IReadOnlyList<int> indices,
        string? cardId,
        bool confirm) {
        var holders = VisibleHolders(hand.ActiveHolders.Cast<Node>());
        if (holders.Count == 0)
            return Fail("Hand selection has no visible cards.");

        var picked = ResolveHolders(holders, indices, cardId);
        if (picked.Count == 0)
            return Fail("No matching hand card to select.");

        foreach (var holder in picked) {
            holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
            await Task.Delay(40);
        }

        if (confirm) {
            var confirmBtn = hand.GetNodeOrNull<NConfirmButton>("%SelectModeConfirmButton");
            if (confirmBtn is { IsEnabled: true })
                await UIHelper.Click(confirmBtn);
        }

        await Task.Delay(50);
        return new JsonObject {
            ["ok"] = true,
            ["pickedCount"] = picked.Count,
            ["selectionActive"] = IsActive(),
        };
    }

    static List<int> ParseIndices(JsonObject args) {
        var indices = new List<int>();
        if (args["card_indices"] is JsonArray arr) {
            foreach (var node in arr) {
                if (node != null)
                    indices.Add(node.GetValue<int>());
            }
        }
        else if (args.TryGetPropertyValue("card_index", out var single))
            indices.Add(single!.GetValue<int>());
        return indices;
    }

    static List<NCardHolder> ResolveHolders(List<NCardHolder> holders, IReadOnlyList<int> indices, string? cardId) {
        if (!string.IsNullOrWhiteSpace(cardId)) {
            var match = holders.FirstOrDefault(h =>
                h.CardModel != null
                && string.Equals(((AbstractModel)h.CardModel).Id.Entry, cardId, StringComparison.OrdinalIgnoreCase));
            return match == null ? [] : [match];
        }

        if (indices.Count > 0) {
            var picked = new List<NCardHolder>();
            foreach (var index in indices) {
                if (index >= 0 && index < holders.Count)
                    picked.Add(holders[index]);
            }
            return picked;
        }

        return holders.Count > 0 ? [holders[0]] : [];
    }

    static List<NCardHolder> VisibleHolders(IEnumerable<Node> nodes) =>
        nodes.OfType<NCardHolder>()
            .Where(h => GodotObject.IsInstanceValid(h) && h.Visible && h.CardModel != null)
            .ToList();

    static List<NCardHolder> VisibleHolders(IEnumerable<NCardHolder> holders) =>
        holders.Where(h => GodotObject.IsInstanceValid(h) && h.Visible && h.CardModel != null).ToList();

    static JsonArray SerializeHolders(IReadOnlyList<NCardHolder> holders) {
        var arr = new JsonArray();
        for (var i = 0; i < holders.Count; i++) {
            var card = holders[i].CardModel!;
            var entry = SnapshotCardJson.FromCard(card, i);
            arr.Add(entry);
        }
        return arr;
    }

    static Node? FindChoiceScreen() {
        var stack = NOverlayStack.Instance;
        if (stack?.Peek() is not Node top)
            return null;

        if (top is NChooseACardSelectionScreen
            or NDeckCardSelectScreen
            or NCombatPileCardSelectScreen
            or NSimpleCardSelectScreen
            or NCardGridSelectionScreen)
            return top;

        return UIHelper.FindFirst<NChooseACardSelectionScreen>(top)
               ?? UIHelper.FindFirst<NCombatPileCardSelectScreen>(top)
               ?? UIHelper.FindFirst<NSimpleCardSelectScreen>(top)
               ?? UIHelper.FindFirst<NDeckCardSelectScreen>(top)
               ?? UIHelper.FindFirst<NCardGridSelectionScreen>(top) as Node;
    }

    static JsonObject Fail(string error) => new() {
        ["ok"] = false,
        ["error"] = error,
    };
}
