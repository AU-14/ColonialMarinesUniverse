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
public abstract class SharedCMUWoundsSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] protected readonly SharedContainerSystem Containers = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly RMCUnrevivableSystem Unrevivable = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    /// <summary>
    ///     Minimum total Brute+Burn for a single
    ///     <see cref="BodyPartDamagedEvent"/> to spawn a wound entry. Below
    ///     this threshold tiny chips of damage don't accumulate into the
    ///     per-part list.
    /// </summary>
    public const float WoundThreshold = 5f;

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

        AddOrMergeWound(partWound, new Wound(
            bruteOrBurn,
            FixedPoint2.Zero,
            bloodloss,
            stopBleedAt,
            type,
            false), size);
        Dirty(ent.Owner, partWound);

        // No-op when a catastrophic fracture or other source already drives a
        // higher rate (recompute picks the max).
        var blunt = GetTypeAmount(args.Delta, "Blunt");
        if (blunt >= SevereBluntInternalBleed)
            SeedInternalBleed(ent.Owner, "blunt", 0.3f);

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
        RaiseLocalEvent(new CMUSplintChangedEvent(ent.Owner, false));
    }

    private void OnSplintRemove(Entity<CMUSplintedComponent> ent, ref ComponentRemove args)
    {
        RaiseLocalEvent(new CMUSplintChangedEvent(ent.Owner, true));
    }

    private void OnSplintChanged(CMUSplintChangedEvent args)
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
        if (TryComp<InternalBleedingComponent>(part, out var existing) && existing.Source == "blunt")
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
        RaiseLocalEvent(new InternalBleedingChangedEvent(body, part, removed));
    }

    public void ClearAllWounds(Entity<BodyPartWoundComponent?> part)
    {
        if (!Resolve(part.Owner, ref part.Comp, logMissing: false))
            return;
        if (part.Comp.Wounds.Count == 0 && part.Comp.Sizes.Count == 0 && part.Comp.Bandages.Count == 0)
            return;
        part.Comp.Wounds.Clear();
        part.Comp.Sizes.Clear();
        part.Comp.Bandages.Clear();
        Dirty(part.Owner, part.Comp);

        if (TryGetBodyOwner(part.Owner) is { } body)
            RaiseLocalEvent(new WoundTreatedEvent(body, part.Owner));
    }

    /// <summary>
    ///     Applies one bandage to the worst unclosed wound on the part.
    /// </summary>
    public bool TryTreatWound(EntityUid part, BodyPartWoundComponent? comp = null)
        => TryTreatWound(part, out _, comp);

    /// <summary>
    ///     Applies one bandage to the worst unclosed wound on the part.
    ///     Large wounds require multiple applications before they become
    ///     <c>Treated</c> and start closing.
    /// </summary>
    public bool TryTreatWound(EntityUid part, out bool completed, BodyPartWoundComponent? comp = null)
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
            if (w.Treated)
                continue;
            if (idx < 0 || w.Damage > worst)
            {
                idx = i;
                worst = w.Damage;
            }
        }
        if (idx < 0)
            return false;

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
        Dirty(part, comp);

        // Body resolution can fail on detached parts; the wound is still
        // treated but there's no pain owner to notify, so skip the raise.
        if (completed && TryGetBodyOwner(part) is { } body)
            RaiseLocalEvent(new WoundTreatedEvent(body, part));

        return true;
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

        Dirty(part, comp);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!IsEnabled())
            return;

        var now = Timing.CurTime;
        TickWoundHealing(frameTime, now);
        TickInternalBleed(now);
    }

    private void TickWoundHealing(float frameTime, TimeSpan now)
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
            for (var i = pw.Wounds.Count - 1; i >= 0; i--)
            {
                var w = pw.Wounds[i];
                if (!w.Treated)
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
            Dirty(partUid, ib);

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
    ///     Server-only side-effect hook for treated wounds closing over time.
    ///     Shared no-ops so prediction rollback can't double-heal body damage.
    /// </summary>
    protected virtual void ApplyWoundHealingDamage(EntityUid body, EntityUid part, WoundType type, FixedPoint2 amount)
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
        while (comp.Bandages.Count < comp.Wounds.Count)
            comp.Bandages.Add(0);

        if (comp.Bandages.Count > comp.Wounds.Count)
            comp.Bandages.RemoveRange(comp.Wounds.Count, comp.Bandages.Count - comp.Wounds.Count);
    }

    private static void AddOrMergeWound(BodyPartWoundComponent comp, Wound wound, WoundSize size)
    {
        EnsureBandageSlots(comp);

        var index = FindMergeTarget(comp, wound.Type);
        if (index < 0)
        {
            comp.Wounds.Add(wound);
            comp.Sizes.Add(size);
            comp.Bandages.Add(0);
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
        while (comp.Sizes.Count <= index)
            comp.Sizes.Add(WoundSize.Deep);

        var mergedSize = WoundSizeProfile.FromDamage(merged.Damage.Float());
        comp.Sizes[index] = mergedSize;

        var required = WoundSizeProfile.BandagesRequired(mergedSize);
        comp.Bandages[index] = Math.Min(comp.Bandages[index], Math.Max(0, required - 1));
    }

    private static int FindMergeTarget(BodyPartWoundComponent comp, WoundType type)
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

        return index;
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
    }
}
