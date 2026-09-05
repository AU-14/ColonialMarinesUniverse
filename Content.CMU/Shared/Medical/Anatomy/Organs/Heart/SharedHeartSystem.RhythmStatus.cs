using System.Collections.Generic;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Core;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;

public abstract partial class SharedHeartSystem
{
    // Drug effects retain their existing timed prototypes. Tissue owns only these
    // derived sources; both display the same shared alert and marker components.
    private static readonly EntProtoId TissueTachycardia = "StatusEffectCMUHeartTachycardia";
    private static readonly EntProtoId TissueArrhythmia = "StatusEffectCMUHeartArrhythmia";
    private readonly HashSet<EntityUid> _reconcilingRhythms = new();
    private readonly HashSet<EntityUid> _pendingRhythms = new();

    private void InitializeRhythmStatus()
    {
        // The directed aggregate event slot belongs to respiratory projection.
        SubscribeLocalEvent<CMUMedicalChangedEvent>(OnRhythmTopologyChanged);
        SubscribeLocalEvent<OrganHealthLifecycleChangedEvent>(OnRhythmHealthLifecycleChanged);
    }

    private void OnRhythmHealthLifecycleChanged(ref OrganHealthLifecycleChangedEvent args)
    {
        if (args.Body is { } body && HasComp<HeartComponent>(args.Organ))
            ReconcileRhythmStatus(body);
    }

    private void OnRhythmTopologyChanged(ref CMUMedicalChangedEvent args)
    {
        if ((args.Changes & CMUMedicalChangeFlags.Topology) != 0)
            ReconcileRhythmStatus(args.Body);
    }

    private void RefreshAllRhythmStatuses()
    {
        if (_net.IsClient)
            return;

        // Configuration is a rare boundary and must include paused patients and
        // patients whose last heart has been removed since the previous projection.
        var patients = new List<EntityUid>();
        var query = EntityManager.AllEntityQueryEnumerator<BodyComponent>();
        while (query.MoveNext(out var uid, out _))
            patients.Add(uid);
        foreach (var patient in patients)
            ReconcileRhythmStatus(patient);
    }

    private void ReconcileRhythmStatus(EntityUid body)
    {
        if (!IsCurrentRhythmBody(body))
            return;
        if (!_reconcilingRhythms.Add(body))
        {
            _pendingRhythms.Add(body);
            return;
        }

        try
        {
            do
            {
                _pendingRhythms.Remove(body);
                var rhythm = GetTissueRhythm(body);
                SetTissueRhythm(body, TissueTachycardia, rhythm.Tachycardia);
                if (!IsCurrentRhythmBody(body))
                    return;
                // Adding/removing a status is a public callback boundary. Re-read
                // anatomy before the second projection and repeat if nested tissue
                // changes requested reconciliation during either mutation.
                rhythm = GetTissueRhythm(body);
                SetTissueRhythm(body, TissueArrhythmia, rhythm.Arrhythmia);
            }
            while (IsCurrentRhythmBody(body) && _pendingRhythms.Contains(body));
        }
        finally
        {
            _pendingRhythms.Remove(body);
            _reconcilingRhythms.Remove(body);
        }
    }

    private bool IsCurrentRhythmBody(EntityUid body)
        => _net.IsServer && !TerminatingOrDeleted(body) && !EntityManager.IsQueuedForDeletion(body) &&
           TryComp<BodyComponent>(body, out var component) && component.LifeStage <= ComponentLifeStage.Running;

    private (bool Tachycardia, bool Arrhythmia) GetTissueRhythm(EntityUid body)
    {
        if (!Enabled)
            return default;

        var tachycardia = false;
        var arrhythmia = false;
        foreach (var organ in MedicalIndex.GetOrgans(body))
        {
            if (organ.Comp.Body != body || TerminatingOrDeleted(organ.Owner) ||
                EntityManager.IsQueuedForDeletion(organ.Owner) || organ.Comp.LifeStage > ComponentLifeStage.Running ||
                !TryComp<HeartComponent>(organ.Owner, out var heart) || heart.LifeStage > ComponentLifeStage.Running ||
                !TryComp<OrganHealthComponent>(organ.Owner, out var health) || health.LifeStage > ComponentLifeStage.Running ||
                !TryComp<ChildOrganComponent>(organ.Owner, out var relation) || relation.LifeStage > ComponentLifeStage.Running ||
                relation.Parent is not { } parent ||
                !TryComp<BodyPartComponent>(parent, out var part) || part.Body != body ||
                part.LifeStage > ComponentLifeStage.Running || TerminatingOrDeleted(parent) || EntityManager.IsQueuedForDeletion(parent))
                continue;

            // Each viable attached heart retains its established stage indication.
            // A healthy/dead second heart cannot erase another heart's symptoms.
            tachycardia |= health.Stage == OrganDamageStage.Bruised;
            arrhythmia |= health.Stage is OrganDamageStage.Damaged or OrganDamageStage.Failing;
        }
        return (tachycardia, arrhythmia);
    }

    private void SetTissueRhythm(EntityUid body, EntProtoId prototype, bool active)
    {
        if (active)
        {
            if (Status.TryGetStatusEffect(body, prototype, out var current) &&
                !EntityManager.IsQueuedForDeletion(current.Value))
                return;
            if (current is { } retiring && !TerminatingOrDeleted(retiring))
                Del(retiring);
            if (IsCurrentRhythmBody(body))
                Status.TrySetStatusEffectDuration(body, prototype, duration: null);
            return;
        }

        // Only our derived source is retired. Immediate deletion prevents a heal ->
        // re-injure in one tick from renewing an entity already queued for deletion.
        if (Status.TryGetStatusEffect(body, prototype, out var effect))
            Del(effect.Value);
    }
}
