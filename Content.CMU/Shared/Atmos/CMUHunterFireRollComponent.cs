namespace Content.Shared.CMU14.Atmos;

// cmss13 resist_fire: yautja and thralls "expertly roll" for -25 stacks and WEAKEN 1 (the code notes it's
// really ~0.5s) instead of the -10 / WEAKEN 4 humans get, and they never panic-roll
[RegisterComponent]
public sealed partial class CMUHunterFireRollComponent : Component
{
    [DataField]
    public TimeSpan ResistTime = TimeSpan.FromSeconds(1);

    // total stacks one roll takes off, the normal OnFire resist amount is topped up to this
    [DataField]
    public float ResistStacks = -25;

    [ViewVariables]
    public TimeSpan? OriginalResistTime;

    [ViewVariables]
    public TimeSpan? LastRollEnd;
}
