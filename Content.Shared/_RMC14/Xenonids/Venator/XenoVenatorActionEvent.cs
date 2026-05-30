using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Venator;

public sealed partial class XenoVenatorSpitActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId Projectile = "XenoVenatorCorrosiveProjectile";

    [DataField]
    public int Shots = 1;

    [DataField]
    public Angle Deviation = Angle.Zero;

    [DataField]
    public float Speed = 24;

    [DataField]
    public bool UseStoreCharge = true;

    [DataField]
    public SoundSpecifier? Sound = new SoundCollectionSpecifier("XenoSpitAcid", AudioParams.Default.WithVolume(-10f));

    [DataField]
    public bool StopAtTarget;
}

public sealed partial class XenoStoreAcidActionEvent : InstantActionEvent;
