using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Damage.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._CMU14.Medical.Human.Damage;

public sealed partial class HumanDamageSeveranceTriggerSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalDamageAppliedEvent>(OnDamageApplied);
    }

    private void OnDamageApplied(ref HumanMedicalDamageAppliedEvent args)
    {
        if (!LimbSeveranceThresholdRules.ShouldSever(
                args.Region,
                args.PreviousRegion,
                args.CurrentRegion,
                args.BruteDelta) ||
            !TryResolveSeverablePart(args.Body, args, out var part, out var type))
        {
            TrySeverAnyOverThresholdRegion(args.Body, args.BruteDelta);
            return;
        }

        var ev = new BodyPartSeveredEvent(args.Body, part, type);
        RaiseLocalEvent(part, ref ev);
    }

    private bool TryResolveSeverablePart(
        EntityUid body,
        HumanMedicalDamageAppliedEvent args,
        out EntityUid part,
        out BodyPartType type)
    {
        part = default;
        type = default;

        if (!TryGetPartTarget(args.Region, out type, out var symmetry))
            return false;

        if (args.Part is { } resolvedPart &&
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

    private void TrySeverAnyOverThresholdRegion(
        EntityUid body,
        FixedPoint2 bruteDelta)
    {
        if (bruteDelta <= FixedPoint2.Zero)
            return;

        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return;

        foreach (var region in medical.Regions)
        {
            if (!LimbSeveranceThresholdRules.ShouldSever(
                    region.Region,
                    region,
                    region,
                    bruteDelta))
            {
                continue;
            }

            var args = new HumanMedicalDamageAppliedEvent(
                body,
                region.Region,
                default,
                null,
                bruteDelta,
                default,
                region,
                region,
                null,
                default);
            if (!TryResolveSeverablePart(body, args, out var part, out var type))
                continue;

            var ev = new BodyPartSeveredEvent(body, part, type);
            RaiseLocalEvent(part, ref ev);
            return;
        }
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
}
