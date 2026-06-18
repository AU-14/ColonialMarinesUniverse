using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared._CMU14.Medical.Human.Data;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Targeting.Events;

/// <summary>
///     Raised on the target before damage application so the routing pipeline can decide
///     which body part absorbs the hit. Each handler in the pipeline runs in order and
///     can claim the resolution by setting <see cref="Handled"/>.
///     <see cref="ForcedSymmetry"/> tightens the forced step to a specific side when
///     the source supplied one. A null symmetry on a paired type picks the first match.
/// </summary>
[ByRefEvent]
public record struct HitLocationResolveEvent
{
    public readonly EntityUid Target;
    public readonly EntityUid? Attacker;
    public readonly DamageSpecifier Damage;
    public readonly BodyPartType? Forced;
    public readonly BodyPartSymmetry? ForcedSymmetry;
    public readonly BodyRegion ForcedRegion;

    public BodyPartType ResolvedPart;
    public EntityUid? ResolvedPartEntity;
    public BodyRegion ResolvedRegion;
    public bool Handled;

    public HitLocationResolveEvent(
        EntityUid target,
        EntityUid? attacker,
        DamageSpecifier damage,
        BodyPartType? forced,
        BodyPartSymmetry? forcedSymmetry = null,
        BodyRegion forcedRegion = BodyRegion.None)
    {
        Target = target;
        Attacker = attacker;
        Damage = damage;
        Forced = forced;
        ForcedSymmetry = forcedSymmetry;
        ForcedRegion = forcedRegion;
        ResolvedPart = BodyPartType.Other;
        ResolvedPartEntity = null;
        ResolvedRegion = BodyRegion.None;
        Handled = false;
    }
}
