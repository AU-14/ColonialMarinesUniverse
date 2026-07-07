using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Injuries.Trauma;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;

[ByRefEvent]
public readonly record struct BodyPartDamagedEvent(
    EntityUid Body,
    EntityUid Part,
    BodyPartType Type,
    DamageSpecifier Delta,
    FixedPoint2 NewCurrent,
    IReadOnlyList<EntityUid> ContainedOrgans,
    EntityUid? Tool,
    DamageImpact Impact,
    CMUTraumaContactResult Trauma,
    TargetBodyZone? TargetZone = null);
