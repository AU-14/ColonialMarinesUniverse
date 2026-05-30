using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Reaper;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoReaperSystem))]
public sealed partial class XenoReaperComponent : Component
{
    [DataField, AutoNetworkedField]
    public int FleshResin;

    [DataField, AutoNetworkedField]
    public int MaxFleshResin = 1000;

    [DataField, AutoNetworkedField]
    public int MeleeGain = 5;

    [DataField, AutoNetworkedField]
    public int PassiveDrain = 1;

    [DataField, AutoNetworkedField]
    public TimeSpan PassiveDrainEvery = TimeSpan.FromSeconds(4);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextPassiveDrainAt;

    [DataField, AutoNetworkedField]
    public int HighFleshResinDrainThreshold = 300;

    [DataField, AutoNetworkedField]
    public int HighFleshResinDrain = 0;

    [DataField, AutoNetworkedField]
    public int FleshHarvestGain = 150;

    [DataField, AutoNetworkedField]
    public TimeSpan FleshHarvestDelay = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public int RaptureGain = 30;

    [DataField, AutoNetworkedField]
    public EntProtoId RaptureEffect = "RMCEffectExtraSlash";

    [DataField, AutoNetworkedField]
    public SoundSpecifier RaptureSound = new SoundCollectionSpecifier("AlienClaw");

    [DataField, AutoNetworkedField]
    public TimeSpan DrainPauseAfterAbility = TimeSpan.FromSeconds(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan PauseDrainUntil;

    [DataField, AutoNetworkedField]
    public int FleshBloomCost = 120;

    [DataField, AutoNetworkedField]
    public EntProtoId FleshBloomPrototype = "XenoFleshBloom";

    [DataField, AutoNetworkedField]
    public EntProtoId FleshBloomTelegraphPrototype = "RMCEffectReaperFleshBloomTelegraph";

    [DataField, AutoNetworkedField]
    public TimeSpan FleshBloomDelay = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public SoundSpecifier FleshBloomSound = new SoundCollectionSpecifier("XenoResinBreak");

    [DataField, AutoNetworkedField]
    public int RedGasCost = 10;

    [DataField, AutoNetworkedField]
    public int RedGasRange = 7;

    [DataField, AutoNetworkedField]
    public EntProtoId RedGasPrototype = "XenoReaperRedGas";

    [DataField, AutoNetworkedField]
    public SoundSpecifier RedGasSound = new SoundPathSpecifier("/Audio/Effects/smoke.ogg", AudioParams.Default.WithVolume(-8f).WithMaxDistance(5f));

    [DataField, AutoNetworkedField]
    public TimeSpan RedGasStepEvery = TimeSpan.FromSeconds(0.2);

    [DataField, AutoNetworkedField]
    public TimeSpan RedGasPulseEvery = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public float RedGasRadius = 0.5f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier RedGasDamage = new()
    {
        DamageDict = { ["Poison"] = 5 },
    };

    [DataField, AutoNetworkedField]
    public int CarrionMantleCost = 150;

    [DataField, AutoNetworkedField]
    public TimeSpan CarrionMantleDuration = TimeSpan.FromSeconds(8);

    [DataField, AutoNetworkedField]
    public FixedPoint2 CarrionMantleShieldAmount = FixedPoint2.New(200);

    [DataField, AutoNetworkedField]
    public int CarrionMantleShieldDecay = 100;

    [DataField, AutoNetworkedField]
    public string CarrionMantleShieldVisualState = "king-shield";

    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> Alert = "XenoFlesh";
}

[RegisterComponent, NetworkedComponent]
public sealed partial class XenoFleshHarvestedComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoReaperSystem))]
public sealed partial class XenoFleshBloomComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Reaper;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextPulseAt;

    [DataField, AutoNetworkedField]
    public TimeSpan PulseEvery = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public float Range = 0.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(1.5);

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = { ["Poison"] = 4 },
    };

    [DataField, AutoNetworkedField]
    public DamageSpecifier Heal = new()
    {
        DamageDict = { ["Blunt"] = -2, ["Slash"] = -2 },
    };
}

[RegisterComponent]
[Access(typeof(XenoReaperSystem))]
public sealed partial class XenoReaperRedGasComponent : Component
{
    public EntityUid? Reaper;

    public TimeSpan NextPulseAt;

    public TimeSpan PulseEvery = TimeSpan.FromSeconds(1);

    public float Radius = 0.5f;

    public DamageSpecifier Damage = new()
    {
        DamageDict = { ["Poison"] = 5 },
    };
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoReaperSystem))]
public sealed partial class XenoCarrionMantleComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public int Armor = 15;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 0.9f;

    [DataField, AutoNetworkedField]
    public string[] ImmuneToStatuses = { "KnockedDown" };
}
