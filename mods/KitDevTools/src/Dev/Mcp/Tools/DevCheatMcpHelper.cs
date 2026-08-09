using System.Text.Json.Nodes;
using KitLib.Abstractions.Host;
using KitLib.Cheat;
using KitLib.RunInventory;

namespace KitLib.Mcp.Tools;

internal static class DevCheatMcpHelper {
    public static JsonObject Fail(string error) => new() {
        ["ok"] = false,
        ["error"] = error,
    };

    public static bool TryRequireCheats(out JsonObject? error) {
        error = null;
        if (!KitLibState.CheatsInRun) {
            error = Fail("Cheats are not active. Start a dev test run or set NormalRunMode to Cheat.");
            return false;
        }
        return true;
    }

    public static RuntimeStatModifiers EnsureStatModifiers() => CheatRunState.Ensure();

    public static bool? ParseOptionalBool(JsonObject args, out string? error) {
        error = null;
        if (!args.TryGetPropertyValue("enabled", out var node))
            return null;

        if (node?.GetValueKind() == System.Text.Json.JsonValueKind.True)
            return true;
        if (node?.GetValueKind() == System.Text.Json.JsonValueKind.False)
            return false;

        if (node?.GetValueKind() == System.Text.Json.JsonValueKind.String) {
            return node.GetValue<string>()!.Trim().ToLowerInvariant() switch {
                "on" or "true" or "1" or "yes" => true,
                "off" or "false" or "0" or "no" => false,
                _ => null,
            };
        }

        error = "Invalid enabled value. Use true/false or on/off.";
        return null;
    }

    public static bool TryParseCheatName(string? raw, out string cheat, out string error) {
        cheat = (raw ?? "").Trim().ToLowerInvariant().Replace('-', '_');
        error = "";
        if (string.IsNullOrEmpty(cheat)) {
            error = "Missing cheat name.";
            return false;
        }
        return true;
    }

    public static bool TryParseStat(string? raw, out KitLibRunStat stat, out string error) {
        error = "";
        switch ((raw ?? "").Trim().ToLowerInvariant().Replace('-', '_')) {
            case "gold":
                stat = KitLibRunStat.Gold;
                return true;
            case "current_hp":
                stat = KitLibRunStat.CurrentHp;
                return true;
            case "max_hp":
                stat = KitLibRunStat.MaxHp;
                return true;
            case "current_energy":
                stat = KitLibRunStat.CurrentEnergy;
                return true;
            case "max_energy":
                stat = KitLibRunStat.MaxEnergy;
                return true;
            case "stars":
                stat = KitLibRunStat.Stars;
                return true;
            case "orb_slots":
                stat = KitLibRunStat.OrbSlots;
                return true;
            case "potion_slots":
                stat = KitLibRunStat.PotionSlots;
                return true;
            default:
                stat = KitLibRunStat.Gold;
                error =
                    $"Unknown stat '{raw}'. Use gold, current_hp, max_hp, current_energy, max_energy, " +
                    "stars, orb_slots, or potion_slots.";
                return false;
        }
    }

    public static JsonObject ApplyCheat(string cheat, bool? enabled, float? value) {
        var result = RuntimeCheatBridge.TrySetCheat(new KitLibSetCheatRequest(cheat, enabled, value));
        if (!result.Ok)
            return Fail(result.Error ?? "Set cheat failed.");

        var json = new JsonObject {
            ["ok"] = true,
            ["cheat"] = result.Cheat ?? cheat,
        };
        if (result.Enabled.HasValue)
            json["enabled"] = result.Enabled.Value;
        if (result.Value.HasValue)
            json["value"] = result.Value.Value;
        return json;
    }

    public static JsonObject ApplyStat(KitLibRunStat stat, int value, bool? lockEnabled) {
        var result = RuntimeCheatBridge.TrySetStat(new KitLibSetStatRequest(stat, value, lockEnabled));
        if (!result.Ok)
            return Fail(result.Error ?? "Set stat failed.");

        return new JsonObject {
            ["ok"] = true,
            ["stat"] = result.Stat ?? stat.ToString(),
            ["value"] = result.Value,
            ["locked"] = result.Locked,
        };
    }
}
