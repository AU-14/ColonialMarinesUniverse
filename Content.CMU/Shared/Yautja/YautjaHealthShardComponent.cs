using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Yautja;

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaHealthShardComponent : Component
{
    [DataField]
    public EntProtoId HalfPrototype = "CMUYautjaHealthShardHalf";

    [DataField]
    public SoundSpecifier? SplitSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/HealthShard/prd_health_twist_01.wav");

    [DataField]
    public SoundSpecifier? EquipSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/HealthShard/prd_health_twist_air_02.wav");
}

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaHealthShardHalfComponent : Component
{
    [DataField]
    public EntProtoId WholePrototype = "CMUYautjaHealthShard";

    [DataField]
    public SoundSpecifier? MergeSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/HealthShard/prd_health_twist_01.wav");

    [DataField]
    public SoundSpecifier? UseSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/HealthShard/prd_health_jab_01.wav");

    [DataField]
    public SoundSpecifier? CompleteSound = new SoundCollectionSpecifier("CMUYautjaHealthShardPainScream");

    [DataField]
    public TimeSpan UseDuration = TimeSpan.FromSeconds(0.95);

    [DataField]
    public DamageSpecifier InstantHeal = new()
    {
        DamageDict = new()
        {
            { "Blunt", FixedPoint2.New(-15) },
            { "Slash", FixedPoint2.New(-15) },
            { "Piercing", FixedPoint2.New(-15) },
            { "Heat", FixedPoint2.New(-15) },
            { "Shock", FixedPoint2.New(-15) },
            { "Caustic", FixedPoint2.New(-15) },
            { "Poison", FixedPoint2.New(-15) },
        },
    };

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new()
    {
        { "CMBicaridine", FixedPoint2.New(15) },
        { "CMKelotane", FixedPoint2.New(15) },
        { "CMTricordrazine", FixedPoint2.New(15) },
        { "CMUParacetamol", FixedPoint2.New(15) },
    };
}
