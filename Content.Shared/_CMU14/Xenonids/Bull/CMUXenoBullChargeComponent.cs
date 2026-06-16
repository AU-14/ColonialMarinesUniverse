using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenonids.Bull;

public enum CMUXenoBullChargeMode : byte
{
    Plow,
    Headbutt,
    Gore,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUXenoBullChargeSystem))]
public sealed partial class CMUXenoBullChargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUXenoBullChargeMode Mode = CMUXenoBullChargeMode.Plow;

    [DataField, AutoNetworkedField]
    public DamageSpecifier PlowDamage = new()
    {
        DamageDict = { ["Blunt"] = FixedPoint2.New(8) },
    };

    [DataField, AutoNetworkedField]
    public DamageSpecifier HeadbuttDamage = new()
    {
        DamageDict = { ["Blunt"] = FixedPoint2.New(14) },
    };

    [DataField, AutoNetworkedField]
    public DamageSpecifier GoreDamage = new()
    {
        DamageDict = { ["Piercing"] = FixedPoint2.New(24) },
    };

    [DataField, AutoNetworkedField]
    public float PlowKnockback = 1.5f;

    [DataField, AutoNetworkedField]
    public float HeadbuttKnockback = 3f;

    [DataField, AutoNetworkedField]
    public float GoreKnockback = 1f;

    [DataField, AutoNetworkedField]
    public float KnockbackSpeed = 10f;

    [DataField, AutoNetworkedField]
    public TimeSpan HeadbuttParalyze = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan GoreSlowdown = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public TimeSpan GoreStagger = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public string GoreReagent = "RMCXenoAlchPain";

    [DataField, AutoNetworkedField]
    public FixedPoint2 GoreReagentAmount = FixedPoint2.New(10);

    [DataField, AutoNetworkedField]
    public SoundSpecifier ImpactSound = new SoundCollectionSpecifier("Punch");

    [DataField, AutoNetworkedField]
    public SoundSpecifier GoreImpactSound = new SoundCollectionSpecifier("Punch");

    [DataField, AutoNetworkedField]
    public SoundSpecifier GoreSpraySound = new SoundPathSpecifier("/Audio/Effects/spray3.ogg");
}
