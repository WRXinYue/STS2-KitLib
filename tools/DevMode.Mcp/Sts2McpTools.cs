using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace KitLib.McpProxy;

[McpServerToolType]
internal sealed class Sts2McpTools {
    private readonly GameBridgeClient _bridge;

    public Sts2McpTools(GameBridgeClient bridge) => _bridge = bridge;

    [McpServerTool(Name = "get_game_state", ReadOnly = true), Description(
        "Get the current STS2 game state. In combat includes playerPowers, phase, and indexed enemies with powers.")]
    public Task<string> GetGameState(CancellationToken cancellationToken) =>
        _bridge.CallToolAsync("get_game_state", new JsonObject(), cancellationToken);

    [McpServerTool(Name = "combat_action"), Description(
        "Execute a combat action. play_card success returns afterState unless pseudo-coop queued.")]
    public Task<string> CombatAction(
        [Description("The combat action to perform: play_card, end_turn, or use_potion.")]
        string action,
        [Description("Index of the card in hand to play (for play_card).")]
        int card_index = 0,
        [Description("Index of the enemy to target (for targeted cards). -1 for untargeted.")]
        int target_index = -1,
        CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("combat_action", new JsonObject {
            ["action"] = action,
            ["card_index"] = card_index,
            ["target_index"] = target_index,
        }, cancellationToken);

    [McpServerTool(Name = "map_action"), Description(
        "Execute a non-combat action: select map node, pick card reward, choose event option, interact with shop, rest, or proceed.")]
    public Task<string> MapAction(
        [Description(
            "The action to perform: select_map_node, pick_card_reward, skip_card_reward, select_event_choice, " +
            "purchase_shop_item, remove_card_at_shop, leave_shop, rest, upgrade_card, collect_reward, " +
            "dismiss_rewards, or proceed.")]
        string action,
        [Description("Index of the target (node, card, item, etc.).")]
        int target_index = 0,
        CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("map_action", new JsonObject {
            ["action"] = action,
            ["target_index"] = target_index,
        }, cancellationToken);

    [McpServerTool(Name = "dev_get_session", ReadOnly = true), Description(
        "Get DevMode session state: run active, game phase, dev-run flag, and blocking startup prompts.")]
    public Task<string> DevGetSession(CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("dev_get_session", new JsonObject(), cancellationToken);

    [McpServerTool(Name = "dev_list_save_slots", ReadOnly = true), Description(
        "List DevMode save slots with metadata and debug notes for AI slot selection.")]
    public Task<string> DevListSaveSlots(CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("dev_list_save_slots", new JsonObject(), cancellationToken);

    [McpServerTool(Name = "dev_tag_save_slot"), Description(
        "Set debug notes on a save slot so agents can identify what the save is for.")]
    public Task<string> DevTagSaveSlot(
        [Description("Save slot ID (0 = quick save).")]
        int slot_id,
        [Description("Debug label, e.g. combat:ironclad-act1-boss.")]
        string notes,
        CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("dev_tag_save_slot", new JsonObject {
            ["slot_id"] = slot_id,
            ["notes"] = notes,
        }, cancellationToken);

    [McpServerTool(Name = "dev_load_save_slot"), Description(
        "Load a DevMode save slot. Poll dev_get_session until runActive is true.")]
    public Task<string> DevLoadSaveSlot(
        [Description("Save slot ID (default 0 = quick save).")]
        int slot_id = 0,
        CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("dev_load_save_slot", new JsonObject {
            ["slot_id"] = slot_id,
        }, cancellationToken);

    [McpServerTool(Name = "dev_start_test_run"), Description(
        "Start a new DevMode test run from the main menu (opens character select). Optional seed.")]
    public Task<string> DevStartTestRun(
        [Description("Optional run seed override.")]
        string? seed = null,
        CancellationToken cancellationToken = default) {
        var args = new JsonObject();
        if (!string.IsNullOrWhiteSpace(seed))
            args["seed"] = seed.Trim();
        return _bridge.CallToolAsync("dev_start_test_run", args, cancellationToken);
    }

    [McpServerTool(Name = "dev_list_cards", ReadOnly = true), Description(
        "List cards in deck/hand/draw/discard/exhaust piles, or all piles.")]
    public Task<string> DevListCards(
        [Description("Pile: deck, hand, draw, discard, exhaust, or all (default all).")]
        string target = "all",
        CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("dev_list_cards", new JsonObject {
            ["target"] = target,
        }, cancellationToken);

    [McpServerTool(Name = "dev_add_card"), Description(
        "Add a card to deck or a combat pile (DevMode card browser API).")]
    public Task<string> DevAddCard(
        [Description("Card model ID, e.g. IRONCLAD_CARD_STRIKE.")]
        string card_id,
        [Description("Target pile: deck, hand, draw, discard, exhaust (default hand).")]
        string target = "hand",
        [Description("perm or temp for combat piles (default perm).")]
        string duration = "perm",
        [Description("Upgrade levels to apply (default 0).")]
        int upgrade_levels = 0,
        CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("dev_add_card", new JsonObject {
            ["card_id"] = card_id,
            ["target"] = target,
            ["duration"] = duration,
            ["upgrade_levels"] = upgrade_levels,
        }, cancellationToken);

    [McpServerTool(Name = "dev_remove_card"), Description(
        "Remove a card from a pile by card_id or pile_index.")]
    public Task<string> DevRemoveCard(
        [Description("Target pile: deck, hand, draw, discard, exhaust (default hand).")]
        string target = "hand",
        [Description("Card model ID (first match in pile).")]
        string? card_id = null,
        [Description("Index in pile from dev_list_cards.")]
        int? pile_index = null,
        [Description("Remove from run deck when deleting from combat piles (default true).")]
        bool permanent = true,
        CancellationToken cancellationToken = default) {
        var args = new JsonObject {
            ["target"] = target,
            ["permanent"] = permanent,
        };
        if (!string.IsNullOrWhiteSpace(card_id))
            args["card_id"] = card_id.Trim();
        if (pile_index.HasValue)
            args["pile_index"] = pile_index.Value;
        return _bridge.CallToolAsync("dev_remove_card", args, cancellationToken);
    }

    [McpServerTool(Name = "dev_list_monsters", ReadOnly = true), Description(
        "List known monster model IDs for dev_add_monster.")]
    public Task<string> DevListMonsters(
        [Description("Optional ID prefix filter, e.g. OVICO.")]
        string? prefix = null,
        CancellationToken cancellationToken = default) {
        var args = new JsonObject();
        if (!string.IsNullOrWhiteSpace(prefix))
            args["prefix"] = prefix.Trim();
        return _bridge.CallToolAsync("dev_list_monsters", args, cancellationToken);
    }

    [McpServerTool(Name = "dev_list_enemies", ReadOnly = true), Description(
        "List enemies currently in combat.")]
    public Task<string> DevListEnemies(CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("dev_list_enemies", new JsonObject(), cancellationToken);

    [McpServerTool(Name = "dev_add_monster"), Description(
        "Add a monster to the current combat (DevMode enemy browser / dmenemy spawn).")]
    public Task<string> DevAddMonster(
        [Description("Monster model ID, e.g. OVICOPTER.")]
        string monster_id,
        CancellationToken cancellationToken = default) =>
        _bridge.CallToolAsync("dev_add_monster", new JsonObject {
            ["monster_id"] = monster_id,
        }, cancellationToken);

    [McpServerTool(Name = "dev_set_cheat"), Description(
        "Toggle patch/runtime cheats or set multiplier values.")]
    public Task<string> DevSetCheat(
        [Description("Cheat id, e.g. freeze_enemies, damage_multiplier, god_mode.")]
        string cheat,
        [Description("For toggles. Omit to flip current state.")]
        bool? enabled = null,
        [Description("For multipliers or extra_draw amount.")]
        double? value = null,
        CancellationToken cancellationToken = default) {
        var args = new JsonObject { ["cheat"] = cheat };
        if (enabled.HasValue)
            args["enabled"] = enabled.Value;
        if (value.HasValue)
            args["value"] = value.Value;
        return _bridge.CallToolAsync("dev_set_cheat", args, cancellationToken);
    }

    [McpServerTool(Name = "dev_set_stat"), Description(
        "Set run/combat stats or enable stat locks.")]
    public Task<string> DevSetStat(
        [Description("gold, current_hp, max_hp, current_energy, max_energy, stars, orb_slots, potion_slots.")]
        string stat,
        [Description("Target value.")]
        int value,
        [Description("When true, lock stat at value. When false, disable lock. Omit for one-shot set.")]
        bool? @lock = null,
        CancellationToken cancellationToken = default) {
        var args = new JsonObject {
            ["stat"] = stat,
            ["value"] = value,
        };
        if (@lock.HasValue)
            args["lock"] = @lock.Value;
        return _bridge.CallToolAsync("dev_set_stat", args, cancellationToken);
    }
}
