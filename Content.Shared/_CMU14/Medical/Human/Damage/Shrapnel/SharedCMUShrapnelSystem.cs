using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared.Buckle.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Human.Damage.Shrapnel;

public sealed partial class SharedCMUShrapnelSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHumanMedicalSystem _humanMedical = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    private const float PainTargetCap = 70f;
    private const float MovementDistanceThreshold = 0.75f;
    private const float MovementPulseCooldownSeconds = 1.25f;
    private const float MinimumMoveDistance = 0.05f;
    private const float HighForceShrapnelExposure = 0.62f;
    private const float ExplosionShrapnelChanceStart = 0.35f;
    private const float ExplosionShrapnelGuaranteed = 0.85f;
    private const float ExplosionDeepShrapnelExposure = 0.62f;
    private const float FragmentingExplosionGuaranteedExposure = 0.62f;
    private const float ExplosionSurgicalShrapnelExposure = 0.88f;
    private const int MaxExplosionFragments = 8;
    private const int DefaultSurgicalRemoveCount = 1;
    private const ForeignObjectDepth ManualExtractionMaxDepth = ForeignObjectDepth.Deep;
    private static readonly ProtoId<DamageTypePrototype> DefaultSurgicalDamageType = "Piercing";

    private readonly Dictionary<EntityUid, float> _movementAccumulators = new();
    private readonly Dictionary<EntityUid, TimeSpan> _movementPainCooldowns = new();

    private bool _medicalEnabled;
    private bool _painEnabled;

    public readonly record struct WeightedBodyPart(
        EntityUid Part,
        BodyPartType Type,
        BodyPartSymmetry Symmetry,
        float Weight);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalComponent, GetVerbsEvent<InteractionVerb>>(OnGetShrapnelVerbs);
        SubscribeLocalEvent<DamageableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetShrapnelAltVerbs);
        SubscribeLocalEvent<CMUShrapnelExtractorComponent, UseInHandEvent>(OnExtractorUseInHand);
        SubscribeLocalEvent<CMUShrapnelExtractorComponent, CMUShrapnelExtractDoAfterEvent>(OnExtractorDoAfter);
        SubscribeLocalEvent<ActiveEmbeddedObjectMovementComponent, MoveEvent>(OnEmbeddedObjectMove);
        SubscribeLocalEvent<HumanMedicalStartupEvent>(OnHumanMedicalStartup);
        SubscribeLocalEvent<HumanMedicalShutdownEvent>(OnHumanMedicalShutdown);
        SubscribeLocalEvent<HumanMedicalComponent, ComponentRemove>(OnHumanRemove);
        SubscribeLocalEvent<HumanMedicalComponent, HumanMedicalDamageAppliedEvent>(OnHumanMedicalDamageApplied);

        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.PainEnabled, v => _painEnabled = v, true);
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled;
    }

    public static float GetPainTarget(ForeignObjectRecord shrapnel)
    {
        if (shrapnel.Fragments <= 0 || shrapnel.Severity <= 0f)
            return 0f;

        var fragmentPressure = 4f + shrapnel.Fragments * 3.5f;
        return MathF.Min(PainTargetCap, MathF.Max(shrapnel.Severity, fragmentPressure));
    }

    public static FixedPoint2 GetMovementDamage(ForeignObjectRecord shrapnel)
    {
        return HumanMovementDebuffRules.GetEmbeddedObjectMovementDamage(
            new EmbeddedObjectMovementInput(
                shrapnel.Fragments,
                shrapnel.MoveDamage,
                shrapnel.MoveDamagePerFragment,
                shrapnel.Flags.HasFlag(ForeignObjectFlags.CanExplode),
                shrapnel.ExplosionChance));
    }

    public bool AddShrapnel(
        EntityUid body,
        BodyRegion region,
        int fragments,
        float severity,
        FixedPoint2 moveDamage = default,
        FixedPoint2 moveDamagePerFragment = default,
        bool canExplode = false,
        float explosionChance = 0f,
        ForeignObjectDepth removalDepth = ForeignObjectDepth.Surface)
    {
        if (fragments <= 0 || severity <= 0f)
            return false;

        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return false;

        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddForeignObject(
            region,
            ForeignObjectKind.Shrapnel,
            fragments,
            severity,
            removalDepth,
            moveDamage,
            moveDamagePerFragment,
            canExplode ? ForeignObjectFlags.CanExplode : ForeignObjectFlags.None,
            explosionChance));

        var result = _humanMedical.ApplyTransaction((body, medical), transaction);
        if (!result.Applied)
            return false;

        RaiseShrapnelChanged(body, region, removed: false);
        return true;
    }

    public int TryApplyExplosionShrapnel(
        EntityUid body,
        ProtoId<ExplosionPrototype> explosion,
        float exposure,
        IReadOnlyList<WeightedBodyPart> weightedParts)
    {
        if (!IsLayerEnabled())
            return 0;

        if (!HasComp<HumanMedicalComponent>(body))
            return 0;

        if (weightedParts.Count == 0)
            return 0;

        if (!IsShrapnelCapable(explosion, exposure))
            return 0;

        var chance = GetExplosionShrapnelChance(explosion, exposure);
        if (chance <= 0f)
            return 0;

        if (!IsGuaranteedExplosionShrapnel(explosion, exposure) &&
            chance < 1f &&
            !_random.Prob(chance))
        {
            return 0;
        }

        var desiredFragments = Math.Clamp((int) MathF.Ceiling(exposure * MaxExplosionFragments), 1, MaxExplosionFragments);
        var removalDepth = GetExplosionShrapnelDepth(explosion, exposure);
        var applied = 0;
        for (var i = 0; i < weightedParts.Count && applied < desiredFragments; i++)
        {
            var weighted = weightedParts[i];
            if (weighted.Weight <= 0f)
                continue;

            var region = ResolveWeightedRegion(weighted);
            if (region == BodyRegion.None)
                continue;

            var partFragments = Math.Clamp((int) MathF.Ceiling(desiredFragments * weighted.Weight), 1, desiredFragments - applied);
            var severity = 8f + exposure * 34f * MathF.Max(0.35f, weighted.Weight);
            if (AddShrapnel(
                    body,
                    region,
                    partFragments,
                    severity,
                    removalDepth: removalDepth))
            {
                applied += partFragments;
            }
        }

        return applied;
    }

    private static float GetExplosionShrapnelChance(
        ProtoId<ExplosionPrototype> explosion,
        float exposure)
    {
        if (exposure >= ExplosionShrapnelGuaranteed)
            return 1f;

        if (IsFragmentingExplosion(explosion))
        {
            if (exposure >= FragmentingExplosionGuaranteedExposure)
                return 1f;

            return Math.Clamp(0.35f + exposure * 0.75f, 0.25f, 0.95f);
        }

        return Math.Clamp(
            (exposure - ExplosionShrapnelChanceStart) /
            (ExplosionShrapnelGuaranteed - ExplosionShrapnelChanceStart),
            0f,
            1f);
    }

    private static ForeignObjectDepth GetExplosionShrapnelDepth(
        ProtoId<ExplosionPrototype> explosion,
        float exposure)
    {
        if (exposure >= ExplosionSurgicalShrapnelExposure ||
            explosion == "RMCOB" ||
            explosion == "RMCOBXenoTunnel")
        {
            return ForeignObjectDepth.Surgical;
        }

        if (exposure >= ExplosionDeepShrapnelExposure ||
            explosion == "RMCMortar" ||
            explosion == "Minibomb" ||
            explosion == "HardBomb")
        {
            return ForeignObjectDepth.Deep;
        }

        return ForeignObjectDepth.Surface;
    }

    private static bool IsGuaranteedExplosionShrapnel(
        ProtoId<ExplosionPrototype> explosion,
        float exposure)
    {
        return exposure >= ExplosionShrapnelGuaranteed ||
            IsFragmentingExplosion(explosion) && exposure >= FragmentingExplosionGuaranteedExposure;
    }

    public bool TryExtractShrapnel(
        EntityUid body,
        Entity<CMUShrapnelExtractorComponent> tool,
        out int removed,
        EntityUid? user = null,
        BodyRegion preferredRegion = BodyRegion.None)
    {
        removed = 0;
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return false;

        if (!TryFindExtractionRecord(medical, out var shrapnel, user, preferredRegion, ManualExtractionMaxDepth))
            return false;

        var removeCount = Math.Max(1, tool.Comp.RemoveCount);
        var damageOnExtract = tool.Comp.DamageOnExtract;
        var painPenalty = tool.Comp.PainPenalty;

        if (shrapnel.Depth == ForeignObjectDepth.Deep)
        {
            removeCount = 1;
            damageOnExtract *= (FixedPoint2) tool.Comp.DeepDamageMultiplier;
            painPenalty *= tool.Comp.DeepPainMultiplier;
        }

        return TryExtractShrapnelRecord(
            body,
            medical,
            shrapnel,
            removeCount,
            damageOnExtract,
            tool.Comp.DamageType,
            painPenalty,
            tool.Owner,
            out removed);
    }

    public bool TryFindSurgicalShrapnelInRegion(
        EntityUid body,
        BodyRegion region,
        out ForeignObjectDepth depth)
    {
        depth = ForeignObjectDepth.Surface;
        if (!TryComp<HumanMedicalComponent>(body, out var medical) ||
            !TryFindSurgicalShrapnelRecord(medical, region, out var shrapnel))
        {
            return false;
        }

        depth = shrapnel.Depth;
        return true;
    }

    public bool TryExtractSurgicalShrapnelFromRegion(
        EntityUid body,
        BodyRegion region,
        EntityUid tool,
        out int removed,
        EntityUid? user = null)
    {
        removed = 0;
        if (!TryComp<HumanMedicalComponent>(body, out var medical) ||
            !TryFindSurgicalShrapnelRecord(medical, region, out var shrapnel))
        {
            return false;
        }

        if (TryComp<CMUShrapnelExtractorComponent>(tool, out var extractor))
        {
            return TryExtractShrapnelRecord(
                body,
                medical,
                shrapnel,
                Math.Max(1, extractor.RemoveCount),
                extractor.DamageOnExtract,
                extractor.DamageType,
                extractor.PainPenalty,
                tool,
                out removed);
        }

        return TryExtractShrapnelRecord(
            body,
            medical,
            shrapnel,
            DefaultSurgicalRemoveCount,
            FixedPoint2.Zero,
            DefaultSurgicalDamageType,
            0f,
            tool,
            out removed);
    }

    public bool TryClearShrapnel(EntityUid body, BodyRegion region)
    {
        if (!TryComp<HumanMedicalComponent>(body, out var medical) ||
            !TryFindBestShrapnelInRegion(medical, region, ForeignObjectDepth.Surgical, out var shrapnel))
        {
            return false;
        }

        return TryExtractShrapnelRecord(
            body,
            medical,
            shrapnel,
            shrapnel.Fragments,
            FixedPoint2.Zero,
            DefaultSurgicalDamageType,
            0f,
            body,
            out _);
    }

    public float ComputeMovementPainPulse(EntityUid body)
    {
        if (!_painEnabled)
            return 0f;

        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return 0f;

        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return 0f;

        return ComputeLedgerMovementPainPulse(medical);
    }

    public static float ComputeLedgerMovementPainPulse(HumanMedicalComponent medical)
    {
        var pulse = 0f;
        foreach (var foreignObject in medical.ForeignObjects)
        {
            if (foreignObject.Kind != ForeignObjectKind.Shrapnel || !foreignObject.Active)
                continue;

            pulse += GetMovementDamage(foreignObject).Float();
        }

        pulse += MovementRegionPainPulse(medical.Regions[(int) BodyRegion.LeftLeg]);
        pulse += MovementRegionPainPulse(medical.Regions[(int) BodyRegion.RightLeg]);
        pulse += MovementRegionPainPulse(medical.Regions[(int) BodyRegion.LeftFoot]);
        pulse += MovementRegionPainPulse(medical.Regions[(int) BodyRegion.RightFoot]);

        return MathF.Min(35f, pulse);
    }

    public (int Fragments, float Severity) GetTotalShrapnel(EntityUid body)
    {
        var fragments = 0;
        var severity = 0f;
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return (fragments, severity);

        foreach (var shrapnel in medical.ForeignObjects)
        {
            if (shrapnel.Kind != ForeignObjectKind.Shrapnel || !shrapnel.Active)
                continue;

            fragments += Math.Max(0, shrapnel.Fragments);
            severity += Math.Max(0f, shrapnel.Severity);
        }

        return (fragments, severity);
    }

    private void OnGetShrapnelVerbs(Entity<HumanMedicalComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!IsLayerEnabled() || !args.CanAccess || !args.CanInteract)
            return;

        if (args.Using is not { } tool || !TryComp<CMUShrapnelExtractorComponent>(tool, out var extractor))
            return;

        if (!TryFindExtractionRecord(ent.Comp, out var shrapnel, args.User, maxDepth: ManualExtractionMaxDepth))
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => StartExtraction(user, target, tool, shrapnel.Region),
            Text = Loc.GetString("cmu-medical-shrapnel-extract-verb"),
            Icon = extractor.VerbIcon,
        });
    }

    private void OnGetShrapnelAltVerbs(Entity<DamageableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsLayerEnabled() || !args.CanAccess || !args.CanInteract)
            return;

        if (!HasComp<HumanMedicalComponent>(ent.Owner))
            return;

        if (args.Using is not { } tool || !TryComp<CMUShrapnelExtractorComponent>(tool, out var extractor))
            return;

        if (!TryComp<HumanMedicalComponent>(ent.Owner, out var medical))
            return;

        if (!TryFindExtractionRecord(medical, out var shrapnel, args.User, maxDepth: ManualExtractionMaxDepth))
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartExtraction(user, target, tool, shrapnel.Region),
            Text = Loc.GetString("cmu-medical-shrapnel-extract-verb"),
            Icon = extractor.VerbIcon,
            Priority = 10,
        });
    }

    private void OnExtractorUseInHand(Entity<CMUShrapnelExtractorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !IsLayerEnabled())
            return;

        if (!HasComp<HumanMedicalComponent>(args.User))
            return;

        if (!TryComp<HumanMedicalComponent>(args.User, out var medical))
            return;

        if (!TryFindExtractionRecord(medical, out var shrapnel, args.User, maxDepth: ManualExtractionMaxDepth))
            return;

        args.Handled = true;
        StartExtraction(args.User, args.User, ent.Owner, shrapnel.Region);
    }

    public bool TryStartExtraction(EntityUid user, EntityUid target, EntityUid tool, EntityUid? preferredPart = null)
    {
        if (!IsLayerEnabled() ||
            !HasComp<HumanMedicalComponent>(target) ||
            !HasComp<CMUShrapnelExtractorComponent>(tool))
        {
            return false;
        }

        var preferredRegion = ResolvePreferredRegion(target, preferredPart);
        if (!TryComp<HumanMedicalComponent>(target, out var medical) ||
            !TryFindExtractionRecord(medical, out var shrapnel, user, preferredRegion, ManualExtractionMaxDepth))
        {
            return false;
        }

        StartExtraction(user, target, tool, shrapnel.Region);
        return true;
    }

    private void StartExtraction(EntityUid user, EntityUid target, EntityUid tool, BodyRegion selectedRegion)
    {
        if (!IsLayerEnabled() || !TryComp<HumanMedicalComponent>(target, out var medical))
            return;

        if (!TryComp<CMUShrapnelExtractorComponent>(tool, out var extractor))
            return;

        if (!TryFindExtractionRecord(medical, out var shrapnel, user, selectedRegion, ManualExtractionMaxDepth))
        {
            _popup.PopupPredicted(Loc.GetString("cmu-medical-shrapnel-none"), target, user);
            return;
        }

        var ev = new CMUShrapnelExtractDoAfterEvent { PreSelectedRegion = shrapnel.Region };
        var delay = GetExtractionDelay(extractor, user, target, shrapnel);
        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, tool, target: target, used: tool)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupPredicted(Loc.GetString("cmu-medical-shrapnel-extract-start"), target, user);
    }

    private void OnExtractorDoAfter(Entity<CMUShrapnelExtractorComponent> ent, ref CMUShrapnelExtractDoAfterEvent args)
    {
        var preSelectedRegion = args.PreSelectedRegion;
        args.Repeat = false;
        args.PreSelectedRegion = BodyRegion.None;

        if (args.Cancelled || args.Target is not { } target)
            return;

        if (!TryComp<HumanMedicalComponent>(target, out var medical))
            return;

        if (TryExtractShrapnel(target, ent, out var removed, args.User, preSelectedRegion))
        {
            _popup.PopupPredicted(
                Loc.GetString("cmu-medical-shrapnel-extract-finish", ("count", removed)),
                target,
                args.User);
            _audio.PlayPredicted(ent.Comp.ExtractSound, target, args.User);

            if (TryFindExtractionRecord(medical, out var nextShrapnel, args.User, maxDepth: ManualExtractionMaxDepth))
            {
                args.PreSelectedRegion = nextShrapnel.Region;
                args.Repeat = true;
            }
        }
    }

    private void OnEmbeddedObjectMove(Entity<ActiveEmbeddedObjectMovementComponent> ent, ref MoveEvent args)
    {
        if (_net.IsClient || _timing.ApplyingState || !IsLayerEnabled())
            return;

        if (args.ParentChanged || args.OldPosition == args.NewPosition)
            return;

        if (!args.NewPosition.TryDistance(EntityManager, _transform, args.OldPosition, out var distance))
            return;

        if (distance <= MinimumMoveDistance)
            return;

        _movementAccumulators.TryGetValue(ent.Owner, out var accumulated);
        accumulated += (float) distance;
        if (accumulated < MovementDistanceThreshold)
        {
            _movementAccumulators[ent.Owner] = accumulated;
            return;
        }

        _movementAccumulators[ent.Owner] = accumulated % MovementDistanceThreshold;
        if (!TryComp<HumanMedicalComponent>(ent.Owner, out var medical))
            return;

        if (!IsEligibleForEmbeddedObjectMovement(ent.Owner))
            return;

        var pulse = ApplyEmbeddedObjectMovement(ent.Owner, medical, out var jostledRegion);
        if (pulse <= FixedPoint2.Zero)
            return;

        if (!CanPulseMovementPain(ent.Owner))
            return;

        _pain.AddPainPulse(ent.Owner, pulse);
        _pain.OnRecomputeTrigger(ent.Owner);
        if (jostledRegion != BodyRegion.None &&
            _random.Prob(HumanMovementDebuffRules.ShrapnelJostlePopupChance))
        {
            _pain.TryCustomPain(
                ent.Owner,
                "cmu-medical-shrapnel-movement-jostle",
                1,
                ("part", GetRegionName(jostledRegion)));
            return;
        }

        _pain.TryCustomPain(ent.Owner, "cmu-medical-shrapnel-movement-pain");
    }

    private void OnHumanMedicalStartup(ref HumanMedicalStartupEvent args)
    {
        RefreshEmbeddedMovementMarker(args.Body);
    }

    private void OnHumanMedicalShutdown(ref HumanMedicalShutdownEvent args)
    {
        _movementAccumulators.Remove(args.Body);
        _movementPainCooldowns.Remove(args.Body);
        RemCompDeferred<ActiveEmbeddedObjectMovementComponent>(args.Body);
    }

    private void OnHumanRemove(Entity<HumanMedicalComponent> ent, ref ComponentRemove args)
    {
        _movementAccumulators.Remove(ent.Owner);
        _movementPainCooldowns.Remove(ent.Owner);
        RemCompDeferred<ActiveEmbeddedObjectMovementComponent>(ent.Owner);
    }

    private void OnHumanMedicalDamageApplied(Entity<HumanMedicalComponent> ent, ref HumanMedicalDamageAppliedEvent args)
    {
        if (!IsLayerEnabled())
            return;

        if (args.Tool is not { } tool ||
            !TryComp<CMUProjectileShrapnelComponent>(tool, out var projectile))
        {
            return;
        }

        if (args.CurrentRegion.Presence != LimbPresence.Present)
            return;

        AddShrapnel(
            ent.Owner,
            args.Region,
            projectile.Fragments,
            projectile.Severity,
            projectile.MoveDamage,
            projectile.MoveDamagePerFragment,
            projectile.CanExplode,
            projectile.ExplosionChance,
            projectile.RemovalDepth);
    }

    private FixedPoint2 ApplyEmbeddedObjectMovement(
        EntityUid body,
        HumanMedicalComponent medical,
        out BodyRegion jostledRegion)
    {
        jostledRegion = BodyRegion.None;
        MedicalTransaction? transaction = null;
        var totalPain = FixedPoint2.Zero;
        var highestSeverity = 0f;

        foreach (var shrapnel in medical.ForeignObjects)
        {
            if (shrapnel.Kind != ForeignObjectKind.Shrapnel || !shrapnel.Active)
                continue;

            var damage = GetMovementDamage(shrapnel);
            if (damage <= FixedPoint2.Zero)
                continue;

            var region = shrapnel.Region;
            if (region == BodyRegion.None)
                continue;

            transaction ??= new MedicalTransaction(region);
            transaction.Add(MedicalEffect.AddRegionDamage(region, damage, FixedPoint2.Zero));
            totalPain += damage;
            if (shrapnel.Severity > highestSeverity)
            {
                highestSeverity = shrapnel.Severity;
                jostledRegion = region;
            }

            var input = GetEmbeddedObjectInput(shrapnel);
            if (!HumanMovementDebuffRules.ShouldEmbeddedObjectExplode(input, _random.NextFloat()))
                continue;

            var ev = new CMUShrapnelMovementDetonationAttemptEvent(body, region);
            RaiseLocalEvent(body, ref ev);
            if (!ev.Handled)
            {
                _popup.PopupEntity(
                    Loc.GetString("cmu-medical-shrapnel-movement-detonation"),
                    body,
                    body,
                    PopupType.LargeCaution);
            }
        }

        if (transaction is not { Count: > 0 })
            return totalPain;

        var result = _humanMedical.ApplyTransaction((body, medical), transaction);
        if (!result.Applied)
            jostledRegion = BodyRegion.None;

        return totalPain;
    }

    private bool IsEligibleForEmbeddedObjectMovement(EntityUid body)
    {
        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return false;

        if (_standing.IsDown(body))
            return false;

        return !TryComp<BuckleComponent>(body, out var buckle) || !buckle.Buckled;
    }

    private void RefreshEmbeddedMovementMarker(EntityUid body)
    {
        if (TryComp<HumanMedicalComponent>(body, out var medical) && HasEmbeddedObjects(medical))
        {
            EnsureComp<ActiveEmbeddedObjectMovementComponent>(body);
            return;
        }

        RemCompDeferred<ActiveEmbeddedObjectMovementComponent>(body);
    }

    private static bool HasEmbeddedObjects(HumanMedicalComponent medical)
    {
        foreach (var foreignObject in medical.ForeignObjects)
        {
            if (foreignObject.Kind == ForeignObjectKind.Shrapnel && foreignObject.Active)
                return true;
        }

        return false;
    }

    private EmbeddedObjectMovementInput GetEmbeddedObjectInput(ForeignObjectRecord shrapnel)
    {
        return new EmbeddedObjectMovementInput(
            shrapnel.Fragments,
            shrapnel.MoveDamage,
            shrapnel.MoveDamagePerFragment,
            shrapnel.Flags.HasFlag(ForeignObjectFlags.CanExplode),
            shrapnel.ExplosionChance);
    }

    private BodyRegion ResolveWeightedRegion(WeightedBodyPart weighted)
    {
        if (TryComp<AnatomyRegionComponent>(weighted.Part, out var anatomy) &&
            anatomy.Region != BodyRegion.None)
        {
            return anatomy.Region;
        }

        return RegionForPart(weighted.Type, weighted.Symmetry);
    }

    private static BodyRegion RegionForPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => BodyRegion.Head,
            (BodyPartType.Torso, _) => BodyRegion.Chest,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => BodyRegion.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => BodyRegion.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => BodyRegion.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => BodyRegion.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => BodyRegion.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => BodyRegion.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => BodyRegion.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => BodyRegion.RightFoot,
            _ => BodyRegion.None,
        };
    }

    private bool CanPulseMovementPain(EntityUid body)
    {
        var now = _timing.CurTime;
        if (_movementPainCooldowns.TryGetValue(body, out var next) && next > now)
            return false;

        _movementPainCooldowns[body] = now + TimeSpan.FromSeconds(MovementPulseCooldownSeconds);
        return true;
    }

    private bool TryFindExtractionRecord(
        HumanMedicalComponent medical,
        out ForeignObjectRecord shrapnel,
        EntityUid? user = null,
        BodyRegion preferredRegion = BodyRegion.None,
        ForeignObjectDepth maxDepth = ForeignObjectDepth.Surface)
    {
        shrapnel = default;

        if (preferredRegion != BodyRegion.None &&
            TryFindBestShrapnelInRegion(medical, preferredRegion, maxDepth, out shrapnel))
        {
            return true;
        }

        if (user is { } u
            && TryComp<BodyZoneTargetingComponent>(u, out var aim)
            && aim.LastSelectedAt > TimeSpan.Zero)
        {
            var (type, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
            var aimedRegion = RegionForPart(type, symmetry);
            if (aimedRegion != BodyRegion.None &&
                TryFindBestShrapnelInRegion(medical, aimedRegion, maxDepth, out shrapnel))
            {
                return true;
            }
        }

        var bestScore = 0f;
        foreach (var candidate in medical.ForeignObjects)
        {
            if (candidate.Kind != ForeignObjectKind.Shrapnel ||
                !candidate.Active ||
                candidate.Depth > maxDepth)
            {
                continue;
            }

            var score = GetPainTarget(candidate);
            if (score <= bestScore)
                continue;

            bestScore = score;
            shrapnel = candidate;
        }

        return bestScore > 0f;
    }

    private TimeSpan GetExtractionDelay(
        CMUShrapnelExtractorComponent extractor,
        EntityUid user,
        EntityUid target,
        ForeignObjectRecord shrapnel)
    {
        var multiplier = 1f;
        if (shrapnel.Depth == ForeignObjectDepth.Deep)
        {
            multiplier *= MathF.Max(0.1f, extractor.DeepDelayMultiplier);
        }

        if (user != target)
            multiplier *= Math.Clamp(extractor.AssistedDelayMultiplier, 0.1f, 1f);

        return TimeSpan.FromSeconds(extractor.Delay.TotalSeconds * multiplier);
    }

    private BodyRegion ResolvePreferredRegion(
        EntityUid body,
        EntityUid? preferredPart)
    {
        if (preferredPart is { } preferred &&
            TryComp<BodyPartComponent>(preferred, out var preferredBody) &&
            preferredBody.Body == body)
        {
            if (TryComp<AnatomyRegionComponent>(preferred, out var anatomy) &&
                anatomy.Region != BodyRegion.None)
            {
                return anatomy.Region;
            }

            return RegionForPart(preferredBody.PartType, preferredBody.Symmetry);
        }

        return BodyRegion.None;
    }

    private static bool TryFindSurgicalShrapnelRecord(
        HumanMedicalComponent medical,
        BodyRegion region,
        out ForeignObjectRecord shrapnel)
    {
        return TryFindBestShrapnelInRegion(medical, region, ForeignObjectDepth.Surgical, out shrapnel, ForeignObjectDepth.Deep);
    }

    private static bool TryFindBestShrapnelInRegion(
        HumanMedicalComponent medical,
        BodyRegion region,
        ForeignObjectDepth maxDepth,
        out ForeignObjectRecord shrapnel,
        ForeignObjectDepth minDepth = ForeignObjectDepth.Surface)
    {
        shrapnel = default;
        var bestScore = 0f;
        foreach (var candidate in medical.ForeignObjects)
        {
            if (candidate.Region != region ||
                candidate.Kind != ForeignObjectKind.Shrapnel ||
                !candidate.Active ||
                candidate.Depth < minDepth ||
                candidate.Depth > maxDepth)
            {
                continue;
            }

            var score = GetPainTarget(candidate);
            if (score <= bestScore)
                continue;

            bestScore = score;
            shrapnel = candidate;
        }

        return bestScore > 0f;
    }

    private bool TryExtractShrapnelRecord(
        EntityUid body,
        HumanMedicalComponent medical,
        ForeignObjectRecord shrapnel,
        int removeCount,
        FixedPoint2 damageOnExtract,
        ProtoId<DamageTypePrototype> damageType,
        float painPenalty,
        EntityUid tool,
        out int removed)
    {
        removed = 0;
        if (!shrapnel.Active)
            return false;

        removed = Math.Min(Math.Max(1, removeCount), shrapnel.Fragments);
        var transaction = new MedicalTransaction(shrapnel.Region);
        transaction.Add(MedicalEffect.RemoveForeignObject(shrapnel.Region, shrapnel.Id, removed));
        var result = _humanMedical.ApplyTransaction((body, medical), transaction);
        if (!result.Applied)
            return false;

        if (damageOnExtract > FixedPoint2.Zero)
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[damageType] = damageOnExtract;
            _damageable.TryChangeDamage(
                body,
                damage,
                interruptsDoAfters: false,
                origin: tool,
                tool: tool,
                impact: DamageImpact.SnaggingContact);
        }

        if (painPenalty > 0f)
            _pain.AddPainPulse(body, (FixedPoint2) painPenalty);

        RaiseShrapnelChanged(body, shrapnel.Region, removed: true);
        return true;
    }

    private void RaiseShrapnelChanged(EntityUid body, BodyRegion region, bool removed)
    {
        var ev = new CMUShrapnelChangedEvent(body, region, removed);
        RaiseLocalEvent(body, ref ev);
        _pain.OnRecomputeTrigger(body);
        RefreshEmbeddedMovementMarker(body);
    }

    private static float MovementRegionPainPulse(RegionState region)
    {
        if (!region.Skeletal.Broken)
            return 0f;

        return region.Skeletal.Splinted ? 4f : 14f;
    }

    private static bool IsShrapnelCapable(ProtoId<ExplosionPrototype> explosion, float exposure)
    {
        if (exposure >= HighForceShrapnelExposure)
            return true;

        return IsFragmentingExplosion(explosion)
            || explosion == "Minibomb"
            || explosion == "MicroBomb"
            || explosion == "HardBomb";
    }

    private static bool IsFragmentingExplosion(ProtoId<ExplosionPrototype> explosion)
    {
        return explosion == "Default"
            || explosion == "RMC"
            || explosion == "RMCMortar"
            || explosion == "RMCOB"
            || explosion == "RMCOBXenoTunnel";
    }

    private string GetRegionName(BodyRegion region)
    {
        return Loc.GetString(HumanMedicalScannerBuiSystem.GetRegionLocKey(region));
    }
}
