using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ShipWeapons;

/// <summary>
///     A ship's orbital battery. It fires one mission, then has to be repaired by working through
///     <see cref="RepairSteps"/> in order, each with its own tool, before it can fire again.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUShipWeaponGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<CMUShipWeaponPrototype>> Weapons = new();

    /// <summary>Whether the battery can fire. Firing clears this until the repair is finished.</summary>
    [DataField, AutoNetworkedField]
    public bool Ready = true;

    /// <summary>How many of <see cref="RepairSteps"/> are done since the battery last fired.</summary>
    [DataField, AutoNetworkedField]
    public int RepairStep;

    [DataField(required: true)]
    public List<CMUShipWeaponRepairStep> RepairSteps = new();
}

/// <summary>One step of an orbital battery's repair: a tool, how long it takes, and what the crew are told.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUShipWeaponRepairStep
{
    [DataField(required: true)]
    public ProtoId<ToolQualityPrototype> Quality;

    [DataField]
    public float Delay = 3f;

    /// <summary>Welding fuel the step uses up.</summary>
    [DataField]
    public float Fuel;

    /// <summary>What the step is, shown on examine while it's the next one to do.</summary>
    [DataField(required: true)]
    public LocId Hint;

    /// <summary>Shown to the user when the step is done.</summary>
    [DataField(required: true)]
    public LocId Done;
}

[Serializable, NetSerializable]
public sealed partial class CMUShipWeaponRepairDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Step;

    private CMUShipWeaponRepairDoAfterEvent()
    {
    }

    public CMUShipWeaponRepairDoAfterEvent(int step)
    {
        Step = step;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUShipWeaponRepairDoAfterEvent(Step);
    }
}

/// <summary>
///     Admin testing: spawning this fires its strike on the spot, then deletes itself. With <see cref="Instant"/> off
///     it runs the real 24-second flight, with the warnings and sounds; on, it goes straight to impact.
/// </summary>
[RegisterComponent]
public sealed partial class CMUShipWeaponStrikeSpawnerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CMUShipWeaponPrototype> Weapon;

    [DataField]
    public CMUShipWeaponBearing Bearing = CMUShipWeaponBearing.North;

    [DataField]
    public bool Instant;
}
