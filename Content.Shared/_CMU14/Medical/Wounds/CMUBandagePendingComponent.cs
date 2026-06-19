using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Wounds;

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
}
