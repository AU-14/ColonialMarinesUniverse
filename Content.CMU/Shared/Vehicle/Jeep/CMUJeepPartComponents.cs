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
