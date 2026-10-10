using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Round.Antags.Rider;

/// <summary>
/// On the parasite-extraction surgery entity. Cancels validity unless the body
/// is currently ridden; the hatchling is what gets cut out.
/// </summary>
[RegisterComponent]
public sealed partial class RiderSurgeryConditionComponent : Component;

/// <summary>
/// On the extraction step entity. Completing the step ejects the rider alive
/// at the patient's feet.
/// </summary>
[RegisterComponent]
public sealed partial class RiderSurgeryStepEffectComponent : Component;
