using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Medical.HUD.Components;

/// <summary>
/// The holocard state used to indicate which holocard description and icon to show
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HolocardStateComponent : Component
{
    /// <summary>The effective projection sent to HUD viewers and containers.</summary>
    [DataField, AutoNetworkedField]
    public HolocardStatus HolocardStatus = HolocardStatus.None;

    /// <summary>Explicit user annotation. None relinquishes this source.</summary>
    [DataField]
    public HolocardStatus ManualStatus;

    /// <summary>The diagnostic owner's current assessment, independent of the annotation.</summary>
    [DataField]
    public HolocardStatus AutomaticStatus;

    /// <summary>
    /// Records the existing brain-extraction assessment, without changing revival eligibility.
    /// Reattachment, successful revival, or rejuvenation clears this medical source.
    /// </summary>
    [DataField]
    public bool BrainRemovalAssessment;
}
