namespace KitLib.AI.Combat.Simulation;

public sealed record PlayerPowerInstall(
    PlayerPowerEffectKind Kind,
    int Amount,
    int SelfDamageIncrementOnPlay = 0);
