using Content.Shared.CMU14.Medical.Injuries.Trauma;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds.Events;

[ByRefEvent]
public readonly record struct BodyPartWoundAppliedEvent(
    EntityUid Body,
    EntityUid Part,
    BodyPartType Type,
    DamageSpecifier Delta,
    EntityUid? Tool,
    DamageImpact Impact,
    CMUTraumaContactResult Trauma);
