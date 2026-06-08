using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Hooks;

namespace KitLib.Scripts;

// ─────────────────────────── Condition tree ───────────────────────────

[JsonConverter(typeof(ConditionNodeConverter))]
public abstract record ConditionNode;

public record AndNode(List<ConditionNode> Children) : ConditionNode;
public record OrNode(List<ConditionNode> Children) : ConditionNode;
public record NotNode(ConditionNode Child) : ConditionNode;
public record LeafCondition(ConditionType Type, string Value) : ConditionNode;
public record VarCompareCondition(string VarName, string Op, int Threshold) : ConditionNode;

// ─────────────────────────── Action tree ───────────────────────────

[JsonConverter(typeof(ActionNodeConverter))]
public abstract record ActionNode;

public record SequenceNode(List<ActionNode> Steps) : ActionNode;
public record IfNode(ConditionNode Condition, ActionNode Then, ActionNode? Else) : ActionNode;
public record ForEachEnemyNode(ActionNode Body) : ActionNode;
public record RepeatNode(int Count, ActionNode Body) : ActionNode;
public record SetVarNode(string VarName, int Value) : ActionNode;
public record IncrVarNode(string VarName, int Delta) : ActionNode;
public record BasicActionNode(ActionType Type, string TargetId, int Amount, HookTargetType Target) : ActionNode;

// ─────────────────────────── Top-level script ───────────────────────────

public sealed class ScriptEntry {
    public string Name { get; set; } = "";
    public TriggerType Trigger { get; set; }
    public ConditionNode? RootCondition { get; set; }
    public ActionNode? RootAction { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>Blockly workspace XML stored for round-trip editing.</summary>
    [JsonPropertyName("_blocklyXml")]
    public string? BlocklyXml { get; set; }
}

// ─────────────────────────── JSON converters ───────────────────────────

public sealed class ConditionNodeConverter : JsonConverter<ConditionNode> {
    public override ConditionNode? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) {
        using var doc = JsonDocument.ParseValue(ref reader);
        return ParseCondition(doc.RootElement);
    }

    internal static ConditionNode? ParseCondition(JsonElement el) {
        if (el.ValueKind == JsonValueKind.Null) return null;

        var type = el.GetProperty("type").GetString() ?? "";
        return type.ToUpperInvariant() switch {
            "AND" => new AndNode(ParseConditionList(el, "children")),
            "OR" => new OrNode(ParseConditionList(el, "children")),
            "NOT" => new NotNode(ParseCondition(el.GetProperty("child"))!),
            "VARCOMPARE" or "VARABOVE" or "VARBELOW" =>
                new VarCompareCondition(
                    el.GetProperty("varName").GetString() ?? "",
                    el.TryGetProperty("op", out var opEl) ? opEl.GetString() ?? ">" : type.ToUpperInvariant() == "VARBELOW" ? "<" : ">",
                    el.TryGetProperty("value", out var valEl) ? ParseInt(valEl) : 0),
            _ => new LeafCondition(
                    System.Enum.TryParse<ConditionType>(type, true, out var ct) ? ct : ConditionType.None,
                    el.TryGetProperty("value", out var v) ? v.GetString() ?? "" : ""),
        };
    }

    private static List<ConditionNode> ParseConditionList(JsonElement el, string prop) {
        var list = new List<ConditionNode>();
        if (!el.TryGetProperty(prop, out var arr)) return list;
        foreach (var child in arr.EnumerateArray()) {
            var node = ParseCondition(child);
            if (node != null) list.Add(node);
        }
        return list;
    }

    private static int ParseInt(JsonElement el) =>
        el.ValueKind == JsonValueKind.Number ? el.GetInt32() : int.TryParse(el.GetString(), out var n) ? n : 0;

    public override void Write(Utf8JsonWriter writer, ConditionNode value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        switch (value) {
            case AndNode and:
                writer.WriteString("type", "AND");
                writer.WritePropertyName("children");
                WriteConditionList(writer, and.Children, options);
                break;
            case OrNode or:
                writer.WriteString("type", "OR");
                writer.WritePropertyName("children");
                WriteConditionList(writer, or.Children, options);
                break;
            case NotNode not:
                writer.WriteString("type", "NOT");
                writer.WritePropertyName("child");
                Write(writer, not.Child, options);
                break;
            case VarCompareCondition vc:
                writer.WriteString("type", "VarCompare");
                writer.WriteString("varName", vc.VarName);
                writer.WriteString("op", vc.Op);
                writer.WriteNumber("value", vc.Threshold);
                break;
            case LeafCondition leaf:
                writer.WriteString("type", leaf.Type.ToString());
                writer.WriteString("value", leaf.Value);
                break;
        }
        writer.WriteEndObject();
    }

    private static void WriteConditionList(Utf8JsonWriter writer, List<ConditionNode> nodes, JsonSerializerOptions options) {
        var converter = new ConditionNodeConverter();
        writer.WriteStartArray();
        foreach (var n in nodes)
            converter.Write(writer, n, options);
        writer.WriteEndArray();
    }
}

public sealed class ActionNodeConverter : JsonConverter<ActionNode> {
    public override ActionNode? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) {
        using var doc = JsonDocument.ParseValue(ref reader);
        return ParseAction(doc.RootElement);
    }

    internal static ActionNode? ParseAction(JsonElement el) {
        if (el.ValueKind == JsonValueKind.Null) return null;

        var type = el.GetProperty("type").GetString() ?? "";
        return type.ToUpperInvariant() switch {
            "SEQUENCE" => new SequenceNode(ParseActionList(el, "steps")),
            "IF" => new IfNode(
                ConditionNodeConverter.ParseCondition(el.GetProperty("condition"))!,
                ParseAction(el.GetProperty("then"))!,
                el.TryGetProperty("else", out var elseEl) ? ParseAction(elseEl) : null),
            "FOREACHENEMY" => new ForEachEnemyNode(ParseAction(el.GetProperty("body"))!),
            "REPEAT" => new RepeatNode(
                el.TryGetProperty("count", out var cntEl) ? ParseInt(cntEl) : 1,
                ParseAction(el.GetProperty("body"))!),
            "SETVAR" => new SetVarNode(
                el.GetProperty("varName").GetString() ?? "",
                el.TryGetProperty("value", out var svEl) ? ParseInt(svEl) : 0),
            "INCRVAR" => new IncrVarNode(
                el.GetProperty("varName").GetString() ?? "",
                el.TryGetProperty("delta", out var dEl) ? ParseInt(dEl) : 1),
            _ => new BasicActionNode(
                System.Enum.TryParse<ActionType>(type, true, out var at) ? at : ActionType.ApplyPower,
                el.TryGetProperty("targetId", out var tid) ? tid.GetString() ?? "" : "",
                el.TryGetProperty("amount", out var amt) ? ParseInt(amt) : 1,
                el.TryGetProperty("target", out var tgt) && System.Enum.TryParse<HookTargetType>(tgt.GetString(), true, out var ht)
                    ? ht : HookTargetType.Player),
        };
    }

    private static List<ActionNode> ParseActionList(JsonElement el, string prop) {
        var list = new List<ActionNode>();
        if (!el.TryGetProperty(prop, out var arr)) return list;
        foreach (var child in arr.EnumerateArray()) {
            var node = ParseAction(child);
            if (node != null) list.Add(node);
        }
        return list;
    }

    private static int ParseInt(JsonElement el) =>
        el.ValueKind == JsonValueKind.Number ? el.GetInt32() : int.TryParse(el.GetString(), out var n) ? n : 0;

    public override void Write(Utf8JsonWriter writer, ActionNode value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        switch (value) {
            case SequenceNode seq:
                writer.WriteString("type", "Sequence");
                writer.WritePropertyName("steps");
                WriteActionList(writer, seq.Steps, options);
                break;
            case IfNode ifn:
                writer.WriteString("type", "If");
                writer.WritePropertyName("condition");
                JsonSerializer.Serialize(writer, ifn.Condition, options);
                writer.WritePropertyName("then");
                Write(writer, ifn.Then, options);
                if (ifn.Else != null) {
                    writer.WritePropertyName("else");
                    Write(writer, ifn.Else, options);
                }
                break;
            case ForEachEnemyNode fe:
                writer.WriteString("type", "ForEachEnemy");
                writer.WritePropertyName("body");
                Write(writer, fe.Body, options);
                break;
            case RepeatNode rp:
                writer.WriteString("type", "Repeat");
                writer.WriteNumber("count", rp.Count);
                writer.WritePropertyName("body");
                Write(writer, rp.Body, options);
                break;
            case SetVarNode sv:
                writer.WriteString("type", "SetVar");
                writer.WriteString("varName", sv.VarName);
                writer.WriteNumber("value", sv.Value);
                break;
            case IncrVarNode iv:
                writer.WriteString("type", "IncrVar");
                writer.WriteString("varName", iv.VarName);
                writer.WriteNumber("delta", iv.Delta);
                break;
            case BasicActionNode ba:
                writer.WriteString("type", ba.Type.ToString());
                writer.WriteString("targetId", ba.TargetId);
                writer.WriteNumber("amount", ba.Amount);
                writer.WriteString("target", ba.Target.ToString());
                break;
        }
        writer.WriteEndObject();
    }

    private static void WriteActionList(Utf8JsonWriter writer, List<ActionNode> nodes, JsonSerializerOptions options) {
        var converter = new ActionNodeConverter();
        writer.WriteStartArray();
        foreach (var n in nodes)
            converter.Write(writer, n, options);
        writer.WriteEndArray();
    }
}
