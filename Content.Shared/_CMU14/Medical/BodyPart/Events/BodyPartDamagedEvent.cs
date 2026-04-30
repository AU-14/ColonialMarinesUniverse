using System.Collections.Generic;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.BodyPart.Events;

[ByRefEvent]
public readonly record struct BodyPartDamagedEvent(
    EntityUid Body,
    EntityUid Part,
    BodyPartType Type,
    DamageSpecifier Delta,
    FixedPoint2 NewCurrent,
    IReadOnlyList<EntityUid> ContainedOrgans);
