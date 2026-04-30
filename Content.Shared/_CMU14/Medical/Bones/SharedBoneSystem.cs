using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._CMU14.Medical.Organs.Lungs;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Bones;

public abstract class SharedBoneSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly SharedFractureSystem Fracture = default!;
    [Dependency] protected readonly RMCUnrevivableSystem Unrevivable = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";

    private const float IntegrityScanInterval = 1f;
    private float _integrityScanAccumulator;

    private bool _medicalEnabled;
    private bool _boneEnabled;
    private FixedPoint2 _boneHealRate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BoneComponent, BodyPartDamagedEvent>(OnBodyPartDamaged);
        SubscribeLocalEvent<BoneComponent, ComponentStartup>(OnBoneStartup);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneEnabled, v => _boneEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneHealRate, v => _boneHealRate = (FixedPoint2)v, true);
    }

    private void OnBoneStartup(Entity<BoneComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextIntegrityTick = Timing.CurTime + TimeSpan.FromSeconds(10);
    }

    private void OnBodyPartDamaged(Entity<BoneComponent> ent, ref BodyPartDamagedEvent args)
    {
        if (!_medicalEnabled || !_boneEnabled)
            return;

        var brute = GetGroupTotal(args.Delta, BruteGroup);
        if (brute <= FixedPoint2.Zero)
            return;

        var absorbed = brute * (FixedPoint2)ent.Comp.BruteAbsorbFraction;
        ent.Comp.Integrity = FixedPoint2.Max(FixedPoint2.Zero, ent.Comp.Integrity - absorbed);
        Dirty(ent);

        var newSeverity = SeverityFromIntegrity(ent.Comp);
        if (newSeverity == FractureSeverity.None)
            return;

        var current = TryComp<FractureComponent>(ent, out var existing) ? existing.Severity : FractureSeverity.None;
        if (newSeverity <= current)
            return;

        var attempt = new BoneFractureAttemptEvent(ent.Owner, newSeverity);
        RaiseLocalEvent(ent, attempt);
        if (attempt.Cancelled)
            return;

        var fracture = EnsureComp<FractureComponent>(ent);
        Fracture.SetSeverity((ent.Owner, fracture), newSeverity);

        var fracEv = new BoneFracturedEvent(args.Body, ent.Owner, current, newSeverity);
        RaiseLocalEvent(ent, ref fracEv);
        // Audio for Compound+ spawns is played server-side by Content.Server's
        // sealed BoneSystem to avoid a double-play on prediction rollback.

        if (args.Type == BodyPartType.Torso && newSeverity.IsAtLeast(FractureSeverity.Compound))
            RaiseRibBurst(args.Body, args.ContainedOrgans, args.Delta);
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
        // Use a small, fixed slice of the damage so a single Comminuted hit
        // doesn't multi-apply the full Brute load to organs already taking the
        // distributed share.
        var halved = new DamageSpecifier();
        foreach (var (type, amount) in delta.DamageDict)
            halved.DamageDict[type] = amount / 2;

        if (halved.GetTotal() <= FixedPoint2.Zero)
            return;

        foreach (var organ in partOrgans)
        {
            if (!HasComp<HeartComponent>(organ) && !HasComp<LungsComponent>(organ))
                continue;
            if (!HasComp<OrganHealthComponent>(organ))
                continue;

            var ev = new OrganDamagedEvent(body, organ, halved, OrganDamageSource.RibFracture);
            RaiseLocalEvent(organ, ref ev);
        }
    }

    /// <summary>
    ///     Walk descending: lowest threshold first wins so a hit that crashes
    ///     integrity from 80 down to 3 lands as Comminuted, not Hairline.
    /// </summary>
    private static FractureSeverity SeverityFromIntegrity(BoneComponent bone)
    {
        var i = bone.Integrity;
        if (bone.FractureThresholds.TryGetValue(FractureSeverity.Comminuted, out var c) && i <= c)
            return FractureSeverity.Comminuted;
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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!_medicalEnabled || !_boneEnabled)
            return;

        _integrityScanAccumulator += frameTime;
        if (_integrityScanAccumulator < IntegrityScanInterval)
            return;
        _integrityScanAccumulator = 0f;

        var now = Timing.CurTime;
        var rate = _boneHealRate;
        var query = EntityQueryEnumerator<FractureComponent, BoneComponent, BodyPartComponent>();
        while (query.MoveNext(out var partUid, out var fracture, out var bone, out var part))
        {
            if (bone.NextIntegrityTick > now)
                continue;
            bone.NextIntegrityTick = now + TimeSpan.FromSeconds(10);

            if (part.Body is not { } body || Unrevivable.IsUnrevivable(body))
                continue;

            if (!CanHeal(fracture.Severity))
                continue;

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
    ///     Only Hairline fractures self-heal by default.
    /// </summary>
    protected virtual bool CanHeal(FractureSeverity severity)
        => severity == FractureSeverity.Hairline;

    public void RestoreIntegrity(Entity<BoneComponent?> part, FixedPoint2 newIntegrity)
    {
        if (!Resolve(part.Owner, ref part.Comp, logMissing: false))
            return;
        part.Comp.Integrity = FixedPoint2.Min(part.Comp.IntegrityMax, newIntegrity);
        Dirty(part.Owner, part.Comp);
    }
}
