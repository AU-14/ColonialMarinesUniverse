using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.BodyPart.Events;

/// <summary>
///     Raised when part HP crosses a pain-source boundary such as 25% or 10%.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartPainThresholdCrossedEvent(
    EntityUid Body,
    EntityUid Part,
    BodyPartType Type,
    float PreviousFraction,
    float CurrentFraction,
    float ThresholdFraction);
