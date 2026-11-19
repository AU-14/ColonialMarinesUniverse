using Content.Shared.Actions;
using Content.Shared.Actions.Components;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

/// <summary>
/// Grants hang-out and hang-back actions to a door gunner mount.
/// </summary>
[RegisterComponent]
public sealed partial class GunshipDoorGunnerActionComponent : Component
{
    [DataField]
    public EntProtoId HangOutAction = "ActionGunshipDoorGunnerHangOut";

    [DataField]
    public EntityUid? HangOutActionEntity;

    [DataField]
    public EntProtoId HangBackAction = "ActionGunshipDoorGunnerHangBack";

    [DataField]
    public EntityUid? HangBackActionEntity;
}
