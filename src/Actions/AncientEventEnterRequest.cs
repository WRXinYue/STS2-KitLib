namespace DevMode.Actions;

/// <param name="DarvIncludeDustyTome">
/// <c>true</c> = 2 boss relics + dusty tome; <c>false</c> = 3 boss relics; <c>null</c> = vanilla RNG.
/// </param>
internal readonly record struct AncientEventEnterRequest(bool? DarvIncludeDustyTome = null);
