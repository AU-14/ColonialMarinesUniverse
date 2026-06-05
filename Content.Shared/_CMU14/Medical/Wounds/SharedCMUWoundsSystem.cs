using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Trauma;
using Content.Shared._CMU14.Medical.Wounds.Events;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Wounds;

/// <summary>
///     Subscribes after <see cref="SharedBoneSystem"/> and
///     <see cref="SharedOrganHealthSystem"/> so integrity / fracture-severity
///     / organ-stage are already updated when the wound layer reads them.
/// </summary>
public abstract partial class SharedCMUWoundsSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected SharedBodyPartHealthSystem PartHealth = default!;
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected SharedContainerSystem Containers = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected RMCUnrevivableSystem Unrevivable = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    /// <summary>
    ///     Minimum total Brute+Burn for a single
    ///     <see cref="BodyPartDamagedEvent"/> to spawn a wound entry. Below
    ///     this threshold tiny chips of damage don't accumulate into the
    ///     per-part list.
    /// </summary>
    public const float WoundThreshold = 5f;

    public const int MaxWoundsPerPart = 6;

    /// <summary>
    ///     Single-hit Blunt threshold above which crushing trauma also spawns
    ///     an internal bleed in the part.
    /// </summary>
    public const float SevereBluntInternalBleed = 60f;

    /// <summary>
    ///     Splints stabilize catastrophic fractures but cannot fully control
    ///     the internal bleeding from a shattered bone.
    /// </summary>
    public const float SplintedComminutedInternalBleedMultiplier = 0.5f;

    /// <summary>
    ///     Untreated wounds do not progress; only <c>Treated = true</c>
    ///     unlocks the heal accumulator.
    /// </summary>
    public const float HealPerSecond = 0.6f;

    private const float AdequateCleanupBurdenMultiplier = 0.5f;
    private const float WoundScanInterval = 0.5f;

    private float _woundScanAccumulator;

    private bool _medicalEnabled;
    private bool _woundsEnabled;
    private float _internalBleedTickSeconds;
    private FixedPoint2 _escharBurnThreshold;

    public override void Initialize()
    {
        base.Initialize();
        // after: ordering so we read updated bone integrity / fracture
        // severity / organ stage from the same hit.
        SubscribeLocalEvent<BodyPartComponent, BodyPartDamagedEvent>(
            OnBodyPartDamaged,
            after: new[] { typeof(SharedBoneSystem), typeof(SharedOrganHealthSystem) });

        SubscribeLocalEvent<FractureComponent, BoneFracturedEvent>(OnBoneFractured);
        SubscribeLocalEvent<CMUSplintedComponent, ComponentStartup>(OnSplintStartup);
        SubscribeLocalEvent<CMUSplintedComponent, ComponentRemove>(OnSplintRemove);
        SubscribeLocalEvent<CMUSplintChangedEvent>(OnSplintChanged);
        SubscribeLocalEvent<OrganHealthComponent, OrganStageChangedEvent>(OnOrganStageChanged);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.WoundsEnabled, v => _woundsEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.WoundsInternalBleedTickSeconds, v => _internalBleedTickSeconds = MathF.Max(0.5f, v), true);
        Cfg.OnValueChanged(CMUMedicalCCVars.EscharBurnThreshold, v => _escharBurnThreshold = (FixedPoint2)v, true);
    }

    public bool IsEnabled()
    {
        return _medicalEnabled && _woundsEnabled;
    }

    private void OnBodyPartDamaged(Entity<BodyPartComponent> ent, ref BodyPartDamagedEvent args)
    {
        if (!IsEnabled())
            return;

        if (!HasComp<CMUHumanMedicalComponent>(args.Body))
            return;

        // Synths repair via welder/cable, not bandages or surgical line.
        if (HasComp<SynthComponent>(args.Body))
            return;

        var brute = GroupSum(args.Delta, BruteGroup);
        var burn = GroupSum(args.Delta, BurnGroup);
        var bruteOrBurn = brute + burn;
        if (bruteOrBurn < (FixedPoint2)WoundThreshold)
            return;

        var partWound = EnsureComp<BodyPartWoundComponent>(ent);

        var type = brute >= burn ? WoundType.Brute : WoundType.Burn;
        var bleedDuration = ComputeBleedDuration(args.Delta);
        var stopBleedAt = Timing.CurTime + bleedDuration;

        var size = WoundSizeProfile.FromDamage(bruteOrBurn.Float());
        var bleedScale = WoundSizeProfile.BleedMultiplier(size);
        var bloodloss = type == WoundType.Brute ? ComputeBleedAmount(brute) * bleedScale : 0f;
        var mechanism = ClassifyMechanism(args, brute, burn);
        var secondary = ClassifySecondaryMechanisms(args, mechanism, brute, burn);
        var cleanup = DefaultCleanupFor(mechanism, secondary, size);

        AddOrMergeWound(partWound, new Wound(
            bruteOrBurn,
            FixedPoint2.Zero,
            bloodloss,
            stopBleedAt,
            type,
            false), size, mechanism, secondary, cleanup);
        UpgradeExternalBleeding(partWound, ComputeExternalBleedTier(mechanism, secondary, size));
        Dirty(ent.Owner, partWound);

        // No-op when a catastrophic fracture or other source already drives a
        // higher rate (recompute picks the max).
        var blunt = GetTypeAmount(args.Delta, "Blunt");
        if (blunt >= SevereBluntInternalBleed)
            SeedInternalBleed(ent.Owner, "blunt", 0.3f);

        if (args.Trauma.VascularContact && args.Trauma.InternalBleedRate > 0f)
            SeedInternalBleed(ent.Owner, $"vascular:{args.Trauma.Mechanism}", args.Trauma.InternalBleedRate);

        if (type == WoundType.Burn
            && burn >= _escharBurnThreshold
            && !HasComp<CMUEscharComponent>(ent.Owner))
        {
            var eschar = AddComp<CMUEscharComponent>(ent.Owner);
            eschar.AppliedAt = Timing.CurTime;
            Dirty(ent.Owner, eschar);
        }
    }

    private void OnBoneFractured(Entity<FractureComponent> ent, ref BoneFracturedEvent args)
    {
        if (!IsEnabled())
            return;
        RecomputeInternalBleed(ent.Owner);
    }

    private void OnSplintStartup(Entity<CMUSplintedComponent> ent, ref ComponentStartup args)
    {
        var ev = new CMUSplintChangedEvent(ent.Owner, false);
        RaiseLocalEvent(ref ev);
    }

    private void OnSplintRemove(Entity<CMUSplintedComponent> ent, ref ComponentRemove args)
    {
        var ev = new CMUSplintChangedEvent(ent.Owner, true);
        RaiseLocalEvent(ref ev);
    }

    private void OnSplintChanged(ref CMUSplintChangedEvent args)
    {
        if (!IsEnabled())
            return;
        RecomputeInternalBleed(args.Part, ignoreSplint: args.Removed);
    }

    private void OnOrganStageChanged(Entity<OrganHealthComponent> ent, ref OrganStageChangedEvent args)
    {
        if (!IsEnabled())
            return;
        if (TryGetContainingPart(ent.Owner) is { } partUid)
            RecomputeInternalBleed(partUid);
    }

    /// <summary>
    ///     Picks the highest-rate active source (fracture / contained organ)
    ///     and (re)applies it. The blunt-impact seed sits outside this pass —
    ///     it's a one-shot spawn in <see cref="OnBodyPartDamaged"/> that
    ///     persists until a higher source overrides or it's cleared.
    /// </summary>
    public void RecomputeInternalBleed(EntityUid part, bool ignoreSplint = false)
    {
        if (IsSynthOwned(part))
        {
            if (HasComp<InternalBleedingComponent>(part))
            {
                RemComp<InternalBleedingComponent>(part);
                RaiseInternalBleedingChanged(part, true);
            }
            return;
        }

        ComputeInternalBleedSource(part, ignoreSplint, out var maxRate, out var source);

        if (maxRate <= 0f)
        {
            if (HasComp<InternalBleedingComponent>(part))
            {
                RemComp<InternalBleedingComponent>(part);
                RaiseInternalBleedingChanged(part, true);
            }
            RemComp<CMUInternalBleedingSuppressedComponent>(part);
            return;
        }

        // Surgical clamping suppresses the source it treated. A worse rate or
        // a different source means the patient has developed a new active IB.
        if (TryComp<CMUInternalBleedingSuppressedComponent>(part, out var suppressed))
        {
            if (IsSuppressedBleedSourceMatch(suppressed.Source, source)
                && maxRate <= suppressed.BloodlossPerSecond + 0.001f)
            {
                if (HasComp<InternalBleedingComponent>(part))
                {
                    RemComp<InternalBleedingComponent>(part);
                    RaiseInternalBleedingChanged(part, true);
                }
                return;
            }

            RemComp<CMUInternalBleedingSuppressedComponent>(part);
        }

        var changed = !TryComp<InternalBleedingComponent>(part, out var before)
            || MathF.Abs(before.BloodlossPerSecond - maxRate) > 0.001f
            || before.Source != source;
        var ib = EnsureComp<InternalBleedingComponent>(part);
        ib.BloodlossPerSecond = maxRate;
        ib.Source = source;
        Dirty(part, ib);
        if (changed)
            RaiseInternalBleedingChanged(part, false);
    }

    private void ComputeInternalBleedSource(EntityUid part, bool ignoreSplint, out float maxRate, out string source)
    {
        maxRate = 0f;
        source = string.Empty;

        if (TryComp<FractureComponent>(part, out var f))
        {
            var profile = FractureProfile.Get(f.Severity);
            var rate = GetSplintAdjustedFractureBleedRate(part, f, (float)profile.BloodlossPerSecond, ignoreSplint);
            if (rate > maxRate)
            {
                maxRate = rate;
                source = $"fracture:{f.Severity}";
            }
        }

        foreach (var (organId, _) in Body.GetPartOrgans(part))
        {
            if (!TryComp<OrganHealthComponent>(organId, out var oh))
                continue;
            if (!oh.Stage.IsAtLeast(oh.InternalBleedAt))
                continue;
            var rate = oh.Stage switch
            {
                OrganDamageStage.Damaged => 0.3f,
                OrganDamageStage.Failing => 0.6f,
                OrganDamageStage.Dead => 1.0f,
                _ => 0f,
            };
            if (rate > maxRate)
            {
                maxRate = rate;
                source = $"organ:{ToShortName(organId)}";
            }
        }

        // Preserve the blunt seed: a transient organ heal back below
        // threshold must not strip a bleed that's actively ticking.
        // Only a stronger fracture / organ rate overrides it.
        if (TryComp<InternalBleedingComponent>(part, out var existing) && IsPersistentSeedSource(existing.Source))
        {
            if (existing.BloodlossPerSecond > maxRate)
            {
                maxRate = existing.BloodlossPerSecond;
                source = existing.Source;
            }
        }
    }

    private static bool IsSuppressedBleedSourceMatch(string suppressed, string current)
    {
        if (suppressed == current)
            return true;

        return suppressed.StartsWith("fracture:", StringComparison.Ordinal)
            && current.StartsWith("fracture:", StringComparison.Ordinal);
    }

    private static bool IsPersistentSeedSource(string source)
        => source == "blunt" || source.StartsWith("vascular:", StringComparison.Ordinal);

    private float GetSplintAdjustedFractureBleedRate(
        EntityUid part,
        FractureComponent fracture,
        float rate,
        bool ignoreSplint)
    {
        if (rate <= 0f || ignoreSplint || !HasComp<CMUSplintedComponent>(part))
            return rate;

        return fracture.Severity == FractureSeverity.Comminuted
            ? rate * SplintedComminutedInternalBleedMultiplier
            : 0f;
    }

    public void SeedInternalBleed(EntityUid part, string source, float rate)
    {
        if (IsSynthOwned(part))
            return;

        RemComp<CMUInternalBleedingSuppressedComponent>(part);

        if (TryComp<InternalBleedingComponent>(part, out var existing) && existing.BloodlossPerSecond >= rate)
            return;

        var changed = !TryComp<InternalBleedingComponent>(part, out var before)
            || MathF.Abs(before.BloodlossPerSecond - rate) > 0.001f
            || before.Source != source;
        var ib = EnsureComp<InternalBleedingComponent>(part);
        ib.BloodlossPerSecond = rate;
        ib.Source = source;
        Dirty(part, ib);
        if (changed)
            RaiseInternalBleedingChanged(part, false);
    }

    public void ClearInternalBleed(EntityUid part)
    {
        ClearInternalBleed(part, false);
    }

    public void SuppressInternalBleed(EntityUid part)
    {
        ClearInternalBleed(part, true);
    }

    private void ClearInternalBleed(EntityUid part, bool suppressCurrentSource)
    {
        if (suppressCurrentSource && !IsSynthOwned(part))
        {
            if (TryComp<InternalBleedingComponent>(part, out var existing))
            {
                var suppressed = EnsureComp<CMUInternalBleedingSuppressedComponent>(part);
                suppressed.Source = existing.Source;
                suppressed.BloodlossPerSecond = existing.BloodlossPerSecond;
            }
            else
            {
                ComputeInternalBleedSource(part, false, out var rate, out var source);
                if (rate > 0f)
                {
                    var suppressed = EnsureComp<CMUInternalBleedingSuppressedComponent>(part);
                    suppressed.Source = source;
                    suppressed.BloodlossPerSecond = rate;
                }
            }
        }

        if (HasComp<InternalBleedingComponent>(part))
        {
            RemComp<InternalBleedingComponent>(part);
            RaiseInternalBleedingChanged(part, true);
        }
    }

    private void RaiseInternalBleedingChanged(EntityUid part, bool removed)
    {
        if (TryGetBodyOwner(part) is not { } body)
            return;
        var ev = new InternalBleedingChangedEvent(body, part, removed);
        RaiseLocalEvent(ref ev);
    }

    public void ClearAllWounds(Entity<BodyPartWoundComponent?> part)
    {
        if (!Resolve(part.Owner, ref part.Comp, logMissing: false))
            return;
        if (part.Comp.Wounds.Count == 0 &&
            part.Comp.Sizes.Count == 0 &&
            part.Comp.Bandages.Count == 0 &&
            part.Comp.ExternalBleeding == ExternalBleedTier.None)
        {
            return;
        }

        part.Comp.Wounds.Clear();
        part.Comp.Sizes.Clear();
        part.Comp.Bandages.Clear();
        part.Comp.Mechanisms.Clear();
        part.Comp.SecondaryMechanisms.Clear();
        part.Comp.TreatmentQualities.Clear();
        part.Comp.Cleanup.Clear();
        ClearExternalBleeding(part.Comp);

        if (TryGetBodyOwner(part.Owner) is { } body)
        {
            OnPartWoundsCleared(body, part.Owner);
            var ev = new WoundTreatedEvent(body, part.Owner);
            RaiseLocalEvent(ref ev);
        }

        if (part.Comp.ExternalBleeding == ExternalBleedTier.None)
            RemComp<BodyPartWoundComponent>(part.Owner);
        else
            Dirty(part.Owner, part.Comp);
    }

    public bool MarkRetainedFragmentCleanup(EntityUid part, int fragments, float severity)
    {
        if (fragments <= 0 || severity <= 0f)
            return false;

        if (TryGetBodyOwner(part) is not { } body || !HasComp<CMUHumanMedicalComponent>(body))
            return false;

        if (IsSynthOwned(part))
            return false;

        var comp = EnsureComp<BodyPartWoundComponent>(part);
        EnsureWoundMetadataSlots(comp);

        var index = FindRetainedFragmentTarget(comp);
        if (index >= 0)
        {
            comp.SecondaryMechanisms[index] |= WoundMechanismFlags.Fragment;
            comp.Cleanup[index] |= WoundCleanupFlags.RetainedFragment;
            Dirty(part, comp);
            return true;
        }

        var size = WoundSizeProfile.FromDamage(MathF.Max(WoundThreshold, severity));
        comp.Wounds.Add(new Wound(FixedPoint2.Zero, FixedPoint2.Zero, 0f, null, WoundType.Brute, true));
        comp.Sizes.Add(size);
        comp.Bandages.Add(WoundSizeProfile.BandagesRequired(size));
        comp.Mechanisms.Add(WoundMechanism.Fragment);
        comp.SecondaryMechanisms.Add(WoundMechanismFlags.None);
        comp.TreatmentQualities.Add(WoundTreatmentQuality.Adequate);
        comp.Cleanup.Add(WoundCleanupFlags.RetainedFragment);
        Dirty(part, comp);
        return true;
    }

    public bool ClearRetainedFragmentCleanup(EntityUid part)
    {
        if (!TryComp<BodyPartWoundComponent>(part, out var comp))
            return false;

        EnsureWoundMetadataSlots(comp);

        var changed = false;
        for (var i = comp.Wounds.Count - 1; i >= 0; i--)
        {
            if ((comp.Cleanup[i] & WoundCleanupFlags.RetainedFragment) == WoundCleanupFlags.None)
                continue;

            comp.Cleanup[i] &= ~WoundCleanupFlags.RetainedFragment;
            changed = true;

            if (comp.Wounds[i].Damage <= FixedPoint2.Zero &&
                comp.Cleanup[i] == WoundCleanupFlags.None &&
                comp.TreatmentQualities[i] != WoundTreatmentQuality.Untreated)
            {
                RemoveWoundAt(comp, i);
            }
        }

        if (!changed)
            return false;

        if (comp.Wounds.Count == 0 && comp.ExternalBleeding == ExternalBleedTier.None)
            RemComp<BodyPartWoundComponent>(part);
        else
            Dirty(part, comp);

        return true;
    }

    /// <summary>
    ///     Applies one bandage to the worst unclosed wound on the part.
    /// </summary>
    public bool TryTreatWound(EntityUid part, BodyPartWoundComponent? comp = null)
        => TryTreatWound(part, out _, comp);

    public bool TryTreatWound(
        EntityUid part,
        WoundType type,
        out bool completed,
        BodyPartWoundComponent? comp = null,
        WoundMechanismFlags mechanismMask = WoundMechanismFlags.None,
        WoundTreatmentQuality quality = WoundTreatmentQuality.Adequate,
        WoundCleanupFlags cleanupClears = WoundCleanupFlags.None,
        bool stopArterialBleeding = true)
        => TryTreatWound(part, out completed, comp, type, quality, mechanismMask, cleanupClears, stopArterialBleeding);

    public bool TryTreatWound(
        EntityUid part,
        WoundTreatmentQuality quality,
        out bool completed,
        BodyPartWoundComponent? comp = null,
        WoundType? type = null,
        WoundMechanismFlags mechanismMask = WoundMechanismFlags.None,
        WoundCleanupFlags cleanupClears = WoundCleanupFlags.None,
        bool stopArterialBleeding = true)
        => TryTreatWound(part, out completed, comp, type, quality, mechanismMask, cleanupClears, stopArterialBleeding);

    /// <summary>
    ///     Applies one bandage to the worst unclosed wound on the part.
    ///     Large wounds require multiple applications before they become
    ///     <c>Treated</c> and start closing.
    /// </summary>
    public bool TryTreatWound(
        EntityUid part,
        out bool completed,
        BodyPartWoundComponent? comp = null,
        WoundType? type = null,
        WoundTreatmentQuality quality = WoundTreatmentQuality.Adequate,
        WoundMechanismFlags mechanismMask = WoundMechanismFlags.None,
        WoundCleanupFlags cleanupClears = WoundCleanupFlags.None,
        bool stopArterialBleeding = true)
    {
        completed = false;
        if (!Resolve(part, ref comp, logMissing: false))
            return false;

        // Defence-in-depth gate: the picker UI pre-filters eschar parts, but
        // direct API callers (admin verbs, tests) reach this path too.
        if (HasComp<CMUEscharComponent>(part))
            return false;

        EnsureBandageSlots(comp);

        var idx = -1;
        var worst = FixedPoint2.Zero;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            var w = comp.Wounds[i];
            if (w.Treated ||
                (type is { } woundType && w.Type != woundType) ||
                !MatchesMechanism(comp, i, mechanismMask))
            {
                continue;
            }

            if (idx < 0 || w.Damage > worst)
            {
                idx = i;
                worst = w.Damage;
            }
        }

        if (idx < 0 && quality == WoundTreatmentQuality.Optimal)
            idx = FindCleanupTreatmentTarget(comp, type, mechanismMask, cleanupClears);

        if (idx < 0)
            return false;

        if (comp.Wounds[idx].Treated)
        {
            CompleteWoundTreatment(part, comp, idx, quality, cleanupClears);
            ClearExternalBleeding(comp, stopArterialBleeding);
            Dirty(part, comp);

            if (TryGetBodyOwner(part) is { } cleanupBody)
            {
                var cleanupEv = new WoundTreatedEvent(cleanupBody, part);
                RaiseLocalEvent(ref cleanupEv);
            }

            completed = true;
            return true;
        }

        var size = GetWoundSize(comp, idx);
        var required = WoundSizeProfile.BandagesRequired(size);
        comp.Bandages[idx] = Math.Min(required, comp.Bandages[idx] + 1);
        completed = comp.Bandages[idx] >= required;

        var picked = comp.Wounds[idx];
        picked = picked with
        {
            Bloodloss = 0f,
            StopBleedAt = Timing.CurTime,
            Treated = completed,
        };
        comp.Wounds[idx] = picked;
        ClearExternalBleeding(comp, stopArterialBleeding);
        if (completed)
            CompleteWoundTreatment(part, comp, idx, quality, cleanupClears);
        Dirty(part, comp);

        // Body resolution can fail on detached parts; the wound is still
        // treated but there's no pain owner to notify, so skip the raise.
        if (completed && TryGetBodyOwner(part) is { } body)
        {
            var ev = new WoundTreatedEvent(body, part);
            RaiseLocalEvent(ref ev);
        }

        return true;
    }

    public bool TryTreatWounds(
        EntityUid part,
        WoundType type,
        int maxWounds,
        out int treated,
        BodyPartWoundComponent? comp = null,
        WoundMechanismFlags mechanismMask = WoundMechanismFlags.None,
        WoundTreatmentQuality quality = WoundTreatmentQuality.Adequate,
        WoundCleanupFlags cleanupClears = WoundCleanupFlags.None,
        bool stopArterialBleeding = true)
    {
        treated = 0;
        if (maxWounds <= 0)
            return false;
        if (!Resolve(part, ref comp, logMissing: false))
            return false;

        // Defence-in-depth gate: the picker UI pre-filters eschar parts, but
        // direct API callers (admin verbs, tests) reach this path too.
        if (HasComp<CMUEscharComponent>(part))
            return false;

        EnsureBandageSlots(comp);

        var now = Timing.CurTime;
        var changed = false;
        while (treated < maxWounds && TryPickWorstUntreatedWound(comp, type, mechanismMask, out var idx))
        {
            var size = GetWoundSize(comp, idx);
            var required = WoundSizeProfile.BandagesRequired(size);
            comp.Bandages[idx] = required;

            var picked = comp.Wounds[idx];
            comp.Wounds[idx] = picked with
            {
                Bloodloss = 0f,
                StopBleedAt = now,
                Treated = true,
            };
            CompleteWoundTreatment(part, comp, idx, quality, cleanupClears);

            treated++;
            changed = true;
        }

        while (treated < maxWounds &&
               quality == WoundTreatmentQuality.Optimal &&
               FindCleanupTreatmentTarget(comp, type, mechanismMask, cleanupClears) is var idx &&
               idx >= 0)
        {
            CompleteWoundTreatment(part, comp, idx, quality, cleanupClears);
            treated++;
            changed = true;
        }

        if (!changed)
            return false;

        ClearExternalBleeding(comp, stopArterialBleeding);
        Dirty(part, comp);

        // Body resolution can fail on detached parts; the wounds are still
        // treated but there's no pain owner to notify, so skip the raise.
        if (TryGetBodyOwner(part) is { } body)
        {
            var ev = new WoundTreatedEvent(body, part);
            RaiseLocalEvent(ref ev);
        }

        return true;
    }

    public bool TryTreatWoundCleanup(
        EntityUid part,
        out bool completed,
        BodyPartWoundComponent? comp = null,
        WoundMechanismFlags mechanismMask = WoundMechanismFlags.None,
        WoundCleanupFlags cleanupClears = WoundCleanupFlags.None,
        bool stopArterialBleeding = true)
    {
        completed = false;
        if (!Resolve(part, ref comp, logMissing: false))
            return false;

        if (HasComp<CMUEscharComponent>(part))
            return false;

        EnsureBandageSlots(comp);

        var idx = FindCleanupTreatmentTarget(comp, null, mechanismMask, cleanupClears);
        if (idx < 0)
            return false;

        CompleteWoundTreatment(part, comp, idx, WoundTreatmentQuality.Optimal, cleanupClears);
        ClearExternalBleeding(comp, stopArterialBleeding);
        Dirty(part, comp);

        if (TryGetBodyOwner(part) is { } body)
        {
            var ev = new WoundTreatedEvent(body, part);
            RaiseLocalEvent(ref ev);
        }

        completed = true;
        return true;
    }

    private static bool TryPickWorstUntreatedWound(
        BodyPartWoundComponent comp,
        WoundType type,
        WoundMechanismFlags mechanismMask,
        out int idx)
    {
        idx = -1;
        var worst = FixedPoint2.Zero;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            var wound = comp.Wounds[i];
            if (wound.Treated || wound.Type != type || !MatchesMechanism(comp, i, mechanismMask))
                continue;
            if (idx < 0 || wound.Damage > worst)
            {
                idx = i;
                worst = wound.Damage;
            }
        }

        return idx >= 0;
    }

    private static int FindCleanupTreatmentTarget(
        BodyPartWoundComponent comp,
        WoundType? type,
        WoundMechanismFlags mechanismMask,
        WoundCleanupFlags cleanupClears)
    {
        EnsureWoundMetadataSlots(comp);

        var idx = -1;
        var worst = FixedPoint2.Zero;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            var wound = comp.Wounds[i];
            if (!wound.Treated ||
                (type is { } woundType && wound.Type != woundType) ||
                !MatchesMechanism(comp, i, mechanismMask) ||
                !CleanupCanBeTreated(comp.Cleanup[i], cleanupClears))
            {
                continue;
            }

            if (idx >= 0 && wound.Damage <= worst)
                continue;

            idx = i;
            worst = wound.Damage;
        }

        return idx;
    }

    private static bool CleanupCanBeTreated(WoundCleanupFlags cleanup, WoundCleanupFlags cleanupClears)
    {
        if (cleanup == WoundCleanupFlags.None)
            return false;

        return cleanupClears == WoundCleanupFlags.None ||
               (cleanup & cleanupClears) != WoundCleanupFlags.None;
    }

    private static bool MatchesMechanism(
        BodyPartWoundComponent comp,
        int index,
        WoundMechanismFlags mechanismMask)
    {
        if (mechanismMask == WoundMechanismFlags.None)
            return true;

        EnsureWoundMetadataSlots(comp);
        if (index < 0 || index >= comp.Wounds.Count)
            return false;

        var primary = index < comp.Mechanisms.Count
            ? ToFlag(comp.Mechanisms[index])
            : WoundMechanismFlags.Generic;
        var secondary = index < comp.SecondaryMechanisms.Count
            ? comp.SecondaryMechanisms[index]
            : WoundMechanismFlags.None;

        return ((primary | secondary) & mechanismMask) != WoundMechanismFlags.None;
    }

    /// <summary>
    ///     Stops the bleed window without marking the wounds Treated.
    ///     Tourniquets use this path: the limb stops bleeding now, but
    ///     bandage flow still owns the <c>Treated</c> transition and
    ///     wound-healing unlock.
    /// </summary>
    public bool StopSurfaceBleedingOnPart(EntityUid part, BodyPartWoundComponent? comp = null)
    {
        if (!Resolve(part, ref comp, logMissing: false))
            return false;

        var now = Timing.CurTime;
        var changed = false;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            var wound = comp.Wounds[i];
            if (wound.Treated)
                continue;

            if (wound.Bloodloss <= 0f && wound.StopBleedAt is { } stopBleedAt && stopBleedAt <= now)
                continue;

            comp.Wounds[i] = wound with { Bloodloss = 0f, StopBleedAt = now };
            changed = true;
        }

        if (!changed)
            return false;

        ClearExternalBleeding(comp);
        Dirty(part, comp);
        return true;
    }

    private void CompleteWoundTreatment(
        EntityUid part,
        BodyPartWoundComponent comp,
        int index,
        WoundTreatmentQuality quality,
        WoundCleanupFlags cleanupClears = WoundCleanupFlags.None)
    {
        EnsureWoundMetadataSlots(comp);

        if (index < 0 || index >= comp.Wounds.Count)
            return;

        if (quality == WoundTreatmentQuality.Optimal)
        {
            comp.Cleanup[index] = cleanupClears == WoundCleanupFlags.None
                ? WoundCleanupFlags.None
                : comp.Cleanup[index] & ~cleanupClears;

            if (comp.Cleanup[index] == WoundCleanupFlags.None)
            {
                comp.TreatmentQualities[index] = WoundTreatmentQuality.Optimal;
                return;
            }

            comp.TreatmentQualities[index] = WoundTreatmentQuality.Adequate;
            RestorePartToFieldCap(part, comp);
            return;
        }

        comp.TreatmentQualities[index] = quality;
        RestorePartToFieldCap(part, comp);
    }

    private void RestorePartToFieldCap(EntityUid part, BodyPartWoundComponent comp)
    {
        PartHealth.RestoreToFractionCap((part, null), ComputeFieldTreatmentCap(comp));
    }

    public static float ComputeFieldTreatmentCap(BodyPartWoundComponent comp)
    {
        EnsureWoundMetadataSlots(comp);

        var penalty = 0f;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            if (!WoundAppliesCapBurden(comp, i))
                continue;

            penalty += FieldTreatmentPenalty(comp, i);
        }

        return Math.Clamp(1f - penalty, 0.35f, 1f);
    }

    private static bool HasCleanupBurden(BodyPartWoundComponent comp)
    {
        EnsureWoundMetadataSlots(comp);

        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            if (WoundAppliesCapBurden(comp, i))
                return true;
        }

        return false;
    }

    private static bool WoundAppliesCapBurden(BodyPartWoundComponent comp, int index)
    {
        if (index < 0 || index >= comp.Wounds.Count)
            return false;

        var quality = index < comp.TreatmentQualities.Count
            ? comp.TreatmentQualities[index]
            : comp.Wounds[index].Treated
                ? WoundTreatmentQuality.Adequate
                : WoundTreatmentQuality.Untreated;

        var cleanup = index < comp.Cleanup.Count
            ? comp.Cleanup[index]
            : WoundCleanupFlags.None;

        return quality == WoundTreatmentQuality.Untreated || cleanup != WoundCleanupFlags.None;
    }

    private static float FieldTreatmentPenalty(BodyPartWoundComponent comp, int index)
    {
        var penalty = FieldTreatmentPenalty(GetWoundSize(comp, index));
        if (index < 0 || index >= comp.Wounds.Count)
            return penalty;

        var quality = index < comp.TreatmentQualities.Count
            ? comp.TreatmentQualities[index]
            : comp.Wounds[index].Treated
                ? WoundTreatmentQuality.Adequate
                : WoundTreatmentQuality.Untreated;
        var cleanup = index < comp.Cleanup.Count
            ? comp.Cleanup[index]
            : WoundCleanupFlags.None;

        return quality == WoundTreatmentQuality.Adequate && cleanup != WoundCleanupFlags.None
            ? penalty * AdequateCleanupBurdenMultiplier
            : penalty;
    }

    private static float FieldTreatmentPenalty(WoundSize size) => size switch
    {
        WoundSize.Small => 0.05f,
        WoundSize.Deep => 0.12f,
        WoundSize.Gaping => 0.20f,
        WoundSize.Massive => 0.30f,
        _ => 0.12f,
    };

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!IsEnabled())
            return;

        _woundScanAccumulator += frameTime;
        if (_woundScanAccumulator < WoundScanInterval)
            return;
        _woundScanAccumulator = 0f;

        var now = Timing.CurTime;
        TickExternalBleed(now);
        TickWoundHealing(now);
        TickInternalBleed(now);
    }

    private void TickExternalBleed(TimeSpan now)
    {
        var query = EntityQueryEnumerator<BodyPartWoundComponent, BodyPartComponent>();
        while (query.MoveNext(out var partUid, out var wounds, out _))
        {
            if (wounds.ExternalBleeding == ExternalBleedTier.None)
                continue;

            if (wounds.NextExternalBleedTick > now)
                continue;

            wounds.NextExternalBleedTick = now + TimeSpan.FromSeconds(1);
            Dirty(partUid, wounds);

            var bodyOwner = TryGetBodyOwner(partUid);
            if (bodyOwner is null)
                continue;

            if (TryComp<MobStateComponent>(bodyOwner, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            ApplyExternalBleed(bodyOwner.Value, partUid, wounds.ExternalBleeding, 1f);
        }
    }

    private void TickWoundHealing(TimeSpan now)
    {
        var query = EntityQueryEnumerator<BodyPartWoundComponent, BodyPartComponent>();
        while (query.MoveNext(out var partUid, out var pw, out var part))
        {
            if (pw.NextHealTick > now)
                continue;

            pw.NextHealTick = now + TimeSpan.FromSeconds(1);

            if (part.Body is not { } body || Unrevivable.IsUnrevivable(body))
                continue;

            EnsureBandageSlots(pw);

            var dirty = false;
            var cleanupBlocked = HasCleanupBurden(pw);
            for (var i = pw.Wounds.Count - 1; i >= 0; i--)
            {
                var w = pw.Wounds[i];
                if (!w.Treated || cleanupBlocked)
                    continue;

                // Scale by the 1s tick cadence, not frameTime.
                var remaining = w.Damage - w.Healed;
                if (remaining <= FixedPoint2.Zero)
                {
                    RemoveWoundAt(pw, i);
                    dirty = true;
                    continue;
                }

                var healing = FixedPoint2.Min((FixedPoint2)HealPerSecond, remaining);
                ApplyWoundHealingDamage(body, partUid, w.Type, healing);

                w = w with { Healed = w.Healed + healing };
                if (w.Healed >= w.Damage)
                {
                    RemoveWoundAt(pw, i);
                    dirty = true;
                }
                else
                {
                    pw.Wounds[i] = w;
                    dirty = true;
                }
            }

            if (pw.Wounds.Count == 0)
            {
                OnPartWoundsCleared(body, partUid);
                RemComp<BodyPartWoundComponent>(partUid);
            }
            else if (dirty)
            {
                Dirty(partUid, pw);
            }
        }
    }

    private void TickInternalBleed(TimeSpan now)
    {
        var tickSeconds = _internalBleedTickSeconds;
        var query = EntityQueryEnumerator<InternalBleedingComponent>();
        while (query.MoveNext(out var partUid, out var ib))
        {
            if (ib.NextBleedTick > now)
                continue;
            ib.NextBleedTick = now + TimeSpan.FromSeconds(tickSeconds);

            // Tourniquet stops bloodflow distal to it, so the bleed tick
            // no-ops while it's on. The necrosis countdown lives in
            // SharedCMUTourniquetSystem.Update.
            if (HasComp<CMUTourniquetComponent>(partUid))
                continue;

            var bodyOwner = TryGetBodyOwner(partUid);
            if (bodyOwner is null)
                continue;

            if (TryComp<MobStateComponent>(bodyOwner, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            ApplyInternalBleed(bodyOwner.Value, partUid, ib.BloodlossPerSecond * tickSeconds);
        }
    }

    /// <summary>
    ///     Server-only side-effect hook; shared no-ops so prediction
    ///     rollback can't double-drain blood volume.
    /// </summary>
    protected virtual void ApplyInternalBleed(EntityUid body, EntityUid part, float amount)
    {
    }

    /// <summary>
    ///     Server-only side-effect hook for external limb bleeding. Shared
    ///     no-ops so prediction rollback can't double-drain blood volume.
    /// </summary>
    protected virtual void ApplyExternalBleed(EntityUid body, EntityUid part, ExternalBleedTier tier, float tickSeconds)
    {
    }

    /// <summary>
    ///     Server-only side-effect hook for treated wounds closing over time.
    ///     Shared no-ops so prediction rollback can't double-heal body damage.
    /// </summary>
    protected virtual void ApplyWoundHealingDamage(EntityUid body, EntityUid part, WoundType type, FixedPoint2 amount)
    {
    }

    /// <summary>
    ///     Server-only reconciliation hook after the wound ledger for a part
    ///     reaches zero. Shared no-ops so prediction rollback can't double-heal
    ///     body damage.
    /// </summary>
    protected virtual void OnPartWoundsCleared(EntityUid body, EntityUid part)
    {
    }

    public EntityUid? TryGetBodyOwner(EntityUid part)
    {
        if (TryComp<BodyPartComponent>(part, out var partComp) && partComp.Body is { } body)
            return body;
        return null;
    }

    private bool IsSynthOwned(EntityUid part)
    {
        if (HasComp<SynthComponent>(part))
            return true;
        return TryGetBodyOwner(part) is { } body && HasComp<SynthComponent>(body);
    }

    public EntityUid? TryGetContainingPart(EntityUid organ)
    {
        if (Containers.TryGetContainingContainer((organ, null, null), out var container)
            && HasComp<BodyPartComponent>(container.Owner))
        {
            return container.Owner;
        }
        // Fallback covers organs where the slot container lookup misses
        // (detached organs that still report OrganComponent.Body).
        if (!TryComp<OrganComponent>(organ, out var organComp) || organComp.Body is not { } bodyId)
            return null;
        foreach (var part in Body.GetBodyChildren(bodyId))
        {
            foreach (var (organId, _) in Body.GetPartOrgans(part.Id, part.Component))
            {
                if (organId == organ)
                    return part.Id;
            }
        }
        return null;
    }

    private FixedPoint2 GroupSum(DamageSpecifier delta, ProtoId<DamageGroupPrototype> group)
    {
        if (!Proto.TryIndex(group, out var groupProto))
            return FixedPoint2.Zero;
        return delta.TryGetDamageInGroup(groupProto, out var total) ? total : FixedPoint2.Zero;
    }

    /// <summary>
    ///     Clamped to a sane window so adversarial damage values can't
    ///     produce half-hour bleeds.
    /// </summary>
    private TimeSpan ComputeBleedDuration(DamageSpecifier delta)
    {
        var slash = GetTypeAmount(delta, "Slash");
        var piercing = GetTypeAmount(delta, "Piercing");
        var blunt = GetTypeAmount(delta, "Blunt");
        var seconds = (slash * 4f) + (piercing * 3f) + (blunt * 1f);
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 5f, 60f));
    }

    private float ComputeBleedAmount(FixedPoint2 brute)
    {
        return brute.Float() * 0.0375f;
    }

    private float GetTypeAmount(DamageSpecifier delta, string typeId)
    {
        return delta.DamageDict.TryGetValue(typeId, out var amount) ? amount.Float() : 0f;
    }

    private string ToShortName(EntityUid organ)
    {
        var meta = MetaData(organ);
        return meta.EntityPrototype is { } proto ? proto.ID : "organ";
    }

    private static WoundSize GetWoundSize(BodyPartWoundComponent comp, int index)
    {
        return index < comp.Sizes.Count ? comp.Sizes[index] : WoundSize.Deep;
    }

    private static void EnsureBandageSlots(BodyPartWoundComponent comp)
    {
        EnsureWoundMetadataSlots(comp);
    }

    private static void EnsureWoundMetadataSlots(BodyPartWoundComponent comp)
    {
        while (comp.Sizes.Count < comp.Wounds.Count)
            comp.Sizes.Add(WoundSize.Deep);

        if (comp.Sizes.Count > comp.Wounds.Count)
            comp.Sizes.RemoveRange(comp.Wounds.Count, comp.Sizes.Count - comp.Wounds.Count);

        while (comp.Bandages.Count < comp.Wounds.Count)
            comp.Bandages.Add(0);

        if (comp.Bandages.Count > comp.Wounds.Count)
            comp.Bandages.RemoveRange(comp.Wounds.Count, comp.Bandages.Count - comp.Wounds.Count);

        while (comp.Mechanisms.Count < comp.Wounds.Count)
            comp.Mechanisms.Add(LegacyMechanismFor(comp.Wounds[comp.Mechanisms.Count].Type));

        if (comp.Mechanisms.Count > comp.Wounds.Count)
            comp.Mechanisms.RemoveRange(comp.Wounds.Count, comp.Mechanisms.Count - comp.Wounds.Count);

        while (comp.SecondaryMechanisms.Count < comp.Wounds.Count)
            comp.SecondaryMechanisms.Add(WoundMechanismFlags.None);

        if (comp.SecondaryMechanisms.Count > comp.Wounds.Count)
            comp.SecondaryMechanisms.RemoveRange(comp.Wounds.Count, comp.SecondaryMechanisms.Count - comp.Wounds.Count);

        while (comp.TreatmentQualities.Count < comp.Wounds.Count)
        {
            var wound = comp.Wounds[comp.TreatmentQualities.Count];
            comp.TreatmentQualities.Add(wound.Treated
                ? WoundTreatmentQuality.Adequate
                : WoundTreatmentQuality.Untreated);
        }

        if (comp.TreatmentQualities.Count > comp.Wounds.Count)
            comp.TreatmentQualities.RemoveRange(comp.Wounds.Count, comp.TreatmentQualities.Count - comp.Wounds.Count);

        while (comp.Cleanup.Count < comp.Wounds.Count)
            comp.Cleanup.Add(WoundCleanupFlags.None);

        if (comp.Cleanup.Count > comp.Wounds.Count)
            comp.Cleanup.RemoveRange(comp.Wounds.Count, comp.Cleanup.Count - comp.Wounds.Count);
    }

    private static void AddOrMergeWound(
        BodyPartWoundComponent comp,
        Wound wound,
        WoundSize size,
        WoundMechanism mechanism,
        WoundMechanismFlags secondary,
        WoundCleanupFlags cleanup)
    {
        EnsureWoundMetadataSlots(comp);

        var index = FindMergeTarget(comp, wound.Type, mechanism);
        if (index < 0)
        {
            comp.Wounds.Add(wound);
            comp.Sizes.Add(size);
            comp.Bandages.Add(0);
            comp.Mechanisms.Add(mechanism);
            comp.SecondaryMechanisms.Add(secondary);
            comp.TreatmentQualities.Add(WoundTreatmentQuality.Untreated);
            comp.Cleanup.Add(cleanup);
            return;
        }

        var existing = comp.Wounds[index];
        var merged = existing with
        {
            Damage = existing.Damage + wound.Damage,
            Bloodloss = existing.Bloodloss + wound.Bloodloss,
            StopBleedAt = MaxTime(existing.StopBleedAt, wound.StopBleedAt),
            Treated = false,
        };

        comp.Wounds[index] = merged;

        var mergedSize = WoundSizeProfile.FromDamage(merged.Damage.Float());
        comp.Sizes[index] = mergedSize;

        var required = WoundSizeProfile.BandagesRequired(mergedSize);
        comp.Bandages[index] = Math.Min(comp.Bandages[index], Math.Max(0, required - 1));

        var existingMechanism = comp.Mechanisms[index];
        if (existingMechanism == WoundMechanism.Generic && mechanism != WoundMechanism.Generic)
            comp.Mechanisms[index] = mechanism;

        if (existingMechanism != mechanism)
            secondary |= ToFlag(mechanism);

        comp.SecondaryMechanisms[index] |= secondary;
        comp.TreatmentQualities[index] = WoundTreatmentQuality.Untreated;
        comp.Cleanup[index] |= cleanup;
    }

    private static int FindMergeTarget(BodyPartWoundComponent comp, WoundType type, WoundMechanism mechanism)
    {
        var index = FindWorstMatchingMechanism(comp, mechanism, exact: true);
        if (index >= 0)
            return index;

        if (comp.Wounds.Count < MaxWoundsPerPart)
            return -1;

        index = FindWorstMatchingMechanism(comp, mechanism, exact: false);
        if (index >= 0)
            return index;

        return FindWorstLegacyWound(comp, type);
    }

    private static int FindRetainedFragmentTarget(BodyPartWoundComponent comp)
    {
        var index = FindCleanupTarget(comp, WoundCleanupFlags.RetainedFragment);
        if (index >= 0)
            return index;

        index = FindAnyMechanism(comp,
            WoundMechanismFlags.Fragment |
            WoundMechanismFlags.Blast |
            WoundMechanismFlags.Bullet);
        if (index >= 0)
            return index;

        index = FindAnySecondaryMechanism(comp,
            WoundMechanismFlags.Fragment |
            WoundMechanismFlags.Blast);
        if (index >= 0)
            return index;

        if (comp.Wounds.Count < MaxWoundsPerPart)
            return -1;

        return FindWorstLegacyWound(comp, WoundType.Brute);
    }

    private static int FindCleanupTarget(BodyPartWoundComponent comp, WoundCleanupFlags flag)
    {
        var index = -1;
        var worst = FixedPoint2.Zero;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            if ((comp.Cleanup[i] & flag) == WoundCleanupFlags.None)
                continue;

            if (index >= 0 && comp.Wounds[i].Damage <= worst)
                continue;

            index = i;
            worst = comp.Wounds[i].Damage;
        }

        return index;
    }

    private static int FindAnyMechanism(BodyPartWoundComponent comp, WoundMechanismFlags mechanismMask)
    {
        var index = -1;
        var worst = FixedPoint2.Zero;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            var mechanism = ToFlag(comp.Mechanisms[i]);
            if ((mechanism & mechanismMask) == WoundMechanismFlags.None)
                continue;

            if (index >= 0 && comp.Wounds[i].Damage <= worst)
                continue;

            index = i;
            worst = comp.Wounds[i].Damage;
        }

        return index;
    }

    private static int FindAnySecondaryMechanism(BodyPartWoundComponent comp, WoundMechanismFlags mechanismMask)
    {
        var index = -1;
        var worst = FixedPoint2.Zero;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            var secondary = comp.SecondaryMechanisms[i];
            if ((secondary & mechanismMask) == WoundMechanismFlags.None)
                continue;

            if (index >= 0 && comp.Wounds[i].Damage <= worst)
                continue;

            index = i;
            worst = comp.Wounds[i].Damage;
        }

        return index;
    }

    private static int FindWorstMatchingMechanism(BodyPartWoundComponent comp, WoundMechanism mechanism, bool exact)
    {
        var index = -1;
        var worst = FixedPoint2.Zero;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            var wound = comp.Wounds[i];
            if (wound.Treated)
                continue;

            var existing = comp.Mechanisms[i];
            var match = exact
                ? existing == mechanism
                : SameMergeFamily(existing, mechanism);

            if (!match)
                continue;

            if (index >= 0 && wound.Damage <= worst)
                continue;

            index = i;
            worst = wound.Damage;
        }

        return index;
    }

    private static int FindWorstLegacyWound(BodyPartWoundComponent comp, WoundType type)
    {
        var index = -1;
        var worst = FixedPoint2.Zero;
        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            var wound = comp.Wounds[i];
            if (wound.Treated || wound.Type != type)
                continue;

            if (index >= 0 && wound.Damage <= worst)
                continue;

            index = i;
            worst = wound.Damage;
        }

        if (index >= 0)
            return index;

        for (var i = 0; i < comp.Wounds.Count; i++)
        {
            var wound = comp.Wounds[i];
            if (index >= 0 && wound.Damage <= worst)
                continue;

            index = i;
            worst = wound.Damage;
        }

        return index;
    }

    private static WoundMechanism ClassifyMechanism(in BodyPartDamagedEvent args, FixedPoint2 brute, FixedPoint2 burn)
    {
        if (args.Trauma.Mechanism == CMUTraumaMechanism.Explosive ||
            args.Impact.Delivery == DamageImpactDelivery.Explosion ||
            args.Impact.Contact == DamageImpactContact.Blast)
        {
            return WoundMechanism.Blast;
        }

        if (args.Impact.Contact == DamageImpactContact.Fragment)
            return WoundMechanism.Fragment;

        if (burn > brute && burn > FixedPoint2.Zero)
            return WoundMechanism.Burn;

        if (args.Impact.Delivery == DamageImpactDelivery.Projectile)
            return WoundMechanism.Bullet;

        return args.Trauma.Mechanism switch
        {
            CMUTraumaMechanism.Ballistic => WoundMechanism.Bullet,
            CMUTraumaMechanism.Pierce => WoundMechanism.Stab,
            CMUTraumaMechanism.Slash => WoundMechanism.Slash,
            CMUTraumaMechanism.Blunt => WoundMechanism.Crush,
            _ => ClassifyMechanismFromImpact(args.Impact, brute, burn),
        };
    }

    private static WoundMechanism ClassifyMechanismFromImpact(DamageImpact impact, FixedPoint2 brute, FixedPoint2 burn)
    {
        if (burn > brute && burn > FixedPoint2.Zero)
            return WoundMechanism.Burn;

        if (impact.Delivery == DamageImpactDelivery.Projectile)
            return WoundMechanism.Bullet;

        return impact.Contact switch
        {
            DamageImpactContact.Stab => WoundMechanism.Stab,
            DamageImpactContact.Slash or DamageImpactContact.Snag => WoundMechanism.Slash,
            DamageImpactContact.Crush => WoundMechanism.Crush,
            DamageImpactContact.Burn => WoundMechanism.Burn,
            DamageImpactContact.Blast => WoundMechanism.Blast,
            DamageImpactContact.Fragment => WoundMechanism.Fragment,
            _ when burn > brute && burn > FixedPoint2.Zero => WoundMechanism.Burn,
            _ when brute > FixedPoint2.Zero => WoundMechanism.Crush,
            _ => WoundMechanism.Generic,
        };
    }

    private static WoundMechanismFlags ClassifySecondaryMechanisms(
        in BodyPartDamagedEvent args,
        WoundMechanism primary,
        FixedPoint2 brute,
        FixedPoint2 burn)
    {
        var flags = WoundMechanismFlags.None;

        AddSecondary(ref flags, primary, burn > FixedPoint2.Zero, WoundMechanism.Burn);
        AddSecondary(ref flags, primary, args.Impact.Contact == DamageImpactContact.Fragment, WoundMechanism.Fragment);
        AddSecondary(ref flags, primary, args.Impact.Contact == DamageImpactContact.Blast ||
            args.Impact.Delivery == DamageImpactDelivery.Explosion ||
            args.Trauma.Mechanism == CMUTraumaMechanism.Explosive, WoundMechanism.Blast);

        var blunt = DamageTypeAmount(args.Delta, "Blunt");
        AddSecondary(ref flags, primary, blunt > FixedPoint2.Zero && brute > burn, WoundMechanism.Crush);

        return flags;
    }

    private static void AddSecondary(
        ref WoundMechanismFlags flags,
        WoundMechanism primary,
        bool present,
        WoundMechanism secondary)
    {
        if (!present || primary == secondary)
            return;

        flags |= ToFlag(secondary);
    }

    private static WoundCleanupFlags DefaultCleanupFor(
        WoundMechanism mechanism,
        WoundMechanismFlags secondary,
        WoundSize size)
    {
        var cleanup = WoundCleanupFlags.DirtyDressing;

        if ((secondary & WoundMechanismFlags.Fragment) != WoundMechanismFlags.None)
            cleanup |= WoundCleanupFlags.RetainedFragment;

        cleanup |= mechanism switch
        {
            WoundMechanism.Fragment => WoundCleanupFlags.RetainedFragment,
            WoundMechanism.Burn => WoundCleanupFlags.CharredTissue,
            WoundMechanism.Blast => WoundCleanupFlags.CrushDebris,
            WoundMechanism.Crush => size >= WoundSize.Deep ? WoundCleanupFlags.CrushDebris : WoundCleanupFlags.None,
            WoundMechanism.Stab or WoundMechanism.Slash or WoundMechanism.Surgical => WoundCleanupFlags.PoorClosure,
            _ => WoundCleanupFlags.None,
        };

        return cleanup;
    }

    private static ExternalBleedTier ComputeExternalBleedTier(
        WoundMechanism mechanism,
        WoundMechanismFlags secondary,
        WoundSize size)
    {
        if (mechanism == WoundMechanism.Burn &&
            (secondary & (WoundMechanismFlags.Blast | WoundMechanismFlags.Fragment)) == WoundMechanismFlags.None)
        {
            return ExternalBleedTier.None;
        }

        return mechanism switch
        {
            WoundMechanism.Blast => size >= WoundSize.Gaping ? ExternalBleedTier.Severe : ExternalBleedTier.Moderate,
            WoundMechanism.Bullet or WoundMechanism.Stab or WoundMechanism.Slash or WoundMechanism.Fragment => size switch
            {
                WoundSize.Small => ExternalBleedTier.Minor,
                WoundSize.Deep => ExternalBleedTier.Moderate,
                WoundSize.Gaping => ExternalBleedTier.Severe,
                WoundSize.Massive => ExternalBleedTier.Arterial,
                _ => ExternalBleedTier.Moderate,
            },
            WoundMechanism.Crush => size >= WoundSize.Gaping ? ExternalBleedTier.Moderate : ExternalBleedTier.Minor,
            _ => ExternalBleedTier.None,
        };
    }

    private static void UpgradeExternalBleeding(BodyPartWoundComponent comp, ExternalBleedTier tier)
    {
        if (tier > comp.ExternalBleeding)
            comp.ExternalBleeding = tier;
    }

    private static void ClearExternalBleeding(BodyPartWoundComponent comp)
    {
        comp.ExternalBleeding = ExternalBleedTier.None;
        comp.ExternalBleedSuppressedUntil = default;
        comp.NextExternalBleedTick = default;
    }

    private static void ClearExternalBleeding(BodyPartWoundComponent comp, bool stopArterialBleeding)
    {
        if (!stopArterialBleeding && comp.ExternalBleeding == ExternalBleedTier.Arterial)
            return;

        ClearExternalBleeding(comp);
    }

    private static WoundMechanism LegacyMechanismFor(WoundType type)
    {
        return type switch
        {
            WoundType.Burn => WoundMechanism.Burn,
            WoundType.Surgery => WoundMechanism.Surgical,
            _ => WoundMechanism.Generic,
        };
    }

    private static bool SameMergeFamily(WoundMechanism a, WoundMechanism b)
    {
        return MergeFamily(a) == MergeFamily(b);
    }

    private static byte MergeFamily(WoundMechanism mechanism)
    {
        return mechanism switch
        {
            WoundMechanism.Bullet or WoundMechanism.Stab or WoundMechanism.Fragment => 1,
            WoundMechanism.Slash or WoundMechanism.Surgical => 2,
            WoundMechanism.Crush or WoundMechanism.Blast => 3,
            WoundMechanism.Burn => 4,
            _ => 0,
        };
    }

    private static WoundMechanismFlags ToFlag(WoundMechanism mechanism)
    {
        return mechanism switch
        {
            WoundMechanism.Bullet => WoundMechanismFlags.Bullet,
            WoundMechanism.Stab => WoundMechanismFlags.Stab,
            WoundMechanism.Slash => WoundMechanismFlags.Slash,
            WoundMechanism.Crush => WoundMechanismFlags.Crush,
            WoundMechanism.Burn => WoundMechanismFlags.Burn,
            WoundMechanism.Blast => WoundMechanismFlags.Blast,
            WoundMechanism.Fragment => WoundMechanismFlags.Fragment,
            WoundMechanism.Surgical => WoundMechanismFlags.Surgical,
            _ => WoundMechanismFlags.Generic,
        };
    }

    private static FixedPoint2 DamageTypeAmount(DamageSpecifier delta, string typeId)
    {
        return delta.DamageDict.TryGetValue(typeId, out var amount)
            ? amount
            : FixedPoint2.Zero;
    }

    private static TimeSpan? MaxTime(TimeSpan? a, TimeSpan? b)
    {
        if (a is null)
            return b;
        if (b is null)
            return a;
        return a > b ? a : b;
    }

    private static void RemoveWoundAt(BodyPartWoundComponent comp, int index)
    {
        comp.Wounds.RemoveAt(index);

        if (index < comp.Sizes.Count)
            comp.Sizes.RemoveAt(index);

        if (index < comp.Bandages.Count)
            comp.Bandages.RemoveAt(index);

        if (index < comp.Mechanisms.Count)
            comp.Mechanisms.RemoveAt(index);

        if (index < comp.SecondaryMechanisms.Count)
            comp.SecondaryMechanisms.RemoveAt(index);

        if (index < comp.TreatmentQualities.Count)
            comp.TreatmentQualities.RemoveAt(index);

        if (index < comp.Cleanup.Count)
            comp.Cleanup.RemoveAt(index);
    }
}
