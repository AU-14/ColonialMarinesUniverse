namespace Content.Shared.CMU14.Combat.Drown;

/// <summary>
/// A body that can Drown (the <c>EWJoeDrownActionEvent</c> action): an aggressively grabbed, living organic is held
/// under until its lungs fail. The victim sleeps, counts as dead for objectives while it sleeps, and the drowner heals a
/// share of its own dead threshold (balance A-5). The defaults are the Early Warning Joe's.
/// </summary>
[RegisterComponent]
public sealed partial class CMUDrownComponent : Component
{
    /// <summary>Hold time before the drowning lands. Playtest 2 asked for longer than the 4 s of A-5.</summary>
    [DataField]
    public TimeSpan DrownDelay = TimeSpan.FromSeconds(8);

    /// <summary>Organ damage to the lungs; 55 puts human lungs in their dead stage.</summary>
    [DataField]
    public int DrownLungDamage = 55;

    [DataField]
    public TimeSpan DrownSleep = TimeSpan.FromSeconds(30);

    /// <summary>Share of the drowner's own dead threshold healed by a drowning.</summary>
    [DataField]
    public float DrownHealFraction = 0.15f;
}
