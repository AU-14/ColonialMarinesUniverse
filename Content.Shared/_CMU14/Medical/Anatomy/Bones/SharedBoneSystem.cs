using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Injuries.Trauma;
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
using Content.Shared._CMU14.Medical.Core;

namespace Content.Shared._CMU14.Medical.Anatomy.Bones;

public abstract partial class SharedBoneSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedFractureSystem Fracture = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;
    [Dependency] protected RMCUnrevivableSystem Unrevivable = default!;

    private const string BoneRegenBoostStatus = "StatusEffectCMUBoneRegenBoost";
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";

    private const float IntegrityScanInterval = 1f;
    private float _integrityScanAccumulator;

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

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneEnabled, v => _boneEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneHealRate, v => _boneHealRate = (FixedPoint2)v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneProjectileBruteMultiplier, v => _projectileBruteMultiplier = (FixedPoint2)MathF.Max(0f, v), true);
    }

    private void OnBoneStartup(Entity<BoneComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextIntegrityTick = Timing.CurTime + TimeSpan.FromSeconds(10);

        if (PartBelongsToSynth(ent.Owner))
            ClearSynthFracture(ent.Owner);
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
            ent.Comp.Integrity = FixedPoint2.Max(FixedPoint2.Zero, ent.Comp.Integrity - absorbed);
        }

        Dirty(ent);

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

    protected void UpdateServer(float frameTime)
    {
        if (!_medicalEnabled || !_boneEnabled)
            return;

        _integrityScanAccumulator += frameTime;
        if (_integrityScanAccumulator < IntegrityScanInterval)
            return;
        _integrityScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<FractureComponent, BoneComponent, BodyPartComponent>();
        while (query.MoveNext(out var partUid, out var fracture, out var bone, out var part))
        {
            if (HasComp<CMUMalunionComponent>(partUid))
                continue;

            if (bone.NextIntegrityTick > now)
                continue;
            bone.NextIntegrityTick = now + TimeSpan.FromSeconds(10);

            if (part.Body is not { } body || Unrevivable.IsUnrevivable(body))
                continue;

            var (boosted, multiplier) = GetBoneRegenBoost(body);
            if (!CanHeal(fracture.Severity, boosted))
                continue;

            var rate = _boneHealRate * (FixedPoint2) multiplier;
            bone.Integrity = FixedPoint2.Min(bone.IntegrityMax, bone.Integrity + rate);
            Dirty(partUid, bone);

            if (bone.FractureThresholds.TryGetValue(fracture.Severity, out var spawnFloor)
                && bone.Integrity > spawnFloor)
            {
                Fracture.SetSeverity((partUid, fracture), FractureSeverity.None);
            }
        }
    }

    /// <summary>
    ///     Only Hairline fractures self-heal by default. Osteocalc's bone
    ///     regen boost can also stabilize Simple and Compound fractures over
    ///     time.
    /// </summary>
    protected virtual bool CanHeal(FractureSeverity severity, bool hasBoneRegenBoost)
        => severity == FractureSeverity.Hairline
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
        part.Comp.Integrity = FixedPoint2.Min(part.Comp.IntegrityMax, newIntegrity);
        Dirty(part.Owner, part.Comp);
    }
}
