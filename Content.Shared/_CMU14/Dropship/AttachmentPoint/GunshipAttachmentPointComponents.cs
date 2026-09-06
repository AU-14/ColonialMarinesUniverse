using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Dropship.AttachmentPoint;

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
    public EntityUid? AttachmentPoint;
    public EntityUid? HeldDoor;
}

/// <summary>
/// Tracks all occupied door-gunner mounts currently preventing this door from closing.
/// </summary>
[RegisterComponent]
public sealed partial class GunshipDoorGunnerHeldOpenComponent : Component
{
    public HashSet<EntityUid> Holders = new();
}
