using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A vehicle fuel tank: the vehicle's own <see cref="Solution"/> solution, burned while driving and
/// filled from a fuel can through its fuel door.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleFuelComponent : Component
{
    /// <summary>
    /// The tank's solution on the vehicle, and the one a fuel can pours from.
    /// </summary>
    [DataField]
    public string Solution = "fuel";

    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "WeldingFuel";

    /// <summary>
    /// The part that is the fuel door: a fuel can used on it fills the tank while it is open.
    /// </summary>
    [DataField]
    public string Door = "fuel_door";

    /// <summary>
    /// Fuel burned per second while the vehicle moves.
    /// </summary>
    [DataField]
    public float BurnRate = 0.05f;

    /// <summary>
    /// Fuel poured per second from a can.
    /// </summary>
    [DataField]
    public float PourRate = 6f;

    [DataField]
    public SoundSpecifier? PourSound = new SoundPathSpecifier("/Audio/Effects/refill.ogg");

    /// <summary>
    /// How often burned fuel leaves the tank.
    /// </summary>
    [DataField]
    public TimeSpan SyncInterval = TimeSpan.FromSeconds(1);

    public TimeSpan NextSync;

    /// <summary>
    /// Fuel burned but not yet taken from the tank.
    /// </summary>
    public float Pending;
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleRefuelDoAfterEvent : SimpleDoAfterEvent;
