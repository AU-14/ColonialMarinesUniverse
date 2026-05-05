using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.BodyPart.Events;

/// <summary>
///     Raised on the target body AFTER <see cref="HitLocationResolveEvent"/> resolution
///     completes. Distinct from the resolve event so handlers can't accidentally
///     mutate the resolution mid-stream.
/// </summary>
[ByRefEvent]
public readonly record struct HitLocationResolvedEvent(
    EntityUid Body,
    EntityUid? Attacker,
    BodyPartType ResolvedPart,
    EntityUid? ResolvedPartEntity);
