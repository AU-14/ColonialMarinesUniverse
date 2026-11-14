using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

/// <summary>
/// Storage for spare gunship hardpoint magazines. While installed on a dropship utility point it is opened and
/// loaded through the point, and empty hardpoint weapons on the same grid reload from it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunshipAmmoRackComponent : Component;
