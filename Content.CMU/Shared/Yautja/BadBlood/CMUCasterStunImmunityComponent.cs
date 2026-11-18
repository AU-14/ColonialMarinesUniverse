namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 COUPLING-601: caster stun diminishing returns, on the target. A caster bolt with an immunity window stuns,
/// then leaves this behind; until <see cref="Until"/> any bolt with a window (stun or immobilizer) only slows.
/// Honourable bolts have no window, so they never read or write it.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCasterStunImmunityComponent : Component
{
    [DataField]
    public TimeSpan Until;
}
