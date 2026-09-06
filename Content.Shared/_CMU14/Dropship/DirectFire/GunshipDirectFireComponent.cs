using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Dropship.DirectFire;

/// <summary>
/// A dropship weapon point reserved for pilot-controlled, forward-firing weapons.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunshipDirectFirePointComponent : Component
{
    [DataField, AutoNetworkedField]
    public float AimOffsetDegrees;

    /// <summary>
    /// Distance from the attachment wall to the visible weapon base, along
    /// ship-forward. The projectile origin uses this same mounting position.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ForwardOffset = 1f;
}

/// <summary>
/// Adapts a normal gun to a dropship direct-fire attachment and its external
/// ammunition box. GimbalDegrees is the complete firing arc centered on
/// ship-forward.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunshipDirectFireWeaponComponent : Component
{
    [DataField]
    public EntProtoId Projectile = "RMCFlareCASBullet";

    [DataField(required: true)]
    public EntProtoId AmmoPrototype;

    [DataField, AutoNetworkedField]
    public float GimbalDegrees = 30f;

    /// <summary>
    /// Distance from the visible mount base to the muzzle along the clamped
    /// firing direction.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MuzzleOffset = 1.25f;
}

[Serializable, NetSerializable]
public enum GunshipDirectFireVisuals
{
    AimOffsetDegrees,
}

[Serializable, NetSerializable]
public sealed class GunshipDirectFireAimEvent(NetCoordinates coordinates) : EntityEventArgs
{
    public NetCoordinates Coordinates = coordinates;
}
