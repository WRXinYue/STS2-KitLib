using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;

namespace DevMode.Actions;

internal static class AncientEventActions
{
    internal readonly record struct AncientOptionChoice(string Token, string Label);

    internal readonly record struct AncientEnterChoice(
        string Label,
        string? Token,
        AncientEventEnterRequest Request);

    internal static bool IsAncient(EventModel eventModel) => eventModel is AncientEventModel;

    internal static bool NeedsOptionPicker(EventModel eventModel) =>
        eventModel is AncientEventModel;

    internal static IReadOnlyList<AncientOptionChoice> GetOptionChoices(AncientEventModel ancient)
    {
        var choices = new List<AncientOptionChoice>();
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in ancient.AllPossibleOptions)
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
                $"[DevMode] Failed to list ancient options for {((AbstractModel)ancient).Id.Entry}: {ex.Message}");
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

        foreach (var choice in GetOptionChoices(ancient))
            enterChoices.Add(new AncientEnterChoice(
                choice.Label,
                choice.Token,
                new AncientEventEnterRequest(PinOptionToken: choice.Token)));

        return enterChoices;
    }

    internal static bool IsValidChoice(AncientEventModel ancient, string token)
    {
        try
        {
            return ancient.AllPossibleOptions.Any(option =>
                option.TextKey.Contains(token, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    internal static string DescribeEnterChoice(AncientEventEnterRequest request) =>
        request.PinOptionToken
            ?? I18N.T("ancient.options.random", "Random (vanilla)");

    internal static AncientEventEnterRequest? ParseEnterFlags(string[] args, int startIndex)
    {
        string? pin = null;
        for (var i = startIndex; i < args.Length; i++)
            pin = args[i].ToUpperInvariant();

        return pin is null ? null : new AncientEventEnterRequest(PinOptionToken: pin);
    }

    internal static string GetOptionToken(EventOption option)
    {
        var parts = option.TextKey.Split('.');
        return parts.Length > 0 ? parts[^1] : option.TextKey;
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
