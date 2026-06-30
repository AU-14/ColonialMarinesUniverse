using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Medical.Wounds;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedWoundsSystem))]
public sealed partial class WoundedComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<DamageGroupPrototype> BruteWoundGroup = "Brute";

    [DataField, AutoNetworkedField]
    public ProtoId<DamageGroupPrototype> BurnWoundGroup = "Burn";

    [DataField, AutoNetworkedField]
    public List<Wound> Wounds = new();

    [DataField, AutoNetworkedField]
    public FixedPoint2 PassiveHealing = FixedPoint2.New(-0.05f);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan UpdateAt;

    [DataField]
    public TimeSpan UpdateCooldown = TimeSpan.FromSeconds(1);
}

[DataRecord]
[Serializable, NetSerializable]
public partial record struct Wound
{
    public Wound()
    {
    }

    public Wound(
        FixedPoint2 damage,
        FixedPoint2 healed,
        float bloodloss,
        TimeSpan? stopBleedAt,
        WoundType type,
        bool treated)
    {
        Damage = damage;
        Healed = healed;
        Bloodloss = bloodloss;
        StopBleedAt = stopBleedAt;
        Type = type;
        Treated = treated;
    }

    public FixedPoint2 Damage { get; set; }
    public FixedPoint2 Healed { get; set; }
    public float Bloodloss { get; set; }

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? StopBleedAt { get; set; }

    public WoundType Type { get; set; }
    public bool Treated { get; set; }
}

[Serializable, NetSerializable]
public enum WoundType
{
    Brute = 0,
    Burn,
    Surgery
}
