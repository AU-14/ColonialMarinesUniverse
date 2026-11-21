using Content.Shared.Actions;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

/// <summary>
/// Event triggered when a door gunner attempts to hang out of the side of the gunship.
/// Places the gunner a tile forward and rotates the gun 90 degrees outward.
/// </summary>
public sealed partial class GunshipDoorGunnerHangOutActionEvent : InstantActionEvent;

/// <summary>
/// Event triggered when a door gunner attempts to hang back inside the gunship.
/// Returns the gunner to the mount position and rotates the gun back.
/// </summary>
public sealed partial class GunshipDoorGunnerHangBackActionEvent : InstantActionEvent;
