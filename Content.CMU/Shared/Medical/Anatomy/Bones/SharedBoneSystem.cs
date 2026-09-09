using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Trauma;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Synth;
using Content.Shared.StatusEffectNew;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Events;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Medical.Anatomy.Bones;

public abstract partial class SharedBoneSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedFractureSystem Fracture = default!;
    [Dependency] protected StatusEffectsSystem Status = default!;
    [Dependency] protected RMCUnrevivableSystem Unrevivable = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;
    [Dependency] private CMStasisBagSystem _stasis = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MetaDataSystem _metadata = default!;

    private const string BoneRegenBoostStatus = "StatusEffectCMUBoneRegenBoost";
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";

    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(10);
    private static readonly CMUMedicalWorkKey RecoveryWork = new("bone-integrity-recovery");

    private bool _medicalEnabled;
    private bool _boneEnabled;
    private FixedPoint2 _boneHealRate;
    private FixedPoint2 _projectileBruteMultiplier = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BoneComponent, BodyPartDamagedEvent>(OnBodyPartDamaged);
        SubscribeLocalEvent<BoneComponent, ComponentStartup>(OnBoneStartup);
        SubscribeLocalEvent<BoneComponent, BoneFractureAttemptEvent>(OnBoneFractureAttempt);
        SubscribeLocalEvent<BoneComponent, CMUMedicalWorkDueEvent>(OnRecoveryDue);
        SubscribeLocalEvent<BoneComponent, ComponentShutdown>(OnBoneShutdown);
        SubscribeLocalEvent<BoneComponent, OrganGotInsertedEvent>(OnPartInserted,
            after: new[] { typeof(SharedBodySystem) });
        SubscribeLocalEvent<BoneComponent, OrganGotRemovedEvent>(OnPartRemoved,
            after: new[] { typeof(SharedBodySystem) });
        SubscribeLocalEvent<CMUHumanMedicalComponent, CMUMedicalStasisChangedEvent>(OnStasisChanged);
        SubscribeLocalEvent<CMUHumanMedicalComponent, EntityPausedEvent>(OnPatientPaused);
        SubscribeLocalEvent<CMUHumanMedicalComponent, EntityUnpausedEvent>(OnPatientUnpaused);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneEnabled, v => _boneEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneHealRate, v => _boneHealRate = (FixedPoint2)v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneProjectileBruteMultiplier, v => _projectileBruteMultiplier = (FixedPoint2)MathF.Max(0f, v), true);
    }

    private void OnBoneStartup(Entity<BoneComponent> ent, ref ComponentStartup args)
    {
        if (PartBelongsToSynth(ent.Owner))
            ClearSynthFracture(ent.Owner);
        RefreshRecovery(ent);
    }

    private void OnBoneFractureAttempt(Entity<BoneComponent> ent, ref BoneFractureAttemptEvent args)
    {
        if (!PartBelongsToSynth(ent.Owner))
            return;

        args.Cancelled = true;
        ClearSynthFracture(ent.Owner);
    }

    private bool PartBelongsToSynth(EntityUid part)
    {
        if (HasComp<CMURoboticLimbComponent>(part))
            return true;

        return TryComp<BodyPartComponent>(part, out var bodyPart) &&
               bodyPart.Body is { } body &&
               HasComp<SynthComponent>(body);
    }

    private void ClearSynthFracture(EntityUid part)
    {
        if (TryComp<FractureComponent>(part, out var fracture))
            Fracture.SetSeverity((part, fracture), FractureSeverity.None, forceUpgrade: false);

        RemComp<CMUPostOpBoneSetComponent>(part);
        RemComp<CMUMalunionComponent>(part);
        RemComp<CMUSplintedComponent>(part);
        RemComp<CMUCastComponent>(part);
    }

    private void OnBodyPartDamaged(Entity<BoneComponent> ent, ref BodyPartDamagedEvent args)
    {
        if (!_medicalEnabled || !_boneEnabled)
            return;

        var brute = GetGroupTotal(args.Delta, BruteGroup);
        if (brute <= FixedPoint2.Zero)
            return;

        var shatterExposedRibs = IsShallowChestMeleeHit(ent, args);
        if (!args.Trauma.BoneContact && !shatterExposedRibs)
            return;

        if (shatterExposedRibs)
        {
            // A direct strike against exposed ribs deliberately substitutes for sawing the cavity open.
            ent.Comp.Integrity = FixedPoint2.Zero;
        }
        else
        {
            var effectiveBrute = args.Trauma.Mechanism == CMUTraumaMechanism.Ballistic
                ? brute * _projectileBruteMultiplier
                : brute;
            var absorbed = effectiveBrute * (FixedPoint2)ent.Comp.BruteAbsorbFraction;
            if (HasComp<ChemicalHyperdensityComponent>(args.Body) &&
                TryComp<ChemicalHyperdensityComponent>(args.Body, out var density))
            {
                absorbed *= (FixedPoint2)Math.Clamp(1f - density.Protection, 0f, 1f);
            }
            ent.Comp.Integrity = FixedPoint2.Max(FixedPoint2.Zero, ent.Comp.Integrity - absorbed);
        }

        Dirty(ent);
        RefreshRecovery(ent);

        var newSeverity = SeverityFromIntegrity(ent.Comp);
        if (newSeverity == FractureSeverity.None)
            return;

        var current = TryComp<FractureComponent>(ent, out var existing) ? existing.Severity : FractureSeverity.None;
        if (newSeverity <= current)
            return;

        var attempt = new BoneFractureAttemptEvent(ent.Owner, newSeverity);
        RaiseLocalEvent(ent, ref attempt);
        if (attempt.Cancelled)
            return;

        var fracture = EnsureComp<FractureComponent>(ent);
        fracture.SourceZone = args.TargetZone ?? fracture.SourceZone ?? args.Type switch
        {
            BodyPartType.Head => TargetBodyZone.Head,
            BodyPartType.Torso => TargetBodyZone.Chest,
            _ => null,
        };
        Fracture.SetSeverity((ent.Owner, fracture), newSeverity);

        var fracEv = new BoneFracturedEvent(args.Body, ent.Owner, current, newSeverity);
        RaiseLocalEvent(ent, ref fracEv, broadcast: true);
        // Audio for Compound+ spawns is played server-side by Content.Server's
        // sealed BoneSystem to avoid a double-play on prediction rollback.

        if (args.Type == BodyPartType.Torso && newSeverity == FractureSeverity.Shattered)
            RaiseRibBurst(args.Body, args.ContainedOrgans, args.Delta);
    }

    private bool IsShallowChestMeleeHit(Entity<BoneComponent> ent, BodyPartDamagedEvent args)
    {
        if (args.Type != BodyPartType.Torso ||
            args.TargetZone != TargetBodyZone.Chest ||
            args.Impact.Delivery != DamageImpactDelivery.Melee ||
            PartBelongsToSynth(ent.Owner) ||
            !HasComp<CMIncisionOpenComponent>(ent) ||
            !HasComp<CMSkinRetractedComponent>(ent) ||
            HasComp<CMRibcageSawedComponent>(ent) ||
            HasComp<CMRibcageOpenComponent>(ent))
        {
            return false;
        }

        return !TryComp<FractureComponent>(ent, out var fracture) ||
               fracture.Severity is not (FractureSeverity.Compound or FractureSeverity.Shattered);
    }

    /// <summary>
    ///     Routes a fraction of the post-bone damage as a direct
    ///     <see cref="OrganDamagedEvent"/> with
    ///     <see cref="OrganDamageSource.RibFracture"/> source against every
    ///     heart and lung organ in the body. Includes lungs in the head/torso
    ///     mapping because vanilla SS14 places lungs in the torso slot.
    /// </summary>
    private void RaiseRibBurst(EntityUid body, IReadOnlyList<EntityUid> partOrgans, DamageSpecifier delta)
    {
        // Use a small, fixed slice of the damage so a single Shattered hit
        // doesn't multi-apply the full Brute load to organs already taking the
        // distributed share.
        var burst = new DamageSpecifier();
        foreach (var (type, amount) in delta.DamageDict)
            burst.DamageDict[type] = amount / 4;

        if (burst.GetTotal() <= FixedPoint2.Zero)
            return;

        foreach (var organ in partOrgans)
        {
            if (!HasComp<HeartComponent>(organ) && !HasComp<LungsComponent>(organ))
                continue;
            if (!HasComp<OrganHealthComponent>(organ))
                continue;

            var ev = new OrganDamagedEvent(body, organ, burst, OrganDamageSource.RibFracture);
            RaiseLocalEvent(organ, ref ev, broadcast: true);
        }
    }

    /// <summary>
    ///     Walk descending: lowest threshold first wins so a hit that crashes
    ///     integrity from 80 down to 3 lands as Shattered, not Hairline.
    /// </summary>
    private static FractureSeverity SeverityFromIntegrity(BoneComponent bone)
    {
        var i = bone.Integrity;
        if (bone.FractureThresholds.TryGetValue(FractureSeverity.Shattered, out var c) && i <= c)
            return FractureSeverity.Shattered;
        if (bone.FractureThresholds.TryGetValue(FractureSeverity.Compound, out var co) && i <= co)
            return FractureSeverity.Compound;
        if (bone.FractureThresholds.TryGetValue(FractureSeverity.Simple, out var s) && i <= s)
            return FractureSeverity.Simple;
        if (bone.FractureThresholds.TryGetValue(FractureSeverity.Hairline, out var h) && i <= h)
            return FractureSeverity.Hairline;
        return FractureSeverity.None;
    }

    /// <summary>
    ///     Resolves the prototype once per call rather than caching so prototype
    ///     reload during dev keeps working.
    /// </summary>
    private FixedPoint2 GetGroupTotal(DamageSpecifier delta, ProtoId<DamageGroupPrototype> group)
    {
        if (!Proto.TryIndex(group, out var groupProto))
            return FixedPoint2.Zero;
        return delta.TryGetDamageInGroup(groupProto, out var total) ? total : FixedPoint2.Zero;
    }

    private void RefreshRecovery(Entity<BoneComponent> bone)
    {
        if (_net.IsClient || TerminatingOrDeleted(bone.Owner))
            return;

        if (bone.Comp.Integrity >= bone.Comp.IntegrityMax || PartBelongsToSynth(bone.Owner))
        {
            _scheduler.Cancel(bone.Owner, RecoveryWork);
            RemComp<BoneRecoveryComponent>(bone.Owner);
            return;
        }

        if (!TryComp<BoneRecoveryComponent>(bone.Owner, out var recovery))
        {
            recovery = AddComp<BoneRecoveryComponent>(bone.Owner);
            recovery.DueAt = RecoveryTime(bone.Owner) + RecoveryInterval;
            ScheduleRecovery((bone.Owner, recovery));
        }
        RefreshSuspension((bone.Owner, recovery));
    }

    private void RefreshSuspension(Entity<BoneRecoveryComponent> ent,
        bool? stasis = null, bool? patientPaused = null)
    {
        var suspended = !TryComp<BodyPartComponent>(ent, out var part) ||
                        part.Body is not { } body ||
                        (patientPaused ?? Paused(body)) ||
                        (stasis ?? HasComp<CMInStasisComponent>(body));
        if (suspended == ent.Comp.Suspended)
            return;

        ent.Comp.Suspended = suspended;
        if (suspended)
        {
            var now = RecoveryTime(ent.Owner);
            ent.Comp.Remaining = ent.Comp.DueAt > now
                ? ent.Comp.DueAt - now : TimeSpan.Zero;
            _scheduler.Cancel(ent.Owner, RecoveryWork);
        }
        else
        {
            ent.Comp.DueAt = RecoveryTime(ent.Owner) + ent.Comp.Remaining;
            ScheduleRecovery(ent);
        }
    }

    private TimeSpan RecoveryTime(EntityUid part) => Timing.CurTime - _metadata.GetPauseTime(part);

    private void ScheduleRecovery(Entity<BoneRecoveryComponent> ent)
        => _scheduler.Schedule(ent.Owner, RecoveryWork, ent.Comp.DueAt + _metadata.GetPauseTime(ent.Owner));

    private void OnRecoveryDue(Entity<BoneComponent> ent, ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != RecoveryWork || !TryComp<BoneRecoveryComponent>(ent, out var recovery))
            return;
        RefreshSuspension((ent.Owner, recovery));
        if (recovery.Suspended)
            return;

        // Preserve the existing ten-second treatment quantum. A late dispatch
        // does not invent historical medication exposure or apply catch-up doses.
        recovery.DueAt = Timing.CurTime + RecoveryInterval;
        ScheduleRecovery((ent.Owner, recovery));
        if (!_medicalEnabled || !_boneEnabled ||
            !TryComp<BodyPartComponent>(ent, out var part) || part.Body is not { } body ||
            Unrevivable.IsUnrevivable(body) || !_stasis.CanBodyMetabolize(body) ||
            HasComp<CMUMalunionComponent>(ent) || PartBelongsToSynth(ent.Owner))
            return;

        // Metabolism eligibility is an event boundary. A listener can remove the
        // part or complete its treatment before returning permission to metabolize.
        if (TerminatingOrDeleted(ent.Owner) || part.Body != body ||
            !TryComp<BoneComponent>(ent.Owner, out var currentBone) || currentBone != ent.Comp ||
            !TryComp<BoneRecoveryComponent>(ent.Owner, out var currentRecovery) ||
            currentRecovery != recovery || recovery.Suspended)
            return;

        var severity = TryComp<FractureComponent>(ent, out var fracture)
            ? fracture.Severity : FractureSeverity.None;
        var (boosted, multiplier) = GetBoneRegenBoost(body);
        if (!CanHeal(severity, boosted))
            return;

        var rate = FixedPoint2.Max(FixedPoint2.Zero, _boneHealRate * (FixedPoint2) multiplier);
        var next = FixedPoint2.Min(ent.Comp.IntegrityMax, ent.Comp.Integrity + rate);
        if (next != ent.Comp.Integrity)
        {
            ent.Comp.Integrity = next;
            Dirty(ent);
            var healedSeverity = SeverityFromIntegrity(ent.Comp);
            if (fracture != null && healedSeverity < severity)
                Fracture.SetSeverity((ent.Owner, fracture), healedSeverity, forceUpgrade: false);
        }
        // Severity callbacks may heal, damage or detach this part synchronously.
        if (!TerminatingOrDeleted(ent.Owner) &&
            TryComp<BoneComponent>(ent.Owner, out currentBone) && currentBone == ent.Comp)
            RefreshRecovery(ent);
    }

    private void OnBoneShutdown(Entity<BoneComponent> ent, ref ComponentShutdown args)
    {
        _scheduler.Cancel(ent.Owner, RecoveryWork);
        if (_net.IsServer && !TerminatingOrDeleted(ent.Owner))
            RemComp<BoneRecoveryComponent>(ent.Owner);
    }

    private void OnPartInserted(Entity<BoneComponent> ent, ref OrganGotInsertedEvent args)
        => RefreshRecovery(ent);

    private void OnPartRemoved(Entity<BoneComponent> ent, ref OrganGotRemovedEvent args)
        => RefreshRecovery(ent);

    private void OnStasisChanged(Entity<CMUHumanMedicalComponent> ent, ref CMUMedicalStasisChangedEvent args)
        => RefreshPatientSuspension(ent.Owner, stasis: args.Active);

    private void OnPatientPaused(Entity<CMUHumanMedicalComponent> ent, ref EntityPausedEvent args)
        => RefreshPatientSuspension(ent.Owner, patientPaused: true);

    private void OnPatientUnpaused(Entity<CMUHumanMedicalComponent> ent, ref EntityUnpausedEvent args)
        => RefreshPatientSuspension(ent.Owner, patientPaused: false);

    private void RefreshPatientSuspension(EntityUid body, bool? stasis = null, bool? patientPaused = null)
    {
        if (_net.IsClient || TerminatingOrDeleted(body))
            return;
        foreach (var (part, _) in _medicalIndex.GetBodyParts(body))
        {
            if (TryComp<BoneRecoveryComponent>(part, out var recovery))
                RefreshSuspension((part, recovery), stasis, patientPaused);
        }
    }

    /// <summary>
    ///     Intact weakened bones and Hairline fractures recover naturally. Osteocalc's bone
    ///     regen boost can also stabilize Simple and Compound fractures over
    ///     time.
    /// </summary>
    protected virtual bool CanHeal(FractureSeverity severity, bool hasBoneRegenBoost)
        => severity is FractureSeverity.None or FractureSeverity.Hairline
           || hasBoneRegenBoost && severity is FractureSeverity.Simple or FractureSeverity.Compound;

    private (bool Boosted, float Multiplier) GetBoneRegenBoost(EntityUid body)
    {
        if (!Status.TryGetStatusEffect(body, BoneRegenBoostStatus, out var effectUid))
            return (false, 1f);

        if (!TryComp<BoneRegenBoostComponent>(effectUid.Value, out var boost))
            return (true, 1f);

        return (true, boost.Multiplier < 1f ? 1f : boost.Multiplier);
    }

    public void RestoreIntegrity(Entity<BoneComponent?> part, FixedPoint2 newIntegrity)
    {
        if (!Resolve(part.Owner, ref part.Comp, logMissing: false))
            return;
        part.Comp.Integrity = FixedPoint2.Clamp(newIntegrity, FixedPoint2.Zero, part.Comp.IntegrityMax);
        Dirty(part.Owner, part.Comp);
        RefreshRecovery((part.Owner, part.Comp));
    }

    /// <summary>
    ///     Seeds a fracture injury with matching structural damage. Existing worse
    ///     injuries are preserved. Scenario generators must use this instead of
    ///     adding a fracture marker to a fully intact bone.
    /// </summary>
    public bool SeedFracture(EntityUid part, FractureSeverity severity)
    {
        if (severity == FractureSeverity.None ||
            !TryComp<BoneComponent>(part, out var bone) ||
            !bone.FractureThresholds.TryGetValue(severity, out var threshold))
            return false;

        var attempt = new BoneFractureAttemptEvent(part, severity);
        RaiseLocalEvent(part, ref attempt);
        if (attempt.Cancelled)
            return false;

        bone.Integrity = FixedPoint2.Min(bone.Integrity, threshold);
        Dirty(part, bone);
        RefreshRecovery((part, bone));
        var fracture = EnsureComp<FractureComponent>(part);
        var previous = fracture.Severity;
        Fracture.SetSeverity((part, fracture), SeverityFromIntegrity(bone));
        if (fracture.Severity > previous &&
            TryComp<BodyPartComponent>(part, out var anatomy) && anatomy.Body is { } body)
        {
            var fractured = new BoneFracturedEvent(body, part, previous, fracture.Severity);
            RaiseLocalEvent(part, ref fractured, broadcast: true);
        }
        return true;
    }

    public int ChemicallyMendFractures(EntityUid body, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return 0;

        var treated = 0;
        foreach (var (part, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp<BoneComponent>(part, out var bone) ||
                !TryComp<FractureComponent>(part, out var fracture) ||
                fracture.Severity is FractureSeverity.None or FractureSeverity.Shattered ||
                !HasComp<CMUSplintedComponent>(part) && !HasComp<CMUCastComponent>(part))
            {
                continue;
            }

            RestoreIntegrity((part, bone), bone.Integrity + amount);
            treated++;
            var healedSeverity = SeverityFromIntegrity(bone);
            if (healedSeverity < fracture.Severity)
                Fracture.SetSeverity((part, fracture), healedSeverity, forceUpgrade: false);
        }

        return treated;
    }

    public bool ApplyChemicalMalunion(EntityUid body)
    {
        foreach (var (part, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp<FractureComponent>(part, out var fracture) || fracture.Severity == FractureSeverity.None)
                continue;
            EnsureComp<CMUMalunionComponent>(part);
            return true;
        }

        return false;
    }

    public bool WorsenChemicalFracture(EntityUid body)
    {
        foreach (var (part, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp<FractureComponent>(part, out var fracture) ||
                fracture.Severity is FractureSeverity.None or FractureSeverity.Shattered)
                continue;
            var next = (FractureSeverity)((byte)fracture.Severity + 1);
            return SeedFracture(part, next);
        }

        return false;
    }

    public bool DamageWeakestBone(EntityUid body, FixedPoint2 amount, bool fracture)
    {
        if (amount <= FixedPoint2.Zero)
            return false;

        EntityUid? selected = null;
        BoneComponent? selectedBone = null;
        foreach (var (part, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp<BoneComponent>(part, out var bone))
                continue;
            if (selectedBone != null && bone.Integrity >= selectedBone.Integrity)
                continue;
            selected = part;
            selectedBone = bone;
        }

        if (selected is not { } selectedPart || selectedBone == null)
            return false;

        selectedBone.Integrity = FixedPoint2.Max(FixedPoint2.Zero, selectedBone.Integrity - amount);
        Dirty(selectedPart, selectedBone);
        RefreshRecovery((selectedPart, selectedBone));
        if (fracture)
        {
            var severity = SeverityFromIntegrity(selectedBone);
            var fractureComp = EnsureComp<FractureComponent>(selectedPart);
            Fracture.SetSeverity((selectedPart, fractureComp), severity);
        }
        return true;
    }
}
