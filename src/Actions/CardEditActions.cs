using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitLib.Presets;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Actions;

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
        };

        var vars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in GetDynamicVarKeys(card)) {
            var v = GetDynamicVar(card, key);
            if (v.HasValue) vars[key] = v.Value;
        }
        if (vars.Count > 0) template.DynamicVars = vars;
        CaptureEnchantmentTemplate(card, template);
        return template;
    }

    private static void CaptureEnchantmentTemplate(CardModel card, CardEditTemplate template) {
        try {
            var enchantment = card.Enchantment;
            if (enchantment == null) return;
            template.EnchantmentTypeName = enchantment.GetType().FullName;
            template.EnchantmentAmount = Math.Max(1, (int)Math.Round((double)enchantment.Amount));
            template.ClearEnchantment = false;
        }
        catch { /* ignore */ }
    }

    public static void ApplyTemplate(CardModel card, CardEditTemplate template, bool forceEnchantment = false) {
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
        ApplyEnchantmentTemplate(card, template, forceEnchantment);
    }

    private static void ApplyEnchantmentTemplate(CardModel card, CardEditTemplate template, bool force) {
        if (template.ClearEnchantment == true) {
            TryClearEnchantment(card, out _);
            return;
        }
        if (string.IsNullOrWhiteSpace(template.EnchantmentTypeName)) return;
        if (!TryResolveEnchantmentType(template.EnchantmentTypeName, out var enchantmentType)) return;
        var amount = Math.Clamp(template.EnchantmentAmount.GetValueOrDefault(1), 1, 999);
        TryApplyEnchantment(card, enchantmentType, amount, force, out _);
    }

    public readonly record struct EnchantmentEntry(string TypeFullName, string DisplayName);

    /// <summary>Vanilla enchantments from <c>MegaCrit.Sts2.Core.Models.Enchantments</c>.</summary>
    public static IReadOnlyList<EnchantmentEntry> GetEnchantmentEntries() {
        try {
            var baseType = typeof(EnchantmentModel);
            return baseType.Assembly
                .GetTypes()
                .Where(t =>
                    t.IsClass &&
                    !t.IsAbstract &&
                    baseType.IsAssignableFrom(t) &&
                    string.Equals(t.Namespace, "MegaCrit.Sts2.Core.Models.Enchantments", StringComparison.Ordinal) &&
                    !t.Name.Contains("Deprecated", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(t.FullName))
                .Select(t => CreateEnchantmentEntry(t))
                .Where(e => e.HasValue)
                .Select(e => e!.Value)
                .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch {
            return Array.Empty<EnchantmentEntry>();
        }
    }

    private static EnchantmentEntry? CreateEnchantmentEntry(Type enchantmentType) {
        if (string.IsNullOrWhiteSpace(enchantmentType.FullName)) return null;
        return new EnchantmentEntry(enchantmentType.FullName, GetEnchantmentDisplayName(enchantmentType));
    }

    public static Texture2D? GetEnchantmentIcon(string? typeFullName) {
        if (string.IsNullOrWhiteSpace(typeFullName)) return null;
        if (!TryResolveEnchantmentType(typeFullName, out var enchantmentType)) return null;
        if (!TryGetCanonicalEnchantmentModel(enchantmentType, out var model) || model == null) return null;
        try { return model.Icon; } catch { return null; }
    }

    /// <summary>Legacy helper — returns resolved enchantment types.</summary>
    public static IReadOnlyList<Type> GetEnchantmentTypes() {
        var entries = GetEnchantmentEntries();
        var types = new List<Type>(entries.Count);
        foreach (var entry in entries) {
            if (TryResolveEnchantmentType(entry.TypeFullName, out var type))
                types.Add(type);
        }
        return types;
    }

    public static string GetCardEnchantmentDisplayName(CardModel card) {
        try {
            var enchantment = card.Enchantment;
            if (enchantment == null)
                return I18N.T("cardEdit.noEnchant", "None");
            var title = FormatLocString(enchantment.Title);
            if (!string.IsNullOrWhiteSpace(title)) return title;
            if (!string.IsNullOrWhiteSpace(enchantment.Id.Entry))
                return enchantment.Id.Entry;
            return GetEnchantmentDisplayName(enchantment.GetType());
        }
        catch {
            return "?";
        }
    }

    public static int GetCardEnchantmentAmount(CardModel card) {
        try {
            return card.Enchantment == null ? 1 : Math.Max(1, (int)Math.Round((double)card.Enchantment.Amount));
        }
        catch {
            return 1;
        }
    }

    public static string GetEnchantmentDisplayName(Type enchantmentType) {
        if (TryGetCanonicalEnchantmentModel(enchantmentType, out var canonical) && canonical != null) {
            var title = FormatLocString(canonical.Title);
            if (!string.IsNullOrWhiteSpace(title)) return title;
            if (!string.IsNullOrWhiteSpace(canonical.Id.Entry))
                return canonical.Id.Entry;
        }
        return enchantmentType.Name;
    }

    public static string FormatLocString(LocString? value) {
        if (value == null) return "";
        try {
            if (value.IsEmpty) return "";
            var formatted = value.GetFormattedText();
            if (string.IsNullOrWhiteSpace(formatted)) return "";
            return formatted.StripBbCode().Trim();
        }
        catch {
            return "";
        }
    }

    public static bool TryApplyEnchantment(CardModel card, Type enchantmentType, bool force = false) =>
        TryApplyEnchantment(card, enchantmentType, 1, force, out _);

    public static bool TryApplyEnchantment(
        CardModel card,
        Type enchantmentType,
        int amount,
        bool forceWhenIncompatible,
        out string error) {
        error = "";
        var clampedAmount = Math.Clamp(amount, 1, 999);
        try {
            if (!typeof(EnchantmentModel).IsAssignableFrom(enchantmentType)) {
                error = $"Invalid enchantment type: {enchantmentType.FullName}";
                return false;
            }
            if (!TryGetCanonicalEnchantmentModel(enchantmentType, out var canonical) || canonical == null) {
                error = $"Enchantment model not found: {enchantmentType.Name}";
                return false;
            }

            var enchantmentModel = canonical.ToMutable();
            if (TryApplyEnchantmentByCardCommand(card, enchantmentType, enchantmentModel, clampedAmount, out var commandError))
                return true;

            if (forceWhenIncompatible && IsEnchantmentIncompatibleError(commandError)) {
                if (TryForceApplyEnchantment(card, enchantmentModel, clampedAmount, out var forceError))
                    return true;
                error = forceError;
                return false;
            }

            error = FormatEnchantmentApplyError(commandError, card, enchantmentModel);
            return false;
        }
        catch (Exception ex) {
            error = GetInnermostErrorMessage(ex);
            MainFile.Logger.Warn($"Apply enchantment failed: {error}");
            return false;
        }
    }

    public static bool TryClearEnchantment(CardModel card) => TryClearEnchantment(card, out _);

    public static bool TryClearEnchantment(CardModel card, out string error) {
        error = "";
        try {
            CardCmd.ClearEnchantment(card);
            return true;
        }
        catch (Exception ex) {
            error = GetInnermostErrorMessage(ex);
            MainFile.Logger.Warn($"Clear enchantment failed: {error}");
            return false;
        }
    }

    public static bool TryResolveEnchantmentType(string typeName, out Type enchantmentType) {
        enchantmentType = null!;
        if (string.IsNullOrWhiteSpace(typeName)) return false;

        var resolved = Type.GetType(typeName, throwOnError: false);
        if (resolved != null && typeof(EnchantmentModel).IsAssignableFrom(resolved)) {
            enchantmentType = resolved;
            return true;
        }

        resolved = typeof(EnchantmentModel).Assembly
            .GetTypes()
            .FirstOrDefault(type =>
                typeof(EnchantmentModel).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                (string.Equals(type.FullName, typeName, StringComparison.Ordinal) ||
                 string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase)));
        if (resolved == null) return false;
        enchantmentType = resolved;
        return true;
    }

    private static bool TryGetCanonicalEnchantmentModel(Type enchantmentType, out EnchantmentModel? model) {
        model = null;
        try {
            var id = ModelDb.GetId(enchantmentType);
            model = ModelDb.GetByIdOrNull<EnchantmentModel>(id);
            if (model != null) return true;
        }
        catch { /* fall through */ }

        try {
            var generic = typeof(ModelDb)
                .GetMethods(ReflFlags | BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(method =>
                    method.Name == "Enchantment" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 0);
            if (generic == null) return false;
            var value = generic.MakeGenericMethod(enchantmentType).Invoke(null, null);
            if (value is EnchantmentModel enchantmentModel) {
                model = enchantmentModel;
                return true;
            }
        }
        catch { /* ignore */ }
        return false;
    }

    private static bool TryApplyEnchantmentByCardCommand(
        CardModel card,
        Type enchantmentType,
        EnchantmentModel enchantmentModel,
        int amount,
        out string error) {
        error = "";
        try {
            CardCmd.Enchant(enchantmentModel, card, amount);
            return true;
        }
        catch (Exception ex) {
            error = GetInnermostErrorMessage(ex);
        }

        if (TryApplyEnchantmentByGenericTypeCommand(card, enchantmentType, amount, out var genericError))
            return true;

        if (string.IsNullOrWhiteSpace(error))
            error = genericError;
        return false;
    }

    private static bool TryApplyEnchantmentByGenericTypeCommand(CardModel card, Type enchantmentType, int amount, out string error) {
        error = "";
        try {
            var genericEnchant = typeof(CardCmd)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                    method.Name == "Enchant" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters() is { Length: 2 } parameters &&
                    parameters[0].ParameterType == typeof(CardModel) &&
                    parameters[1].ParameterType == typeof(decimal));
            if (genericEnchant == null) return false;

            genericEnchant.MakeGenericMethod(enchantmentType).Invoke(null, new object?[] { card, (decimal)amount });
            return true;
        }
        catch (Exception ex) {
            error = GetInnermostErrorMessage(ex);
            return false;
        }
    }

    private static bool TryForceApplyEnchantment(CardModel card, EnchantmentModel enchantmentModel, int amount, out string error) {
        error = "";
        try {
            var mutable = enchantmentModel.IsMutable ? enchantmentModel : enchantmentModel.ToMutable();
            var clampedAmount = Math.Clamp(amount, 1, 999);
            mutable.Status = EnchantmentStatus.Normal;

            var current = card.Enchantment;
            if (current == null) {
                card.EnchantInternal(mutable, clampedAmount);
                mutable.ModifyCard();
            }
            else if (current.GetType() == mutable.GetType()) {
                current.Amount += clampedAmount;
            }
            else {
                card.ClearEnchantmentInternal();
                card.EnchantInternal(mutable, clampedAmount);
                mutable.ModifyCard();
            }

            card.FinalizeUpgradeInternal();
            return true;
        }
        catch (Exception ex) {
            error = GetInnermostErrorMessage(ex);
            return false;
        }
    }

    private static bool IsEnchantmentIncompatibleError(string? error) {
        if (string.IsNullOrWhiteSpace(error)) return false;
        return error.Contains("Cannot enchant", StringComparison.OrdinalIgnoreCase)
            || error.Contains("not compatible", StringComparison.OrdinalIgnoreCase)
            || error.Contains("incompatible", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatEnchantmentApplyError(string? rawError, CardModel card, EnchantmentModel enchantmentModel) {
        if (IsEnchantmentIncompatibleError(rawError)) {
            var cardId = ((AbstractModel)card).Id.Entry ?? card.Id.Entry;
            var enchantmentId = enchantmentModel.Id.Entry;
            if (string.IsNullOrWhiteSpace(enchantmentId))
                enchantmentId = enchantmentModel.GetType().Name;
            return string.Format(
                I18N.T("cardEdit.enchantIncompatible", "Enchantment incompatible with this card ({0} x {1})."),
                cardId,
                enchantmentId);
        }
        return string.IsNullOrWhiteSpace(rawError)
            ? I18N.T("cardEdit.enchantFailed", "Failed to apply enchantment.")
            : rawError;
    }

    private static string GetInnermostErrorMessage(Exception ex) {
        while (ex.InnerException != null)
            ex = ex.InnerException;
        return ex.Message;
    }

    /// <summary>Get all cards in the player's deck for editing.</summary>
    public static IReadOnlyList<CardModel> GetDeckCards(Player player) {
        return player.Deck?.Cards?.ToArray() ?? Array.Empty<CardModel>();
    }

    public static string GetCardDisplayName(CardModel card) {
        try {
            var title = card.Title;
            if (!string.IsNullOrWhiteSpace(title)) return title;
        }
        catch { }
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
