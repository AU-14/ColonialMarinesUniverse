using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Injuries.Pain;

/// <summary>
///     Marks patients whose current pain tier can produce periodic feedback.
/// </summary>
[RegisterComponent]
public sealed partial class CMUPainFeedbackActiveComponent : Component;
