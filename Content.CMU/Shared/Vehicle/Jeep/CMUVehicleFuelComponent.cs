using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A vehicle tank that burns fuel while driving and is refilled from fuel cans through its fuel door.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleFuelComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Fuel = 60f;

    [DataField]
    public float MaxFuel = 60f;

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
    public string CanSolution = "fuel";

    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "WeldingFuel";

    [DataField]
    public SoundSpecifier? PourSound = new SoundPathSpecifier("/Audio/Effects/refill.ogg");

    [DataField]
    public TimeSpan SyncInterval = TimeSpan.FromSeconds(1);

    public TimeSpan NextSync;
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleRefuelDoAfterEvent : SimpleDoAfterEvent;
