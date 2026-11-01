using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja;

// cmss13 makes a thrall the "Thrall" species the moment the master links their bracer (link_bracer). that's the
// hero human baseline plus the PR 11542 thrall changes. added on link, removed on unlink, only takes back what it gave
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

    // hero punch/strong does 10 against the normal 5
    [DataField]
    public float UnarmedMultiplier = 2f;

    // darksight and the colder cold_level_1, swapped onto the thrall from this prototype
    [DataField]
    public EntProtoId SpeciesPrototype = "CMUYautjaThrallSpecies";

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

    [ViewVariables]
    public bool AddedNoPermanentDamage;

    [ViewVariables]
    public bool AddedNoDragSuffocation;

    [ViewVariables]
    public bool AddedNightVision;

    [ViewVariables]
    public bool ReplacedTemperatureSpeed;
}
