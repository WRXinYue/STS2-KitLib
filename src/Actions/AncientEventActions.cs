using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Actions;

internal static class AncientEventActions
{
    private static readonly PropertyInfo OwnerProperty =
        AccessTools.Property(typeof(EventModel), nameof(EventModel.Owner))
        ?? throw new InvalidOperationException("EventModel.Owner property not found.");

    internal readonly record struct AncientOptionChoice(string Token, string Label);

    internal readonly record struct AncientEnterChoice(
        string Label,
        string? Token,
        AncientEventEnterRequest Request);

    internal static bool NeedsOptionPicker(EventModel eventModel) =>
        eventModel is AncientEventModel;

    internal static IReadOnlyList<AncientOptionChoice> GetOptionChoices(AncientEventModel ancient, Player? player)
    {
        if (player is null)
            return [];

        var choices = new List<AncientOptionChoice>();
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in PlayerScopedOptions(ancient, player))
            {
                if (option.IsLocked)
                    continue;

                var token = GetOptionToken(option);
                if (string.IsNullOrWhiteSpace(token) || !seen.Add(token))
                    continue;

                choices.Add(new AncientOptionChoice(token, FormatOptionLabel(option, token)));
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn(
                $"[KitLib] Failed to list ancient options for {((AbstractModel)ancient).Id.Entry}: {ex.Message}");
        }

        return choices;
    }

    internal static IReadOnlyList<AncientEnterChoice> GetEnterChoices(EventModel eventModel)
    {
        if (eventModel is not AncientEventModel ancient)
            return [];

        var enterChoices = new List<AncientEnterChoice>
        {
            new(
                I18N.T("ancient.options.random", "Random (vanilla)"),
                null,
                new AncientEventEnterRequest()),
        };

        if (!RunContext.TryGetRunAndPlayer(out _, out var player))
            return enterChoices;

        foreach (var choice in GetOptionChoices(ancient, player))
            enterChoices.Add(new AncientEnterChoice(
                choice.Label,
                choice.Token,
                new AncientEventEnterRequest(PinOptionToken: choice.Token)));

        return enterChoices;
    }

    internal static bool IsValidChoice(AncientEventModel ancient, string token, Player player) =>
        GetOptionChoices(ancient, player).Any(choice =>
            choice.Token.Equals(token, StringComparison.OrdinalIgnoreCase));

    internal static string DescribeEnterChoice(AncientEventEnterRequest request) =>
        request.PinOptionToken
            ?? I18N.T("ancient.options.random", "Random (vanilla)");

    internal static AncientEventEnterRequest? ParseEnterFlags(string[] args, int startIndex) =>
        startIndex < args.Length
            ? new AncientEventEnterRequest(PinOptionToken: args[startIndex].ToUpperInvariant())
            : null;

    internal static string GetOptionToken(EventOption option)
    {
        var parts = option.TextKey.Split('.');
        return parts.Length > 0 ? parts[^1] : option.TextKey;
    }

    private static IEnumerable<EventOption> PlayerScopedOptions(AncientEventModel ancient, Player player)
    {
        var mutable = (AncientEventModel)(EventModel)ancient.ToMutable();
        OwnerProperty.SetValue(mutable, player);
        return mutable.AllPossibleOptions;
    }

    private static string FormatOptionLabel(EventOption option, string token)
    {
        try
        {
            var title = option.Title?.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        catch { /* fall through */ }

        return token;
    }
}
