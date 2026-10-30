using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A vehicle's searchlight, fitted on one of its searchlight lamps
/// (<see cref="CMUVehicleLampRole.Searchlight"/>): taken in hand by clicking it, it lights up and
/// follows its operator's cursor until they let go of it (drop the hand holding it). A screwdriver
/// takes it off; it goes on either mount with a screwdriver in the other hand.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleSearchlightComponent : Component
{
    [DataField]
    public EntProtoId BeamPrototype = "CMUVehicleSearchlightBeam";

    /// <summary>
    /// The beam, attached to the vehicle and turned to where the searchlight points.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Beam;

    /// <summary>
    /// Who is working the searchlight, the searchlight they hold and the lamp it is fitted on.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Operator;

    [DataField, AutoNetworkedField]
    public EntityUid? Held;

    [DataField, AutoNetworkedField]
    public string? Lamp;

    /// <summary>
    /// How far from the vehicle, in tiles, its operator can go before they let go of it.
    /// </summary>
    [DataField]
    public float Reach = 2f;

    [DataField]
    public TimeSpan FitDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How far either side of the vehicle's front it swings, in degrees (0 for all the way round),
    /// and how fast, in degrees a second (0 to point at once). The jeep's match its gun mount.
    /// </summary>
    [DataField]
    public float MaxYawDegrees = 45f;

    [DataField]
    public float RotationSpeed = 220f;

    /// <summary>
    /// Where it is swinging to, from the vehicle's front.
    /// </summary>
    [ViewVariables]
    public Angle TargetAim;

    [DataField]
    public SoundSpecifier? SwitchSound;
}

/// <summary>
/// On whoever is working a vehicle's searchlight: their client aims it at their cursor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleSearchlightOperatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Vehicle;
}

/// <summary>
/// The operator's client pointing the searchlight: the world angle from its lens to their cursor.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUVehicleSearchlightAimEvent : EntityEventArgs
{
    public NetEntity Vehicle;
    public Angle Angle;
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleSearchlightFitDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Slot = string.Empty;

    [DataField]
    public NetEntity Searchlight;

    public CMUVehicleSearchlightFitDoAfterEvent()
    {
    }

    public CMUVehicleSearchlightFitDoAfterEvent(string slot, NetEntity searchlight)
    {
        Slot = slot;
        Searchlight = searchlight;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUVehicleSearchlightFitDoAfterEvent(Slot, Searchlight);
    }
}
