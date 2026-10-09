using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A cargo bed one crate is dragged onto, then wrenched down so it stays put while driving.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleCargoComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Crate;

    [DataField, AutoNetworkedField]
    public bool Secured;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public TimeSpan LoadDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public ProtoId<ToolQualityPrototype> SecureQuality = "Anchoring";

    [DataField]
    public TimeSpan SecureDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Where an unloaded crate is set down, in vehicle space with the vehicle facing south.
    /// </summary>
    [DataField]
    public Vector2 DropOffset = new(0f, 1.7f);

    /// <summary>
    /// Speed above which a loose crate slides off.
    /// </summary>
    [DataField]
    public float SlideOffSpeed = 2.5f;

    /// <summary>
    /// Pixel offset (x right, y up) of the crate sprite from the vehicle centre, per direction.
    /// </summary>
    [DataField]
    public Dictionary<Direction, Vector2> CrateOffsets = new();
}

/// <summary>
/// A crate riding in a cargo bed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleCargoCrateComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField]
    public BodyType BodyType = BodyType.Dynamic;

    [DataField]
    public bool CanCollide = true;
}

/// <summary>
/// A crate that has been pulled. Clicking a jeep with the hand pulling it loads it onto the cargo bed;
/// the pull's virtual item hands that click to the crate. Left on once the pull ends: the handlers
/// check who is pulling it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleCargoPulledComponent : Component;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleCargoLoadDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleCargoSecureDoAfterEvent : SimpleDoAfterEvent;
