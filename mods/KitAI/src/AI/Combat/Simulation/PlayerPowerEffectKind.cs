namespace KitLib.AI.Combat.Simulation;

/// <summary>Combat-sim categories for installed player buff powers.</summary>
public enum PlayerPowerEffectKind {
    /// <summary>Flat attack damage (StrengthPower, Inflame, etc.).</summary>
    Strength,
    /// <summary>Flat block (DexterityPower, Footwork, etc.).</summary>
    Dexterity,
    /// <summary>Orb / focus scaling — mapped to attack flat in sim.</summary>
    Focus,
    /// <summary>Inferno: retaliate AOE on player HP loss + turn-start self-damage stack.</summary>
    InfernoRetaliate,
    /// <summary>Crimson Mantle: turn-start self-damage then block.</summary>
    TurnStartBlock,
    /// <summary>Plating / metallicize-style block at end of the player turn.</summary>
    TurnEndBlock,
}
