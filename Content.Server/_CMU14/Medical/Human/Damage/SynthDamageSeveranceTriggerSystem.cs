using System.Collections.Generic;
using Content.Server._CMU14.Medical.Presentation;
using Content.Shared._CMU14.Medical.Targeting.Events;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Damage.Events;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Server._CMU14.Medical.Human.Damage;

public sealed partial class SynthDamageSeveranceTriggerSystem : EntitySystem
{
    [Dependency] private CMUBloodDecalSystem _bloodDecals = default!;
    [Dependency] private SharedBodySystem _body = default!;

    private readonly Dictionary<EntityUid, ResolvedHit> _pendingHits = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SynthComponent, HitLocationResolvedEvent>(OnHitLocationResolved);
        SubscribeLocalEvent<SynthComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnHitLocationResolved(Entity<SynthComponent> ent, ref HitLocationResolvedEvent args)
    {
        _pendingHits[ent.Owner] = new ResolvedHit(
            args.ResolvedPart,
            args.ResolvedPartEntity,
            args.ResolvedRegion);
    }

    private void OnDamageChanged(Entity<SynthComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } damage)
        {
            _pendingHits.Remove(ent.Owner);
            return;
        }

        _bloodDecals.TryAddSynthDamageBlood(ent.Owner, damage);

        var brute = ExtractBruteDamage(damage);
        if (brute <= FixedPoint2.Zero)
            return;

        var hit = ConsumeResolvedHit(ent.Owner);
        var region = hit.Region != BodyRegion.None
            ? hit.Region
            : ResolveBodyRegion(
                hit.PartType,
                hit.Part is { } partUid && TryComp<AnatomyRegionComponent>(partUid, out var anatomy)
                    ? anatomy
                    : null);

        var threshold = LimbSeveranceThresholdRules.GetThreshold(region);
        if (threshold <= FixedPoint2.Zero ||
            brute < threshold ||
            !TryResolveSeverablePart(ent.Owner, hit, region, out var part, out var type))
        {
            return;
        }

        var ev = new BodyPartSeveredEvent(ent.Owner, part, type);
        RaiseLocalEvent(part, ref ev);
    }

    private bool TryResolveSeverablePart(
        EntityUid body,
        ResolvedHit hit,
        BodyRegion region,
        out EntityUid part,
        out BodyPartType type)
    {
        part = default;
        type = default;

        if (!TryGetPartTarget(region, out type, out var symmetry))
            return false;

        if (hit.Part is { } resolvedPart &&
            TryComp<BodyPartComponent>(resolvedPart, out var resolved) &&
            resolved.PartType == type &&
            resolved.Symmetry == symmetry)
        {
            part = resolvedPart;
            return true;
        }

        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return false;

        foreach (var (candidate, candidatePart) in _body.GetBodyChildren(body, bodyComp))
        {
            if (candidatePart.PartType != type ||
                candidatePart.Symmetry != symmetry)
            {
                continue;
            }

            part = candidate;
            return true;
        }

        return false;
    }

    private ResolvedHit ConsumeResolvedHit(EntityUid body)
    {
        if (_pendingHits.Remove(body, out var hit))
            return hit;

        return new ResolvedHit(BodyPartType.Torso, null, BodyRegion.None);
    }

    private static BodyRegion ResolveBodyRegion(
        BodyPartType partType,
        AnatomyRegionComponent? anatomy)
    {
        if (anatomy is { Region: not BodyRegion.None })
            return anatomy.Region;

        return partType switch
        {
            BodyPartType.Head => BodyRegion.Head,
            BodyPartType.Torso => BodyRegion.Chest,
            BodyPartType.Arm => BodyRegion.LeftArm,
            BodyPartType.Hand => BodyRegion.LeftHand,
            BodyPartType.Leg => BodyRegion.LeftLeg,
            BodyPartType.Foot => BodyRegion.LeftFoot,
            _ => BodyRegion.Chest,
        };
    }

    private static bool TryGetPartTarget(
        BodyRegion region,
        out BodyPartType type,
        out BodyPartSymmetry symmetry)
    {
        switch (region)
        {
            case BodyRegion.Head:
                type = BodyPartType.Head;
                symmetry = BodyPartSymmetry.None;
                return true;
            case BodyRegion.LeftArm:
                type = BodyPartType.Arm;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case BodyRegion.RightArm:
                type = BodyPartType.Arm;
                symmetry = BodyPartSymmetry.Right;
                return true;
            case BodyRegion.LeftHand:
                type = BodyPartType.Hand;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case BodyRegion.RightHand:
                type = BodyPartType.Hand;
                symmetry = BodyPartSymmetry.Right;
                return true;
            case BodyRegion.LeftLeg:
                type = BodyPartType.Leg;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case BodyRegion.RightLeg:
                type = BodyPartType.Leg;
                symmetry = BodyPartSymmetry.Right;
                return true;
            case BodyRegion.LeftFoot:
                type = BodyPartType.Foot;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case BodyRegion.RightFoot:
                type = BodyPartType.Foot;
                symmetry = BodyPartSymmetry.Right;
                return true;
            default:
                type = default;
                symmetry = default;
                return false;
        }
    }

    private static FixedPoint2 ExtractBruteDamage(DamageSpecifier damage)
    {
        return GetPositiveDamageType(damage, "Blunt") +
               GetPositiveDamageType(damage, "Slash") +
               GetPositiveDamageType(damage, "Piercing");
    }

    private static FixedPoint2 GetPositiveDamageType(DamageSpecifier damage, string type)
    {
        return damage.DamageDict.TryGetValue(type, out var value) && value > FixedPoint2.Zero
            ? value
            : FixedPoint2.Zero;
    }

    private readonly record struct ResolvedHit(
        BodyPartType PartType,
        EntityUid? Part,
        BodyRegion Region);
}
