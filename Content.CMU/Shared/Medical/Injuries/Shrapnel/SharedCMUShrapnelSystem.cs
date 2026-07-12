using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Injuries.Trauma;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared.Body.Part;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Explosion;
using Content.Shared.Interaction.Events;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Injuries.Shrapnel;

public sealed partial class SharedCMUShrapnelSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedFractureSystem _fracture = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    private const float PainTargetCap = 70f;
    private const float MovementDistanceThreshold = 0.75f;
    private const float MovementPulseCooldownSeconds = 1.25f;
    private const float MinimumMoveDistance = 0.05f;
    private const float HighForceShrapnelExposure = 0.62f;
    private const float ExplosionShrapnelChanceStart = 0.35f;
    private const float ExplosionShrapnelGuaranteed = 0.85f;
    private const float FragmentingExplosionGuaranteedExposure = 0.62f;
    private const float MovementDamagePerFragment = 0.5f;
    private const int MaxExplosionFragments = 8;
    private static readonly DamageImpact ShrapnelMovementImpact =
        new(DamageImpactDelivery.Contact, DamageImpactContact.Fragment, DamageImpactPenetration.Low, DamageImpactEnergy.Low);

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

        SubscribeLocalEvent<CMUHumanMedicalComponent, GetVerbsEvent<InteractionVerb>>(OnGetShrapnelVerbs);
        SubscribeLocalEvent<DamageableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetShrapnelAltVerbs);
        SubscribeLocalEvent<CMUShrapnelExtractorComponent, UseInHandEvent>(OnExtractorUseInHand);
        SubscribeLocalEvent<CMUShrapnelExtractorComponent, CMUShrapnelExtractDoAfterEvent>(OnExtractorDoAfter);
        SubscribeLocalEvent<CMUHumanMedicalComponent, MoveEvent>(OnHumanMove);
        SubscribeLocalEvent<CMUHumanMedicalComponent, ComponentRemove>(OnHumanRemove);
        SubscribeLocalEvent<BodyPartComponent, BodyPartWoundAppliedEvent>(OnBodyPartWoundApplied);

        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.PainEnabled, v => _painEnabled = v, true);
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled;
    }

    public static float GetPainTarget(CMUShrapnelComponent shrapnel)
    {
        if (shrapnel.Fragments <= 0 || shrapnel.Severity <= 0f)
            return 0f;

        var fragmentPressure = 4f + shrapnel.Fragments * 3.5f;
        return MathF.Min(PainTargetCap, MathF.Max(shrapnel.Severity, fragmentPressure));
    }

    public static FixedPoint2 GetMovementDamage(CMUShrapnelComponent shrapnel)
    {
        if (shrapnel.Fragments <= 0)
            return FixedPoint2.Zero;

        return FixedPoint2.New(MovementDamagePerFragment * shrapnel.Fragments);
    }

    public bool AddShrapnel(EntityUid part, int fragments, float severity)
    {
        if (fragments <= 0 || severity <= 0f)
            return false;

        var shrapnel = EnsureComp<CMUShrapnelComponent>(part);
        var capacity = Math.Max(0, shrapnel.MaxFragments - shrapnel.Fragments);
        if (capacity <= 0)
            return false;

        var added = Math.Min(capacity, fragments);
        shrapnel.Fragments += added;
        shrapnel.Severity = Math.Clamp(MathF.Max(shrapnel.Severity, severity), 0f, PainTargetCap);
        Dirty(part, shrapnel);
        _wounds.MarkRetainedFragmentCleanup(part, shrapnel.Fragments, shrapnel.Severity);
        RaiseShrapnelChanged(part, removed: false);
        return true;
    }

    private void OnBodyPartWoundApplied(Entity<BodyPartComponent> ent, ref BodyPartWoundAppliedEvent args)
    {
        if (!IsLayerEnabled())
            return;
        if (args.Tool is not { } tool ||
            !TryComp<CMUProjectileShrapnelComponent>(tool, out var projectileShrapnel))
        {
            return;
        }

        AddShrapnel(ent.Owner, projectileShrapnel.Fragments, projectileShrapnel.Severity);
    }

    public int TryApplyExplosionShrapnel(
        EntityUid body,
        ProtoId<ExplosionPrototype> explosion,
        float exposure,
        IReadOnlyList<WeightedBodyPart> weightedParts)
    {
        if (!IsLayerEnabled())
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

        var desiredFragments = Math.Clamp((int)MathF.Ceiling(exposure * MaxExplosionFragments), 1, MaxExplosionFragments);
        var applied = 0;
        for (var i = 0; i < weightedParts.Count && applied < desiredFragments; i++)
        {
            var weighted = weightedParts[i];
            if (weighted.Weight <= 0f)
                continue;

            var partFragments = Math.Clamp((int)MathF.Ceiling(desiredFragments * weighted.Weight), 1, desiredFragments - applied);
            var severity = 8f + exposure * 34f * MathF.Max(0.35f, weighted.Weight);
            if (AddShrapnel(weighted.Part, partFragments, severity))
                applied += partFragments;
        }

        return applied;
    }

    public bool TryExtractShrapnel(
        EntityUid body,
        Entity<CMUShrapnelExtractorComponent> tool,
        out int removed,
        EntityUid? user = null,
        EntityUid? preferredPart = null)
    {
        removed = 0;
        if (!TryFindExtractionPart(body, out var part, user, preferredPart))
            return false;
        if (!TryComp<CMUShrapnelComponent>(part, out var shrapnel) || shrapnel.Fragments <= 0)
            return false;

        removed = Math.Min(Math.Max(1, tool.Comp.RemoveCount), shrapnel.Fragments);
        var oldFragments = shrapnel.Fragments;
        shrapnel.Fragments -= removed;

        if (shrapnel.Fragments <= 0)
        {
            RemComp<CMUShrapnelComponent>(part);
            _wounds.ClearRetainedFragmentCleanup(part);
        }
        else
        {
            shrapnel.Severity *= (float)shrapnel.Fragments / oldFragments;
            Dirty(part, shrapnel);
            _wounds.MarkRetainedFragmentCleanup(part, shrapnel.Fragments, shrapnel.Severity);
        }

        if (tool.Comp.DamageOnExtract > FixedPoint2.Zero)
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[tool.Comp.DamageType] = tool.Comp.DamageOnExtract;
            _damageable.TryChangeDamage(
                body,
                damage,
                interruptsDoAfters: false,
                origin: tool.Owner,
                tool: tool.Owner,
                impact: DamageImpact.SnaggingContact);
        }

        if (tool.Comp.PainPenalty > 0f)
            _pain.AddPainPulse(body, (FixedPoint2)tool.Comp.PainPenalty);

        RaiseShrapnelChanged(part, removed: true);
        return true;
    }

    public bool TryClearShrapnel(EntityUid part)
    {
        if (!HasComp<CMUShrapnelComponent>(part))
            return false;

        RemComp<CMUShrapnelComponent>(part);
        _wounds.ClearRetainedFragmentCleanup(part);
        RaiseShrapnelChanged(part, removed: true);
        return true;
    }

    public float ComputeMovementPainPulse(EntityUid body)
    {
        if (!_painEnabled)
            return 0f;
        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return 0f;

        var pulse = 0f;
        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(body))
        {
            if (TryComp<CMUShrapnelComponent>(partUid, out var shrapnel))
                pulse += GetMovementDamage(shrapnel).Float();

            if (part.PartType is not (BodyPartType.Leg or BodyPartType.Foot))
                continue;
            if (!TryComp<FractureComponent>(partUid, out var fracture))
                continue;

            pulse += MovementFracturePulse(_fracture.GetEffectiveSeverity((partUid, fracture)));
        }

        return MathF.Min(35f, pulse);
    }

    public (int Fragments, float Severity) GetTotalShrapnel(EntityUid body)
    {
        var fragments = 0;
        var severity = 0f;
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp<CMUShrapnelComponent>(partUid, out var shrapnel))
                continue;

            fragments += Math.Max(0, shrapnel.Fragments);
            severity += Math.Max(0f, shrapnel.Severity);
        }

        return (fragments, severity);
    }

    private void OnGetShrapnelVerbs(Entity<CMUHumanMedicalComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!IsLayerEnabled() || !args.CanAccess || !args.CanInteract)
            return;
        if (args.Using is not { } tool || !TryComp<CMUShrapnelExtractorComponent>(tool, out var extractor))
            return;

        if (!TryFindExtractionPart(ent.Owner, out var part, args.User))
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => StartExtraction(user, target, tool, part),
            Text = Loc.GetString("cmu-medical-shrapnel-extract-verb"),
            Icon = extractor.VerbIcon,
        });
    }

    private void OnGetShrapnelAltVerbs(Entity<DamageableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsLayerEnabled() || !args.CanAccess || !args.CanInteract)
            return;
        if (!HasComp<CMUHumanMedicalComponent>(ent.Owner))
            return;
        if (args.Using is not { } tool || !TryComp<CMUShrapnelExtractorComponent>(tool, out var extractor))
            return;

        if (!TryFindExtractionPart(ent.Owner, out var part, args.User))
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartExtraction(user, target, tool, part),
            Text = Loc.GetString("cmu-medical-shrapnel-extract-verb"),
            Icon = extractor.VerbIcon,
            Priority = 10,
        });
    }

    private void OnExtractorUseInHand(Entity<CMUShrapnelExtractorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !IsLayerEnabled())
            return;
        if (!HasComp<CMUHumanMedicalComponent>(args.User))
            return;
        if (!TryFindExtractionPart(args.User, out var part, args.User))
            return;

        args.Handled = true;
        StartExtraction(args.User, args.User, ent.Owner, part);
    }

    private void StartExtraction(EntityUid user, EntityUid target, EntityUid tool, EntityUid selectedPart)
    {
        if (!IsLayerEnabled() || !HasComp<CMUHumanMedicalComponent>(target))
            return;
        if (!TryComp<CMUShrapnelExtractorComponent>(tool, out var extractor))
            return;
        if (!TryFindExtractionPart(target, out var part, user, selectedPart))
        {
            _popup.PopupPredicted(Loc.GetString("cmu-medical-shrapnel-none"), target, user);
            return;
        }

        var ev = new CMUShrapnelExtractDoAfterEvent { PreSelectedPart = GetNetEntity(part) };
        var doAfter = new DoAfterArgs(EntityManager, user, extractor.Delay, ev, tool, target: target, used: tool)
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
        var preSelectedPart = args.PreSelectedPart;
        args.Repeat = false;
        args.PreSelectedPart = null;

        if (args.Cancelled || args.Target is not { } target)
            return;
        if (!HasComp<CMUHumanMedicalComponent>(target))
            return;

        EntityUid? preferred = null;
        if (preSelectedPart is { } netPart && TryGetEntity(netPart, out var part))
            preferred = part.Value;

        if (TryExtractShrapnel(target, ent, out var removed, args.User, preferred))
        {
            _popup.PopupPredicted(
                Loc.GetString("cmu-medical-shrapnel-extract-finish", ("count", removed)),
                target,
                args.User);
            _audio.PlayPredicted(ent.Comp.ExtractSound, target, args.User);

            if (TryFindExtractionPart(target, out var nextPart, args.User))
            {
                args.PreSelectedPart = GetNetEntity(nextPart);
                args.Repeat = true;
            }
        }
    }

    private void OnHumanMove(Entity<CMUHumanMedicalComponent> ent, ref MoveEvent args)
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
        accumulated += (float)distance;
        if (accumulated < MovementDistanceThreshold)
        {
            _movementAccumulators[ent.Owner] = accumulated;
            return;
        }

        _movementAccumulators[ent.Owner] = accumulated % MovementDistanceThreshold;
        if (!IsEligibleForEmbeddedMovement(ent.Owner))
            return;

        if (!CanPulseMovementPain(ent.Owner))
            return;

        var pulse = ApplyShrapnelMovementDamage(ent.Owner);
        pulse += (FixedPoint2)GetMovementFracturePainPulse(ent.Owner);
        if (pulse <= FixedPoint2.Zero)
            return;

        _pain.AddPainPulse(ent.Owner, (FixedPoint2)pulse);
        _pain.OnRecomputeTrigger(ent.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-medical-shrapnel-movement-pain"), ent.Owner, ent.Owner, PopupType.SmallCaution);
    }

    private void OnHumanRemove(Entity<CMUHumanMedicalComponent> ent, ref ComponentRemove args)
    {
        _movementAccumulators.Remove(ent.Owner);
        _movementPainCooldowns.Remove(ent.Owner);
    }

    private bool CanPulseMovementPain(EntityUid body)
    {
        var now = _timing.CurTime;
        if (_movementPainCooldowns.TryGetValue(body, out var next) && next > now)
            return false;

        _movementPainCooldowns[body] = now + TimeSpan.FromSeconds(MovementPulseCooldownSeconds);
        return true;
    }

    private FixedPoint2 ApplyShrapnelMovementDamage(EntityUid body)
    {
        var total = FixedPoint2.Zero;
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp<CMUShrapnelComponent>(partUid, out var shrapnel))
                continue;

            var amount = GetMovementDamage(shrapnel);
            if (amount <= FixedPoint2.Zero)
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict["Piercing"] = amount;
            if (!_partHealth.TryApplyPartDamage(
                    body,
                    partUid,
                    damage,
                    tool: partUid,
                    mechanism: CMUTraumaMechanism.Pierce,
                    origin: partUid,
                    impact: ShrapnelMovementImpact))
            {
                continue;
            }

            total += amount;
        }

        return total;
    }

    private float GetMovementFracturePainPulse(EntityUid body)
    {
        if (!_painEnabled)
            return 0f;

        var pulse = 0f;
        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(body))
        {
            if (part.PartType is not (BodyPartType.Leg or BodyPartType.Foot))
                continue;
            if (!TryComp<FractureComponent>(partUid, out var fracture))
                continue;

            pulse += MovementFracturePulse(_fracture.GetEffectiveSeverity((partUid, fracture)));
        }

        return pulse;
    }

    private bool IsEligibleForEmbeddedMovement(EntityUid body)
    {
        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return false;

        if (_standing.IsDown(body))
            return false;

        return !TryComp<BuckleComponent>(body, out var buckle) || !buckle.Buckled;
    }

    private bool TryFindExtractionPart(
        EntityUid body,
        out EntityUid part,
        EntityUid? user = null,
        EntityUid? preferredPart = null)
    {
        part = default;

        if (preferredPart is { } preferred
            && TryComp<BodyPartComponent>(preferred, out var preferredBody)
            && preferredBody.Body == body
            && HasComp<CMUShrapnelComponent>(preferred))
        {
            part = preferred;
            return true;
        }

        if (user is { } u
            && TryComp<BodyZoneTargetingComponent>(u, out var aim)
            && aim.LastSelectedAt > TimeSpan.Zero)
        {
            var (type, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
            foreach (var (partUid, bodyPart) in _medicalIndex.GetBodyParts(body))
            {
                if (bodyPart.PartType != type)
                    continue;
                if (symmetry is not BodyPartSymmetry.None && bodyPart.Symmetry != symmetry)
                    continue;
                if (!HasComp<CMUShrapnelComponent>(partUid))
                    continue;

                part = partUid;
                return true;
            }
        }

        var bestScore = 0f;
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp<CMUShrapnelComponent>(partUid, out var shrapnel))
                continue;

            var score = GetPainTarget(shrapnel);
            if (score <= bestScore)
                continue;

            bestScore = score;
            part = partUid;
        }

        return bestScore > 0f;
    }

    private void RaiseShrapnelChanged(EntityUid part, bool removed)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp) || partComp.Body is not { } body)
            return;

        var ev = new CMUShrapnelChangedEvent(body, part, removed);
        RaiseLocalEvent(part, ref ev, broadcast: true);
        _pain.OnRecomputeTrigger(body);
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

    private static bool IsGuaranteedExplosionShrapnel(
        ProtoId<ExplosionPrototype> explosion,
        float exposure)
    {
        return exposure >= ExplosionShrapnelGuaranteed ||
            IsFragmentingExplosion(explosion) && exposure >= FragmentingExplosionGuaranteedExposure;
    }

    private static bool IsFragmentingExplosion(ProtoId<ExplosionPrototype> explosion)
    {
        return explosion == "Default"
            || explosion == "RMC"
            || explosion == "RMCMortar"
            || explosion == "RMCOB"
            || explosion == "RMCOBXenoTunnel";
    }

    private static float MovementFracturePulse(FractureSeverity severity) => severity switch
    {
        FractureSeverity.Hairline => 3f,
        FractureSeverity.Simple => 8f,
        FractureSeverity.Compound => 14f,
        FractureSeverity.Shattered => 22f,
        _ => 0f,
    };
}
