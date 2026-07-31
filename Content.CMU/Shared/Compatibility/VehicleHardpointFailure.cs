using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleHardpointFailureComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<VehicleHardpointFailure> ActiveFailures = new();
}

[Serializable, NetSerializable]
public enum VehicleHardpointFailure : byte
{
    ArmorCompromised,
    FeedJam,
    RunawayTrigger,
    TurretTraverseDamage,
    EngineMisfire,
    TransmissionSlip,
    WarpedFrame,
    DamagedMount,
    TireBlowout,
    ThrownTread,
    EngineOverheat,
    ElectricalShort,
    FuelLeak,
}

public sealed partial class HardpointSystem
{
    public bool HasHardpointFailure(EntityUid uid, VehicleHardpointFailure failure,
        VehicleHardpointFailureComponent? component = null)
        => Resolve(uid, ref component, false) && component.ActiveFailures.Contains(failure);

    public void ClearAllFailures(EntityUid uid)
    {
        if (TryComp(uid, out VehicleHardpointFailureComponent? component))
            component.ActiveFailures.Clear();
        RemCompDeferred<VehicleHardpointFailureComponent>(uid);
    }

    public void ResetAllHardpointsToFullHealth(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out HardpointIntegrityComponent? integrity))
            return;
        integrity.Integrity = integrity.MaxIntegrity;
        Dirty(vehicle, integrity);
    }
}
