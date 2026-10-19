using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Wear on a removable jeep part (windshield, headlights); at or below <see cref="BrokenFraction"/>
/// it shows damaged and stops working.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehiclePartIntegrityComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Integrity = 50f;

    [DataField]
    public float MaxIntegrity = 50f;

    [DataField]
    public float BrokenFraction = 0.5f;

    /// <summary>
    /// Integrity a welder pass gives back; the vehicle's own amount when unset.
    /// </summary>
    [DataField]
    public float? RepairAmount;

    /// <summary>
    /// Worn right down, it is wrecked: no welder can save it, and it has to be taken off and replaced.
    /// </summary>
    [DataField]
    public bool Wreckable;

    /// <summary>
    /// The wear it has taken by damage type since it was last whole: which damage its stages show.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> Wear = new();

    /// <summary>
    /// Played on the vehicle when wear breaks the part: a headlight's bulb shattering.
    /// </summary>
    [DataField]
    public SoundSpecifier? BreakSound;
}

/// <summary>
/// A holed fuel can, losing its fuel.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUFuelLeakComponent : Component
{
    [DataField]
    public string Solution = "fuel";

    /// <summary>
    /// Units lost per second.
    /// </summary>
    [DataField]
    public float Rate = 0.5f;

    /// <summary>
    /// Fuel lost but not yet drained, waiting to add up to a hundredth of a unit.
    /// </summary>
    public float Pending;
}

/// <summary>
/// The open vehicles a projectile has already passed through and damaged.
/// </summary>
[RegisterComponent]
public sealed partial class CMUPassedThroughComponent : Component
{
    public HashSet<EntityUid> Vehicles = new();
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleEngineRepairDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUVehiclePartRemoveDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Slot = string.Empty;

    public CMUVehiclePartRemoveDoAfterEvent()
    {
    }

    public CMUVehiclePartRemoveDoAfterEvent(string slot)
    {
        Slot = slot;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUVehiclePartRemoveDoAfterEvent(Slot);
    }
}

/// <summary>
/// One step of taking a moving part off or putting it on: its fastening goes from <see cref="From"/>
/// to <see cref="To"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CMUVehiclePanelFastenDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Slot = string.Empty;

    [DataField]
    public int From;

    [DataField]
    public int To;

    public CMUVehiclePanelFastenDoAfterEvent()
    {
    }

    public CMUVehiclePanelFastenDoAfterEvent(string slot, int from, int to)
    {
        Slot = slot;
        From = from;
        To = to;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUVehiclePanelFastenDoAfterEvent(Slot, From, To);
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUVehiclePartRepairDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Slot = string.Empty;

    public CMUVehiclePartRepairDoAfterEvent()
    {
    }

    public CMUVehiclePartRepairDoAfterEvent(string slot)
    {
        Slot = slot;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUVehiclePartRepairDoAfterEvent(Slot);
    }
}
