using System;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace DevMode.Actions;

internal static class AncientEventActions
{
    internal const string DarvId = "DARV";

    internal static bool IsDarv(EventModel eventModel) =>
        eventModel is Darv
        || string.Equals(((AbstractModel)eventModel).Id.Entry, DarvId, StringComparison.OrdinalIgnoreCase);

    internal static AncientEventEnterRequest? ParseEnterFlags(string[] args, int startIndex)
    {
        bool? darvBranch = null;

        for (var i = startIndex; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("+dusty", StringComparison.OrdinalIgnoreCase))
                darvBranch = true;
            else if (arg.Equals("-dusty", StringComparison.OrdinalIgnoreCase))
                darvBranch = false;
        }

        if (darvBranch is null)
            return null;

        return new AncientEventEnterRequest(darvBranch);
    }
}
