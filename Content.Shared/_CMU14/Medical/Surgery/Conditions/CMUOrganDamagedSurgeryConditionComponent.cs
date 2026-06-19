using Content.Shared._CMU14.Medical.Organs;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Conditions;

/// <summary>
///     Step is valid only when the part contains the named organ slot AND the
///     contained organ's <see cref="OrganHealthComponent.Stage"/> is at or
///     past <see cref="MinStage"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUOrganDamagedSurgeryConditionComponent : Component
{
    [DataField(required: true)]
    public string OrganSlot = string.Empty;

    [DataField]
    public OrganDamageStage MinStage = OrganDamageStage.Bruised;
}
