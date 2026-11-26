using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

/// <summary>
/// A utility-compatible point reserved for crew-served gunship hardpoints.
/// Attachment restrictions are enforced both here and by the point's dedicated tag.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunshipHardpointAttachmentPointComponent : Component
{
    [DataField]
    public HashSet<EntProtoId> AllowedAttachments = new()
    {
        "RMCDropshipAttachmentDoorGun",
        "CMUDropshipAttachmentDoorGunM2C",
        "CMUDropshipAttachmentDoorGunMk86",
        "CMUDropshipAttachmentDoorGunATGM",
        "CMUDropshipAttachmentDoorGunUTESC",
        "CMUDropshipAttachmentDoorGunGAU19M",
    };
}

/// <summary>
/// A utility-compatible point reserved for gunship-specific utility systems.
/// Attachment restrictions are enforced both here and by the point's dedicated tag.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunshipUtilityAttachmentPointComponent : Component
{
    [DataField]
    public HashSet<EntProtoId> AllowedAttachments = new()
    {
        "CMUDropshipAttachmentEEXRappelSystem",
        "RMCDropshipAttachmentLaunchBay",
        "CMUDropshipAttachmentAmmoRack",
    };
}

/// <summary>
/// Marks an equipment deployer whose deployed weapon mount may hold a nearby
/// dropship door open when installed in a gunship hardpoint.
/// </summary>
[RegisterComponent]
public sealed partial class GunshipDoorGunnerAttachmentComponent : Component;

/// <summary>
/// Runtime link from a deployed door-gunner mount back to its specialized point.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveGunshipDoorGunnerComponent : Component
{
    [DataField]
    public EntityUid? AttachmentPoint;

    [DataField]
    public EntityUid? HeldDoor;
}

/// <summary>
/// Tracks all occupied door-gunner mounts currently preventing this door from closing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunshipDoorGunnerHeldOpenComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool HeldOpen;

    public HashSet<EntityUid> Holders = new();
}

/// <summary>
/// Marks a door gunner who is currently hanging out of the side of the gunship.
/// When active, the gunner is positioned forward and the gun is rotated outward.
/// </summary>
[RegisterComponent]
public sealed partial class GunshipDoorGunnerHangingOutComponent : Component
{
    /// <summary>
    /// Whether the gunner is currently hanging out (true) or inside (false).
    /// </summary>
    [DataField]
    public bool HangingOut;

    /// <summary>
    /// Stored rotation when hanging out for restoration when going back inside.
    /// </summary>
    [DataField]
    public Angle? StoredRotation;
}
