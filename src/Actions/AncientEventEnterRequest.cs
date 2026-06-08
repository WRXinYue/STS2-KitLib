namespace KitLib.Actions;

/// <param name="PinOptionToken">
/// Matches <see cref="MegaCrit.Sts2.Core.Events.EventOption.TextKey"/> substring
/// (e.g. <c>ARCHAIC_TOOTH</c>), same as vanilla <c>ancient OROBAS ARCHAIC_TOOTH</c>.
/// </param>
internal readonly record struct AncientEventEnterRequest(string? PinOptionToken = null);
