using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Injuries.Wounds;

/// <summary>
///     Transient routing handle for the bandage picker BUI. Carries the
///     patient + treater context because the
///     <see cref="BodyPartPickerSelectMessage"/> only carries the picked
///     part. Server-only.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBandagePendingComponent : Component
{
    [DataField]
    public EntityUid Patient;

    [DataField]
    public EntityUid Treater;

    [DataField]
    public EntityUid? PartHealthCapPart;

    [DataField]
    public FixedPoint2? PartHealthCap;
}
