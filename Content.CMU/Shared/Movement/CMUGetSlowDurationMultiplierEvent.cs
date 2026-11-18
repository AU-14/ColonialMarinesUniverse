namespace Content.Shared.CMU14.Movement;

/// <summary>
/// Raised on an entity about to be slowed (RMC slowdown, superslow, root and temporary speed modifiers) to
/// scale how long the slow lasts. Multiply <see cref="Multiplier"/>; the raiser floors it at zero.
/// Separate from <c>GetChemicalStunTimeMultiplierEvent</c>, which stuns also read.
/// </summary>
[ByRefEvent]
public record struct CMUGetSlowDurationMultiplierEvent
{
    public float Multiplier = 1f;

    public CMUGetSlowDurationMultiplierEvent()
    {
    }
}
