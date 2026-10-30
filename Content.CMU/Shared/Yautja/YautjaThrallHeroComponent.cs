using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// cmss13 turns a thrall into the "Thrall" species the moment the master links their bracer (thrall_procs.dm
/// link_bracer). That species is the human hero baseline plus the thrall changes from cmss13 PR 11542. This is
/// that species: added on link, removed on unlink, and it only takes back what it gave.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(YautjaThrallHeroSystem))]
public sealed partial class YautjaThrallHeroComponent : Component
{
    // hero brute_mod / burn_mod
    [DataField]
    public float BruteMultiplier = 0.5f;

    [DataField]
    public float BurnMultiplier = 0.5f;

    // hero stun_reduction / knock_down_reduction
    [DataField]
    public float StunResistance = 2f;

    // thrall total_health 150 instead of 100, crit and death both come 50 damage later
    [DataField]
    public FixedPoint2 ExtraHealth = 50;

    [DataField]
    public EntProtoId MarkForHuntActionId = "CMUActionYautjaMarkForHunt";

    [ViewVariables]
    public EntityUid? MarkForHuntAction;

    [ViewVariables]
    public FixedPoint2 AppliedExtraHealth;

    [ViewVariables]
    public float? PreviousStunResistance;

    [ViewVariables]
    public bool AddedStunResistance;

    [ViewVariables]
    public bool AddedNoPullSlowdown;

    [ViewVariables]
    public bool AddedIgnoreWeeds;

    [ViewVariables]
    public bool AddedButcher;
}
