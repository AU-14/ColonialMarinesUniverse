namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// FLOW-23: a tile fire fed by Firepenetrating fuel of level 3 or higher. Once per <see cref="Interval"/> the
/// server-side <c>CMUStructureMeltingFireSystem</c> deals Heat damage to the damageable anchored structures
/// (walls, barricades, doors, other structures) on the fire's tile:
/// <c>Intensity * (Level - 2) * DamageMultiplier</c>, with the target's resistances applied.
/// Added with <c>EnsureComp</c> by the flamer fire spec when the blend's penetration level is 3 or more.
/// </summary>
[RegisterComponent]
public sealed partial class CMUStructureMeltingFireComponent : Component
{
    /// <summary>Firepenetrating level of the fuel. Below 3 nothing melts.</summary>
    [DataField]
    public int Level;

    /// <summary>Fire intensity used for the melt damage.</summary>
    [DataField]
    public int Intensity;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>Heat damage per point of intensity per level above 2, per tick.</summary>
    [DataField]
    public float DamageMultiplier = 1f;

    public TimeSpan NextTick;
}

/// <summary>
/// Raised by-ref on a structure before a melting fire damages it. Set <see cref="Cancelled"/> to spare it.
/// </summary>
[ByRefEvent]
public record struct CMUFireMeltAttemptEvent(EntityUid Fire, int Level, bool Cancelled = false);
