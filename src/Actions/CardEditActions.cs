using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DevMode.Presets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace DevMode.Actions;

/// <summary>
/// Dev panel card editing: cost (official <see cref="CardModel.EnergyCost"/> API), replay, damage, block,
/// keywords, enchantments. Cost uses <see cref="TrySetBaseCost"/> / <see cref="GetBaseCost"/>; other fields
/// still use reflection where the game has no stable public surface.
/// </summary>
internal static class CardEditActions {
    private const BindingFlags ReflFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // ── Cost: mutable instances — SetCustomBaseCost (requires AssertMutable). Canonical library templates
    // cannot be edited in-place; use <see cref="CardActions.AddCardBuilder.BaseCost"/> when adding a new instance.

    public static bool TrySetBaseCost(CardModel card, int cost) {
        try {
            card.EnergyCost.SetCustomBaseCost(cost);
            return true;
        }
        catch {
            return false;
        }
    }

    public static int? GetBaseCost(CardModel card) {
        try {
            return card.EnergyCost.GetWithModifiers(CostModifiers.None);
        }
        catch {
            return null;
        }
    }

    // ── Replay: STS2 uses BaseReplayCount ──

    public static bool TrySetReplayCount(CardModel card, int count) {
        try { card.BaseReplayCount = count; return true; } catch { }
        return TrySetProperty(card, "ReplayCount", count)
            || TrySetProperty(card, "Replay", count)
            || TrySetField(card, "_replayCount", count);
    }

    public static int? GetReplayCount(CardModel card) {
        try { return card.BaseReplayCount; } catch { }
        return TryGetInt(card, "ReplayCount") ?? TryGetInt(card, "Replay");
    }

    // ── Damage / Block: STS2 stores these as DynamicVars, not CardModel properties ──

    public static bool TrySetDamage(CardModel card, int damage) {
        try {
            if (card.DynamicVars.TryGetValue("Damage", out var dv)) {
                dv.BaseValue = damage;
                dv.PreviewValue = damage;
                return true;
            }
        }
        catch { }
        return TrySetProperty(card, "BaseDamage", damage)
            || TrySetProperty(card, "Damage", damage)
            || TrySetField(card, "_baseDamage", damage);
    }

    public static int? GetDamage(CardModel card) {
        try {
            if (card.DynamicVars.TryGetValue("Damage", out var dv))
                return (int)Math.Round(dv.BaseValue);
        }
        catch { }
        return TryGetInt(card, "BaseDamage") ?? TryGetInt(card, "Damage");
    }

    public static bool TrySetBlock(CardModel card, int block) {
        try {
            if (card.DynamicVars.TryGetValue("Block", out var dv)) {
                dv.BaseValue = block;
                dv.PreviewValue = block;
                return true;
            }
        }
        catch { }
        return TrySetProperty(card, "BaseBlock", block)
            || TrySetProperty(card, "Block", block)
            || TrySetField(card, "_baseBlock", block);
    }

    public static int? GetBlock(CardModel card) {
        try {
            if (card.DynamicVars.TryGetValue("Block", out var dv))
                return (int)Math.Round(dv.BaseValue);
        }
        catch { }
        return TryGetInt(card, "BaseBlock") ?? TryGetInt(card, "Block");
    }

    // ── Keywords: STS2 uses Keywords set + AddKeyword/RemoveKeyword ──

    public static bool? GetExhaust(CardModel card) {
        try { return card.Keywords.Contains(CardKeyword.Exhaust); } catch { }
        return TryGetBool(card, "Exhaust");
    }

    public static bool? GetEthereal(CardModel card) {
        try { return card.Keywords.Contains(CardKeyword.Ethereal); } catch { }
        return TryGetBool(card, "Ethereal");
    }

    public static bool? GetUnplayable(CardModel card) {
        try { return card.Keywords.Contains(CardKeyword.Unplayable); } catch { }
        return TryGetBool(card, "Unplayable");
    }

    public static bool TrySetExhaust(CardModel card, bool exhaust) {
        try {
            if (exhaust) card.AddKeyword(CardKeyword.Exhaust);
            else card.RemoveKeyword(CardKeyword.Exhaust);
            return true;
        }
        catch { }
        return TrySetProperty(card, "Exhaust", exhaust)
            || TrySetField(card, "_exhaust", exhaust);
    }

    public static bool TrySetEthereal(CardModel card, bool ethereal) {
        try {
            if (ethereal) card.AddKeyword(CardKeyword.Ethereal);
            else card.RemoveKeyword(CardKeyword.Ethereal);
            return true;
        }
        catch { }
        return TrySetProperty(card, "Ethereal", ethereal)
            || TrySetField(card, "_ethereal", ethereal);
    }

    public static bool TrySetUnplayable(CardModel card, bool unplayable) {
        try {
            if (unplayable) card.AddKeyword(CardKeyword.Unplayable);
            else card.RemoveKeyword(CardKeyword.Unplayable);
            return true;
        }
        catch { }
        return TrySetProperty(card, "Unplayable", unplayable)
            || TrySetField(card, "_unplayable", unplayable);
    }

    // ── Other card flags ──

    public static bool? GetExhaustOnNextPlay(CardModel card) => TryGetBool(card, "ExhaustOnNextPlay", "_exhaustOnNextPlay");
    public static bool? GetSingleTurnRetain(CardModel card) => TryGetBool(card, "HasSingleTurnRetain", "_hasSingleTurnRetain");
    public static bool? GetSingleTurnSly(CardModel card) => TryGetBool(card, "HasSingleTurnSly", "_hasSingleTurnSly");

    public static bool TrySetExhaustOnNextPlay(CardModel card, bool enabled) => TrySetBool(card, enabled, "ExhaustOnNextPlay", "_exhaustOnNextPlay");
    public static bool TrySetSingleTurnRetain(CardModel card, bool enabled) => TrySetBool(card, enabled, "HasSingleTurnRetain", "_hasSingleTurnRetain");
    public static bool TrySetSingleTurnSly(CardModel card, bool enabled) => TrySetBool(card, enabled, "HasSingleTurnSly", "_hasSingleTurnSly");

    private static readonly HashSet<string> _builtInVarKeys = new(StringComparer.OrdinalIgnoreCase) { "Damage", "Block" };

    public static IReadOnlyList<string> GetDynamicVarKeys(CardModel card) {
        try {
            return card.DynamicVars?.Keys?
                .Where(k => !_builtInVarKeys.Contains(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();
        }
        catch {
            return Array.Empty<string>();
        }
    }

    public static string GetDynamicVarDisplayName(string key) {
        return I18N.T($"cardEdit.dynVar.{key}", key);
    }

    public static int? GetDynamicVar(CardModel card, string key) {
        if (string.IsNullOrWhiteSpace(key)) return null;
        try {
            if (TryGetDynamicVar(card, key, out var dynamicVar) && dynamicVar != null)
                return (int)Math.Round(dynamicVar.BaseValue);
        }
        catch { }
        return null;
    }

    public static bool TrySetDynamicVar(CardModel card, string key, int value) {
        if (string.IsNullOrWhiteSpace(key)) return false;
        try {
            if (!TryGetDynamicVar(card, key, out var dynamicVar) || dynamicVar == null) return false;
            dynamicVar.BaseValue = value;
            dynamicVar.PreviewValue = value;
            dynamicVar.ResetToBase();
            dynamicVar.PreviewValue = value;
            return true;
        }
        catch { return false; }
    }

    public static string GetTitleText(CardModel card) {
        object? value = TryGetObject(card, "TitleLocString", "_titleLocString");
        if (value is LocString loc) {
            var text = GetLocText(loc);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return GetTitlePropertyText(card);
    }

    private static string GetTitlePropertyText(CardModel card) {
        try {
            return card.Title ?? string.Empty;
        }
        catch {
            return string.Empty;
        }
    }

    public static string GetDescriptionText(CardModel card) {
        object? value = TryGetObject(card, "Description", "_descriptionLocString", "_description");
        return value switch {
            LocString loc => GetLocText(loc),
            string s => s,
            _ => string.Empty
        };
    }

    public static bool TrySetTitleText(CardModel card, string text) {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var loc = CreateOverrideLocString(text.Trim());
        if (loc == null) return false;
        var trimmed = text.Trim();
        var ok = TrySetObject(card, loc, "TitleLocString", "_titleLocString");
        ok |= TrySetProperty(card, "Title", trimmed);
        return ok;
    }

    public static bool TrySetDescriptionText(CardModel card, string text) {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var loc = CreateOverrideLocString(text.Trim());
        if (loc == null) return false;
        var ok = TrySetObject(card, loc, "Description", "_descriptionLocString", "_description");
        return ok;
    }

    /// <summary>LocString the game can render without a loc table entry (raw override text).</summary>
    private static LocString? CreateOverrideLocString(string text) {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try {
            var loc = new LocString("devmode", "card_override");
            if (TrySetLocRawText(loc, text)) return loc;
        }
        catch { }
        try {
            var loc = new LocString("!", text);
            if (TrySetLocRawText(loc, text)) return loc;
        }
        catch { }
        return null;
    }

    private static bool TrySetLocRawText(LocString loc, string text) {
        foreach (var name in new[] { "RawText", "rawText", "_rawText", "_overrideText", "_text" }) {
            if (TrySetProperty(loc, name, text) || TrySetField(loc, name, text))
                return true;
        }
        try {
            var getRaw = loc.GetType().GetMethod("GetRawText", ReflFlags);
            var setRaw = loc.GetType().GetMethod("SetRawText", ReflFlags)
                ?? loc.GetType().GetMethod("SetRaw", ReflFlags);
            if (setRaw != null) {
                setRaw.Invoke(loc, new object[] { text });
                return true;
            }
        }
        catch { }
        return !string.IsNullOrWhiteSpace(GetLocText(loc));
    }

    public static CardEditTemplate CaptureTemplate(CardModel card) {
        var template = new CardEditTemplate {
            BaseCost = GetBaseCost(card),
            ReplayCount = GetReplayCount(card),
            Damage = GetDamage(card),
            Block = GetBlock(card),
            Exhaust = GetExhaust(card),
            Ethereal = GetEthereal(card),
            Unplayable = GetUnplayable(card),
            ExhaustOnNextPlay = GetExhaustOnNextPlay(card),
            SingleTurnRetain = GetSingleTurnRetain(card),
            SingleTurnSly = GetSingleTurnSly(card),
            NameOverride = GetTitleText(card),
            DescriptionOverride = GetDescriptionText(card)
        };

        var vars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in GetDynamicVarKeys(card)) {
            var v = GetDynamicVar(card, key);
            if (v.HasValue) vars[key] = v.Value;
        }
        if (vars.Count > 0) template.DynamicVars = vars;
        return template;
    }

    public static void ApplyTemplate(CardModel card, CardEditTemplate template) {
        if (template.BaseCost.HasValue) TrySetBaseCost(card, template.BaseCost.Value);
        if (template.ReplayCount.HasValue) TrySetReplayCount(card, template.ReplayCount.Value);
        if (template.Damage.HasValue) TrySetDamage(card, template.Damage.Value);
        if (template.Block.HasValue) TrySetBlock(card, template.Block.Value);

        if (template.DynamicVars != null) {
            foreach (var kv in template.DynamicVars)
                TrySetDynamicVar(card, kv.Key, kv.Value);
        }

        if (template.Exhaust.HasValue) TrySetExhaust(card, template.Exhaust.Value);
        if (template.Ethereal.HasValue) TrySetEthereal(card, template.Ethereal.Value);
        if (template.Unplayable.HasValue) TrySetUnplayable(card, template.Unplayable.Value);
        if (template.ExhaustOnNextPlay.HasValue) TrySetExhaustOnNextPlay(card, template.ExhaustOnNextPlay.Value);
        if (template.SingleTurnRetain.HasValue) TrySetSingleTurnRetain(card, template.SingleTurnRetain.Value);
        if (template.SingleTurnSly.HasValue) TrySetSingleTurnSly(card, template.SingleTurnSly.Value);

        if (!string.IsNullOrWhiteSpace(template.NameOverride)) TrySetTitleText(card, template.NameOverride);
        if (!string.IsNullOrWhiteSpace(template.DescriptionOverride)) TrySetDescriptionText(card, template.DescriptionOverride);
    }

    /// <summary>Get all enchantment types available in the game.</summary>
    public static IReadOnlyList<Type> GetEnchantmentTypes() {
        try {
            var baseType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "AbstractEnchantment" && !t.IsInterface);

            if (baseType == null) return Array.Empty<Type>();

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.Name)
                .ToArray();
        }
        catch { return Array.Empty<Type>(); }
    }

    /// <summary>Try to apply an enchantment to a card.</summary>
    public static bool TryApplyEnchantment(CardModel card, Type enchantmentType, bool force = false) {
        try {
            var enchantment = Activator.CreateInstance(enchantmentType);
            if (enchantment == null) return false;

            // Try CardCmd.Enchant or similar
            var enchantMethod = typeof(CardCmd).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m => m.Name.Contains("Enchant", StringComparison.OrdinalIgnoreCase));

            if (enchantMethod != null) {
                var parameters = enchantMethod.GetParameters();
                var args = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; i++) {
                    var pt = parameters[i].ParameterType;
                    if (pt == typeof(CardModel) || typeof(CardModel).IsAssignableFrom(pt))
                        args[i] = card;
                    else if (enchantmentType.IsAssignableFrom(pt) || pt.IsAssignableFrom(enchantmentType))
                        args[i] = enchantment;
                    else if (pt == typeof(bool))
                        args[i] = force;
                    else if (parameters[i].HasDefaultValue)
                        args[i] = parameters[i].DefaultValue;
                    else
                        args[i] = null;
                }
                enchantMethod.Invoke(null, args);
                return true;
            }

            // Fallback: set Enchantment property directly
            return TrySetProperty(card, "Enchantment", enchantment)
                || TrySetProperty(card, "CurrentEnchantment", enchantment);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"Apply enchantment failed: {ex.Message}");
            return false;
        }
    }

    public static bool TryClearEnchantment(CardModel card) {
        return TrySetProperty(card, "Enchantment", null)
            || TrySetProperty(card, "CurrentEnchantment", null);
    }

    /// <summary>Get all cards in the player's deck for editing.</summary>
    public static IReadOnlyList<CardModel> GetDeckCards(Player player) {
        return player.Deck?.Cards?.ToArray() ?? Array.Empty<CardModel>();
    }

    public static string GetCardDisplayName(CardModel card) {
        var overrideTitle = GetTitleText(card);
        if (!string.IsNullOrWhiteSpace(overrideTitle)) return overrideTitle;
        var fromTitle = GetTitlePropertyText(card);
        if (!string.IsNullOrWhiteSpace(fromTitle)) return fromTitle;
        try { return ((AbstractModel)card).Id.Entry ?? "?"; }
        catch { return "?"; }
    }

    // ── Reflection helpers ──

    private static bool TrySetProperty(object target, string name, object? value) {
        var prop = target.GetType().GetProperty(name, ReflFlags);
        if (prop is not { CanWrite: true }) return false;
        try { prop.SetValue(target, value); return true; }
        catch { return false; }
    }

    private static bool TrySetField(object target, string name, object? value) {
        var field = target.GetType().GetField(name, ReflFlags);
        if (field == null || field.IsInitOnly) return false;
        try { field.SetValue(target, value); return true; }
        catch { return false; }
    }

    private static int? TryGetInt(object target, string name) {
        try {
            var prop = target.GetType().GetProperty(name, ReflFlags);
            if (prop != null) return Convert.ToInt32(prop.GetValue(target));
            var field = target.GetType().GetField(name, ReflFlags);
            if (field != null) return Convert.ToInt32(field.GetValue(target));
        }
        catch { }
        return null;
    }

    private static bool? TryGetBool(object target, string name) {
        try {
            var prop = target.GetType().GetProperty(name, ReflFlags);
            if (prop != null) return (bool?)prop.GetValue(target);
            var field = target.GetType().GetField(name, ReflFlags);
            if (field != null) return (bool?)field.GetValue(target);
        }
        catch { }
        return null;
    }

    private static bool? TryGetBool(object target, params string[] names) {
        foreach (var name in names) {
            var v = TryGetBool(target, name);
            if (v.HasValue) return v;
        }
        return null;
    }

    private static bool TrySetBool(object target, bool value, params string[] names) {
        foreach (var name in names) {
            if (TrySetProperty(target, name, value) || TrySetField(target, name, value))
                return true;
        }
        return false;
    }

    private static object? TryGetObject(object target, params string[] names) {
        foreach (var name in names) {
            try {
                var prop = target.GetType().GetProperty(name, ReflFlags);
                if (prop != null && prop.GetIndexParameters().Length == 0) return prop.GetValue(target);
                var field = target.GetType().GetField(name, ReflFlags);
                if (field != null) return field.GetValue(target);
            }
            catch { }
        }
        return null;
    }

    private static bool TrySetObject(object target, object value, params string[] names) {
        foreach (var name in names) {
            try {
                var prop = target.GetType().GetProperty(name, ReflFlags);
                if (prop != null && prop.CanWrite && prop.PropertyType.IsInstanceOfType(value)) {
                    prop.SetValue(target, value);
                    return true;
                }
                var field = target.GetType().GetField(name, ReflFlags);
                if (field != null && !field.IsInitOnly && field.FieldType.IsInstanceOfType(value)) {
                    field.SetValue(target, value);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    private static string GetLocText(LocString? value) {
        if (value == null || value.IsEmpty) return string.Empty;
        try {
            // DevMode UI frequently reads card text outside full gameplay context.
            // Prefer raw text to avoid triggering localization formatter errors when
            // dynamic placeholders (e.g. {StaminaDamage:diff()}) lack variables.
            var raw = value.GetRawText();
            if (!string.IsNullOrWhiteSpace(raw)) return raw.Trim();
        }
        catch { }
        try {
            var text = value.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }
        catch { }
        return value.LocEntryKey ?? string.Empty;
    }

    private static bool TryGetDynamicVar(CardModel card, string key, out MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar? dynamicVar) {
        dynamicVar = null;
        try {
            if (card.DynamicVars == null) return false;
            if (card.DynamicVars.TryGetValue(key, out dynamicVar) && dynamicVar != null) return true;
            var actualKey = card.DynamicVars.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (actualKey == null) return false;
            return card.DynamicVars.TryGetValue(actualKey, out dynamicVar) && dynamicVar != null;
        }
        catch { return false; }
    }
}
