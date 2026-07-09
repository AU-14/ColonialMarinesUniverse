using Content.Shared.FixedPoint;
using Content.Shared._CMU14.Medical.Injuries.Wounds;

namespace Content.Server._CMU14.Medical.Injuries.Wounds;

/// <summary>
///     Transient routing handle for the bandage picker BUI. Carries the
///     patient + treater context because the
///     <see cref="BodyPartPickerSelectMessage"/> only carries the picked part.
///     Server-only.
/// </summary>
[RegisterComponent]
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
