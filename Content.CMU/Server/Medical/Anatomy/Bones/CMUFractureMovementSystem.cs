using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Anatomy.Bones;

/// <summary>
///     Turns movement with an unstabilized fracture into the internal bleeding
///     and region-specific organ trauma used by CM-style medicine.
/// </summary>
public sealed partial class CMUFractureMovementSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedFractureSystem _fractures = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;

    private const float MinimumMoveDistance = 0.05f;

    private readonly List<EntityUid> _organCandidates = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, MoveEvent>(OnMove);
    }

    private void OnMove(Entity<BodyComponent> ent, ref MoveEvent args)
    {
        if (!HasComp<CMUHumanMedicalComponent>(ent) ||
            !_cfg.GetCVar(CMUMedicalCCVars.Enabled) ||
            !_cfg.GetCVar(CMUMedicalCCVars.BoneEnabled))
        {
            return;
        }

        if (_standing.IsDown(ent) || args.ParentChanged || args.OldPosition == args.NewPosition)
            return;
        if (!args.NewPosition.TryDistance(EntityManager, _transform, args.OldPosition, out var distance) ||
            distance <= MinimumMoveDistance)
        {
            return;
        }

        if (TryComp<MobStateComponent>(ent, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        var now = _timing.CurTime;
        foreach (var part in _medicalIndex.GetBodyParts(ent))
        {
            if (!TryComp<FractureComponent>(part, out var fracture))
                continue;

            var severity = _fractures.GetEffectiveSeverity((part.Owner, fracture));
            if (severity == FractureSeverity.None || fracture.NextMovementComplication > now)
                continue;

            var profile = FractureProfile.Get(severity);
            _fractures.SetNextMovementComplication(
                (part.Owner, fracture),
                now + TimeSpan.FromSeconds(profile.MovementCheckCooldownSeconds));

            var causeInternalBleeding = _random.Prob(profile.MovementInternalBleedChance);
            var causeOrganDamage = _cfg.GetCVar(CMUMedicalCCVars.OrganEnabled) &&
                                   _random.Prob(profile.MovementOrganDamageChance);
            if (!causeInternalBleeding && !causeOrganDamage)
                continue;

            ApplyMovementConsequences(
                ent.Owner,
                part.Owner,
                fracture,
                causeInternalBleeding,
                causeOrganDamage);
        }
    }

    /// <summary>
    ///     Applies already-resolved fracture consequences. Separating the rolls
    ///     from the mutation keeps the damage path reusable and deterministic.
    /// </summary>
    public void ApplyMovementConsequences(
        EntityUid body,
        EntityUid part,
        FractureComponent fracture,
        bool causeInternalBleeding,
        bool causeOrganDamage)
    {
        var profile = FractureProfile.Get(fracture.Severity);
        var applied = false;

        if (causeInternalBleeding && profile.MovementInternalBleedRate > 0f)
        {
            _wounds.SeedInternalBleed(part, "fracture-movement", profile.MovementInternalBleedRate);
            applied = true;
        }

        if (causeOrganDamage &&
            profile.MovementOrganDamage > FixedPoint2.Zero &&
            TryPickRegionalOrgan(part, fracture, out var organ))
        {
            var damage = new DamageSpecifier
            {
                DamageDict = { ["Blunt"] = profile.MovementOrganDamage },
            };
            var ev = new OrganDamagedEvent(body, organ, damage, OrganDamageSource.RibFracture);
            RaiseLocalEvent(organ, ref ev, broadcast: true);
            applied = true;
        }

        if (applied)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-medical-fracture-movement-complication"),
                body,
                body,
                PopupType.MediumCaution);
        }
    }

    private bool TryPickRegionalOrgan(
        EntityUid part,
        FractureComponent fracture,
        out EntityUid organ)
    {
        organ = default;
        if (!TryComp<BodyPartComponent>(part, out var bodyPart))
            return false;

        var zone = fracture.SourceZone ?? bodyPart.PartType switch
        {
            BodyPartType.Head => TargetBodyZone.Head,
            BodyPartType.Torso => TargetBodyZone.Chest,
            _ => (TargetBodyZone?) null,
        };
        if (zone is null)
            return false;

        _organCandidates.Clear();
        foreach (var candidate in _medicalIndex.GetPartOrgans(part))
        {
            if (!HasComp<OrganHealthComponent>(candidate) || !MatchesRegion(candidate, zone.Value))
                continue;

            _organCandidates.Add(candidate);
        }

        if (_organCandidates.Count == 0)
            return false;

        organ = _random.Pick(_organCandidates);
        return true;
    }

    private bool MatchesRegion(EntityUid organ, TargetBodyZone zone)
    {
        return zone switch
        {
            TargetBodyZone.Head => HasComp<CMUBrainComponent>(organ) || HasComp<EyesComponent>(organ),
            TargetBodyZone.GroinPelvis => HasComp<KidneysComponent>(organ),
            TargetBodyZone.Chest => HasComp<HeartComponent>(organ) ||
                                    HasComp<LungsComponent>(organ) ||
                                    HasComp<LiverComponent>(organ),
            _ => false,
        };
    }
}
