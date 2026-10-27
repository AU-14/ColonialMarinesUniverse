using Content.Shared.Actions;
using Content.Shared.Explosion;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Xeno.Gambler;

public sealed partial class CMUXenoGambleActionEvent : InstantActionEvent;

public sealed partial class CMUXenoGambleReelActionEvent : InstantActionEvent;

// makes it rain resin froots
public sealed partial class CMUXenoGambleFruitRainActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public List<EntProtoId> Fruits = new();

    [DataField]
    public int Amount = 12;

    [DataField]
    public float Radius = 3;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(0.15);

    [DataField]
    public SoundSpecifier? DropSound;
}

// mystery ability that explodes you
public sealed partial class CMUXenoGambleExplodeActionEvent : InstantActionEvent
{
    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "RMC";

    [DataField]
    public float TotalIntensity = 150;

    [DataField]
    public float IntensitySlope = 11;

    [DataField]
    public float MaxIntensity = 22;
}
