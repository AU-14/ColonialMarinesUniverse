using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.BodyPart.Events;

/// <summary>
///     Raised on a body-part entity when its HP fraction crosses the per-part
///     "low-HP" threshold upward.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartHealedEvent(
    EntityUid Body,
    EntityUid Part,
    BodyPartType Type,
    float PreviousFraction,
    float CurrentFraction,
    float ThresholdFraction);
