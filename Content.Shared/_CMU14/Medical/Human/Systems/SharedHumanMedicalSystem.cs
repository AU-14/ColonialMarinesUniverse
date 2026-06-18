using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Presentation;
using Content.Shared._RMC14.Synth;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed partial class SharedHumanMedicalSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalComponent, ComponentStartup>(OnHumanMedicalStartup);
        SubscribeLocalEvent<HumanMedicalComponent, ComponentShutdown>(OnHumanMedicalShutdown);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveMedicalSummaryDirtyComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            if (!HumanMedicalLedger.RebuildSummaryIfDirty(medical))
            {
                RefreshActiveMarkers(uid, medical);
                continue;
            }

            if (TryComp<HumanMedicalSummaryComponent>(uid, out var summary))
            {
                summary.Summary = medical.Summary;
                Dirty(uid, summary);
            }

            RefreshActiveMarkers(uid, medical);
        }
    }

    private void OnHumanMedicalStartup(Entity<HumanMedicalComponent> body, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        if (HasComp<SynthComponent>(body.Owner))
        {
            ClearActiveMarkers(body.Owner);
            RemCompDeferred<HumanMedicalComponent>(body.Owner);
            RemCompDeferred<HumanMedicalSummaryComponent>(body.Owner);
            RemCompDeferred<HumanMedicalVisualsComponent>(body.Owner);
            return;
        }

        HumanMedicalLedger.EnsureInitialized(body.Comp);
        RefreshActiveMarkers(body.Owner, body.Comp);
        RefreshVisuals(body.Owner, body.Comp);

        var ev = new HumanMedicalStartupEvent(body.Owner, body.Comp);
        RaiseLocalEvent(ref ev);
    }

    private void OnHumanMedicalShutdown(Entity<HumanMedicalComponent> body, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        ClearActiveMarkers(body.Owner);
        RemComp<HumanMedicalVisualsComponent>(body.Owner);

        var ev = new HumanMedicalShutdownEvent(body.Owner);
        RaiseLocalEvent(ref ev);
    }

    public MedicalTransactionResult ApplyTransaction(
        Entity<HumanMedicalComponent> body,
        MedicalTransaction transaction)
    {
        if (_net.IsClient)
            return new MedicalTransactionResult(false, body.Comp.Revision, MedicalDirtyFlags.None, "Human medical ledger is server-authoritative.");

        HumanMedicalLedger.EnsureInitialized(body.Comp);
        var widenedRegions = GetWidenedWoundRegions(body.Comp, transaction);
        var result = HumanMedicalLedger.ApplyTransaction(body.Comp, transaction);
        if (!result.Applied)
            return result;

        RefreshActiveMarkers(body.Owner, body.Comp);
        RefreshVisuals(body.Owner, body.Comp);

        var ev = new HumanMedicalLedgerChangedEvent(body.Owner, result);
        RaiseLocalEvent(ref ev);

        RaiseTransactionPresentationEvents(body.Owner, transaction, widenedRegions);

        return result;
    }

    public void RefreshActiveMarkers(EntityUid uid, HumanMedicalComponent medical)
    {
        if (_net.IsClient)
            return;

        var activity = MedicalActivityClassifier.Classify(medical);

        SetMarker<ActiveBleedingComponent>(uid, activity.HasFlag(MedicalActivityFlags.ActiveBleeding));
        SetMarker<ActiveOrganSymptomsComponent>(uid, activity.HasFlag(MedicalActivityFlags.ActiveOrganSymptoms));
        SetMarker<ActiveBoneKnittingComponent>(uid, activity.HasFlag(MedicalActivityFlags.ActiveBoneKnitting));
        SetMarker<ActiveUnsplintedFractureRiskComponent>(uid, activity.HasFlag(MedicalActivityFlags.ActiveUnsplintedFractureRisk));
        SetMarker<ActiveEmbeddedObjectMovementComponent>(uid, activity.HasFlag(MedicalActivityFlags.ActiveEmbeddedObjectMovement));
        SetMarker<ActiveTourniquetComponent>(uid, activity.HasFlag(MedicalActivityFlags.ActiveTourniquet));
        SetMarker<ActiveTreatedWoundHealingComponent>(uid, activity.HasFlag(MedicalActivityFlags.ActiveTreatedWoundHealing));
        SetMarker<ActiveMedicalSummaryDirtyComponent>(uid, activity.HasFlag(MedicalActivityFlags.ActiveMedicalSummaryDirty));

        RefreshVisuals(uid, medical);
    }

    public void RefreshVisuals(EntityUid uid, HumanMedicalComponent medical)
    {
        if (_net.IsClient)
            return;

        var visuals = EnsureComp<HumanMedicalVisualsComponent>(uid);
        if (visuals.RegionFlags.Length != HumanMedicalComponent.RegionSlotCount)
            visuals.RegionFlags = new HumanMedicalRegionVisualFlags[HumanMedicalComponent.RegionSlotCount];

        var changed = visuals.Revision != medical.Revision;
        for (var i = 1; i < visuals.RegionFlags.Length; i++)
        {
            var region = (BodyRegion) i;
            var flags = BuildRegionVisualFlags(medical, region);
            if (visuals.RegionFlags[i] == flags)
                continue;

            visuals.RegionFlags[i] = flags;
            changed = true;
        }

        if (!changed)
            return;

        visuals.Revision = medical.Revision;
        Dirty(uid, visuals);
    }

    private void SetMarker<T>(EntityUid uid, bool enabled) where T : Component, new()
    {
        if (enabled)
        {
            EnsureComp<T>(uid);
            return;
        }

        RemComp<T>(uid);
    }

    private void ClearActiveMarkers(EntityUid uid)
    {
        RemComp<ActiveBleedingComponent>(uid);
        RemComp<ActiveOrganSymptomsComponent>(uid);
        RemComp<ActiveBoneKnittingComponent>(uid);
        RemComp<ActiveUnsplintedFractureRiskComponent>(uid);
        RemComp<ActiveEmbeddedObjectMovementComponent>(uid);
        RemComp<ActiveTourniquetComponent>(uid);
        RemComp<ActiveTreatedWoundHealingComponent>(uid);
        RemComp<ActiveMedicalSummaryDirtyComponent>(uid);
    }

    private static HumanMedicalRegionVisualFlags BuildRegionVisualFlags(
        HumanMedicalComponent medical,
        BodyRegion region)
    {
        var state = HumanMedicalLedger.GetRegion(medical, region);
        var flags = HumanMedicalRegionVisualFlags.None;

        if (state.Presence == LimbPresence.Prosthetic)
            flags |= HumanMedicalRegionVisualFlags.Prosthetic;

        if (state.Skeletal.Splinted)
            flags |= state.Skeletal.Knitting
                ? HumanMedicalRegionVisualFlags.Casted
                : HumanMedicalRegionVisualFlags.Splinted;

        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            if (injury.Region != region ||
                injury.Flags.HasFlag(InjuryFlags.Closed))
            {
                continue;
            }

            if (injury.Flags.HasFlag(InjuryFlags.Bandaged))
            {
                flags |= HumanMedicalRegionVisualFlags.Bandaged;
                break;
            }
        }

        return flags;
    }

    private static List<BodyRegion> GetWidenedWoundRegions(
        HumanMedicalComponent medical,
        MedicalTransaction transaction)
    {
        var regions = new List<BodyRegion>();
        var effects = transaction.Effects.Span;
        for (var i = 0; i < effects.Length; i++)
        {
            ref readonly var effect = ref effects[i];
            if (effect.Kind != MedicalEffectKind.AddInjury ||
                !CanPresentAsWoundWidening(effect.InjuryKind))
            {
                continue;
            }

            if (HasCompatibleOpenInjury(medical, effect.Region, effect.InjuryKind))
                regions.Add(effect.Region);
        }

        return regions;
    }

    private void RaiseTransactionPresentationEvents(
        EntityUid body,
        MedicalTransaction transaction,
        List<BodyRegion> widenedRegions)
    {
        var effects = transaction.Effects.Span;
        for (var i = 0; i < effects.Length; i++)
        {
            ref readonly var effect = ref effects[i];
            if (effect.Kind != MedicalEffectKind.AddBleedSource ||
                effect.BleedKind != BleedKind.Internal)
            {
                continue;
            }

            var ev = new HumanInternalRipEvent(body, effect.Region);
            RaiseLocalEvent(body, ref ev);
        }

        for (var i = 0; i < widenedRegions.Count; i++)
        {
            var ev = new HumanWoundWidenedEvent(body, widenedRegions[i]);
            RaiseLocalEvent(body, ref ev);
        }
    }

    private static bool HasCompatibleOpenInjury(
        HumanMedicalComponent medical,
        BodyRegion region,
        InjuryKind kind)
    {
        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            if (injury.Region == region &&
                injury.Kind == kind &&
                CanPresentAsWoundWidening(injury.Kind) &&
                !injury.Flags.HasFlag(InjuryFlags.Closed) &&
                !injury.Flags.HasFlag(InjuryFlags.Sutured))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanPresentAsWoundWidening(InjuryKind kind)
    {
        return kind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Bruise;
    }
}

[ByRefEvent]
public readonly record struct HumanMedicalLedgerChangedEvent(
    EntityUid Body,
    MedicalTransactionResult Result);

[ByRefEvent]
public readonly record struct HumanMedicalStartupEvent(
    EntityUid Body,
    HumanMedicalComponent Medical);

[ByRefEvent]
public readonly record struct HumanMedicalShutdownEvent(EntityUid Body);

[ByRefEvent]
public readonly record struct HumanSplintBrokenEvent(EntityUid Body, BodyRegion Region);

[ByRefEvent]
public readonly record struct HumanInternalRipEvent(EntityUid Body, BodyRegion Region);

[ByRefEvent]
public readonly record struct HumanWoundWidenedEvent(EntityUid Body, BodyRegion Region);
