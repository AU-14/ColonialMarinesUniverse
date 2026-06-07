using Content.Shared._RMC14.Stun;
using Content.Shared.Damage;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Bulwark;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoBulwarkSystem))]
public sealed partial class XenoBulwarkComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Encased;

    [DataField, AutoNetworkedField]
    public bool Reflecting;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan ReflectExpiresAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan ReflectStartedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan ReflectDuration = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public TimeSpan ReflectFullCooldown = TimeSpan.FromSeconds(18);

    [DataField, AutoNetworkedField]
    public TimeSpan ReflectMinCooldown = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public float ReflectCooldownPerSecond = 2f;

    [DataField, AutoNetworkedField]
    public EntProtoId ReflectEffectId = "RMCEffectShieldBlue";

    [DataField, AutoNetworkedField]
    public TimeSpan TailSwingMissCooldown = TimeSpan.FromSeconds(0.3);

    [DataField, AutoNetworkedField]
    public SoundSpecifier TailSwingSound = new SoundPathSpecifier("/Audio/_CM13/Effects/tail_swing.wav");

    [DataField, AutoNetworkedField]
    public TimeSpan TailSwingParalyzeTime = TimeSpan.FromSeconds(1.2);

    [DataField, AutoNetworkedField]
    public ProtoId<TagPrototype> TailSwingFlingable = "Grenade";

    [DataField, AutoNetworkedField]
    public float TailSwingFlingDistance = 3f;

    [DataField, AutoNetworkedField]
    public float TailSwingFlingSpeed = 10f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier PlateBashDamage = new()
    {
        DamageDict = { ["Blunt"] = 20 },
    };

    [DataField, AutoNetworkedField]
    public TimeSpan PlateBashParalyzeTime = TimeSpan.FromSeconds(0.8);

    [DataField, AutoNetworkedField]
    public float PlateBashUnencasedKnockBackDistance = 1f;

    [DataField, AutoNetworkedField]
    public float PlateBashEncasedKnockBackDistance = 3f;

    [DataField, AutoNetworkedField]
    public float PlateBashEncasedRange = 1.5f;

    [DataField, AutoNetworkedField]
    public float PlateBashUnencasedLeapRange = 3f;

    [DataField, AutoNetworkedField]
    public float PlateBashUnencasedLeapSpeed = 30f;

    [DataField, AutoNetworkedField]
    public float PlateBashKnockBackSpeed = 8f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier PlateBashSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/alien_knockdown.ogg");

    [DataField, AutoNetworkedField]
    public RMCSizes EncasedSize = RMCSizes.Big;

    [DataField, AutoNetworkedField]
    public RMCSizes? OriginalSize;

    [DataField, AutoNetworkedField]
    public string[] EncasedImmuneToStatuses = { "KnockedDown" };

    [DataField, AutoNetworkedField]
    public int PassiveFrontalArmor = 10;

    [DataField, AutoNetworkedField]
    public int PassiveSideArmor = 10;

    [DataField, AutoNetworkedField]
    public int EncasedFrontalArmor = 20;

    [DataField, AutoNetworkedField]
    public int EncasedSideArmorPenalty = -10;

    [DataField, AutoNetworkedField]
    public float EncasedSpeedMultiplier = 0.68f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier EncasedMeleePenalty = new()
    {
        DamageDict = { ["Slash"] = -8 },
    };
}
