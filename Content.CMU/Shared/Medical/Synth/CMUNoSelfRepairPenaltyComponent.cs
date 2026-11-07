using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Synth;

/// <summary>
/// Repairing or treating yourself takes as long as having someone else do it.
/// Skips the wound treater self multiplier, robotic limb SelfRepairTime and synth organ OrganSelfRepairTime.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUNoSelfRepairPenaltyComponent : Component;
