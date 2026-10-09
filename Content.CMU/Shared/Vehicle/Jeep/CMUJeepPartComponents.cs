using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Wear on a removable jeep part (windshield, headlights); at or below <see cref="BrokenFraction"/>
/// it shows damaged and stops working.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUJeepPartIntegrityComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Integrity = 50f;

    [DataField]
    public float MaxIntegrity = 50f;

    [DataField]
    public float BrokenFraction = 0.5f;
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
public sealed partial class CMUJeepEngineRepairDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUJeepPartRemoveDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Slot = string.Empty;

    public CMUJeepPartRemoveDoAfterEvent()
    {
    }

    public CMUJeepPartRemoveDoAfterEvent(string slot)
    {
        Slot = slot;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUJeepPartRemoveDoAfterEvent(Slot);
    }
}

/// <summary>
/// One step of taking a moving part off or putting it on: its fastening goes from <see cref="From"/>
/// to <see cref="To"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CMUJeepPanelFastenDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Slot = string.Empty;

    [DataField]
    public int From;

    [DataField]
    public int To;

    public CMUJeepPanelFastenDoAfterEvent()
    {
    }

    public CMUJeepPanelFastenDoAfterEvent(string slot, int from, int to)
    {
        Slot = slot;
        From = from;
        To = to;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUJeepPanelFastenDoAfterEvent(Slot, From, To);
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUJeepPartRepairDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Slot = string.Empty;

    public CMUJeepPartRepairDoAfterEvent()
    {
    }

    public CMUJeepPartRepairDoAfterEvent(string slot)
    {
        Slot = slot;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUJeepPartRepairDoAfterEvent(Slot);
    }
}
