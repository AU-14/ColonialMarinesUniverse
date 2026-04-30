using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Effects;

/// <summary>
///     Restores the organ in <see cref="OrganSlot"/> to full health and
///     forces a stage recompute so the resulting <c>OrganStageChangedEvent</c>
///     fires immediately.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepRepairOrganEffectComponent : Component
{
    [DataField(required: true)]
    public string OrganSlot = string.Empty;
}
