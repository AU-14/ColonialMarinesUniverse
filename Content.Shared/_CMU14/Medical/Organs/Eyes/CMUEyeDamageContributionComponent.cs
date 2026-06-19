using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Organs.Eyes;

/// <summary>
///     Tracks the eye-damage value the CMU eye-stage system has contributed
///     to the body's <see cref="Content.Shared.Eye.Blinding.Components.BlindableComponent.EyeDamage"/>.
///     Storing the last-applied number lets the system subtract its previous
///     contribution before adding a new one on each stage change, so other
///     eye-damage sources (welder flashes, flashbangs) accumulate independently
///     without being clobbered.
/// </summary>
[RegisterComponent]
public sealed partial class CMUEyeDamageContributionComponent : Component
{
    [DataField]
    public int Applied;
}
