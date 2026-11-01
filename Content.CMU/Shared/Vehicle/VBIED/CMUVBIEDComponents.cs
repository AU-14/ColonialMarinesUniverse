using Content.Shared.DoAfter;
using Content.Shared.Explosion;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.VBIED;

/// <summary>
/// An explosive that can be rigged inside a civilian vehicle as a car bomb.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVBIEDComponent : Component
{
    [DataField]
    public TimeSpan RigDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How close, in tiles, someone with hostile IFF has to come to set off a proximity rig.
    /// </summary>
    [DataField]
    public float ProximityRange = 10f;

    /// <summary>
    /// How much integrity the vehicle can lose, across its hull and parts, before the bomb goes off.
    /// </summary>
    [DataField]
    public float DamageThreshold = 50f;

    /// <summary>
    /// The blast set off inside the vehicle's interior alongside the one outside it.
    /// </summary>
    [DataField]
    public ProtoId<ExplosionPrototype> InteriorExplosionType = "CMUIED";

    [DataField]
    public float InteriorTotalIntensity = 400f;

    [DataField]
    public float InteriorIntensitySlope = 20f;

    [DataField]
    public float InteriorMaxIntensity = 40f;
}

/// <summary>
/// A vehicle an IED can be rigged inside. Military vehicles don't get this.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVBIEDRiggableComponent : Component
{
    public const string ContainerId = "cmu-vbied";
}

[Serializable, NetSerializable]
public enum CMUVBIEDTrigger : byte
{
    /// <summary>
    /// When the vehicle crashes into something or runs someone down.
    /// </summary>
    Impact,

    /// <summary>
    /// When someone with IFF hostile to whoever rigged it comes close.
    /// </summary>
    Proximity,
}

[Serializable, NetSerializable]
public sealed partial class CMUVBIEDRigDoAfterEvent : DoAfterEvent
{
    [DataField]
    public CMUVBIEDTrigger Trigger;

    public CMUVBIEDRigDoAfterEvent()
    {
    }

    public CMUVBIEDRigDoAfterEvent(CMUVBIEDTrigger trigger)
    {
        Trigger = trigger;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUVBIEDRigDoAfterEvent(Trigger);
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUVBIEDInspectDoAfterEvent : SimpleDoAfterEvent;
