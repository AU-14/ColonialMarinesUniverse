using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle;

/// <summary>Resin that can foul a vehicle's running gear on contact.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleStickyResinComponent : Component
{
    [DataField]
    public float Chance = 0.4f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleResinComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Fouled;

    [DataField]
    public float SpeedMultiplier = 0.4f;

    [DataField]
    public TimeSpan ClearTime = TimeSpan.FromSeconds(8);
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleClearResinDoAfterEvent : SimpleDoAfterEvent;
