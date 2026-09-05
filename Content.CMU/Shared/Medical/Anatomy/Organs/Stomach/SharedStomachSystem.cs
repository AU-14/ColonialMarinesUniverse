using System.Linq;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Stomach;

public abstract partial class SharedStomachSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected StatusEffectsSystem Status = default!;
    [Dependency] protected CMStasisBagSystem Stasis = default!;

    private static readonly EntProtoId Nausea = "StatusEffectCMUNausea";
    private const float StomachScanInterval = 1f;
    private float _stomachScanAccumulator;
    private bool _medicalEnabled;
    private bool _organEnabled;
    private bool Enabled => _medicalEnabled && _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUOrganPhysiologyBoundaryEvent>(OnBodyPhysiologyBoundary);
        SubscribeLocalEvent<CMUStomachComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<CMUStomachComponent, ComponentStartup>(OnStomachStartup,
            after: new[] { typeof(SharedOrganHealthSystem) });
        SubscribeLocalEvent<CMUStomachComponent, OrganRemovedFromBodyEvent>(OnStomachRemovedFromBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<CMUStomachComponent, OrganAddedToBodyEvent>(OnStomachAddedToBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<CMUStomachComponent, EntityPausedEvent>(OnOrganPaused);
        SubscribeLocalEvent<CMUStomachComponent, EntityUnpausedEvent>(OnOrganUnpaused);
        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => SetLayerEnabled(ref _medicalEnabled, v), true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => SetLayerEnabled(ref _organEnabled, v), true);
    }

    private void OnStomachStartup(Entity<CMUStomachComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;
        ent.Comp.LastPhysiologyUpdate = Timing.CurTime;
        ent.Comp.PhysiologyActive = false;
        AdvanceOrgan(ent, GetBody(ent.Owner), Timing.CurTime);
    }

    private void OnStageChanged(Entity<CMUStomachComponent> ent, ref OrganStageChangedEvent args)
    {
        if (_net.IsClient || PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.Body) ||
            GetBody(ent.Owner) != args.Body)
            return;
        AdvanceOrgan(ent, args.Body, Timing.CurTime);
        if (PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.Body) || GetBody(ent.Owner) != args.Body)
            return;
        RefreshNausea(ent, args.Body);
    }

    private void OnStomachRemovedFromBody(Entity<CMUStomachComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (_net.IsClient)
            return;
        AdvanceOrgan(ent, args.OldBody, Timing.CurTime);
        // A donor does not bring the old recipient's partial vomiting interval.
        ent.Comp.ActiveCheckElapsed = TimeSpan.Zero;
        if (!Enabled || PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.OldBody))
            return;
        EnsureComp<MissingStomachComponent>(args.OldBody);
        Status.TrySetStatusEffectDuration(args.OldBody, Nausea, duration: null);
    }

    private void OnStomachAddedToBody(Entity<CMUStomachComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (_net.IsClient || PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.Body))
            return;
        RemComp<MissingStomachComponent>(args.Body);
        AdvanceOrgan(ent, args.Body, Timing.CurTime);
        if (PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.Body) || GetBody(ent.Owner) != args.Body)
            return;
        RefreshNausea(ent, args.Body);
    }

    private void RefreshNausea(Entity<CMUStomachComponent> ent, EntityUid body)
    {
        if (ent.Comp.PhysiologyStage.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, Nausea, duration: null);
        else
            Status.TryRemoveStatusEffect(body, Nausea);
    }

    /// <summary>Discards pending trials before rejuvenation publishes ordinary healing boundaries.</summary>
    public void ResetPhysiology(EntityUid body)
    {
        if (_net.IsClient || PhysiologyUnavailable(body))
            return;
        var now = Timing.CurTime;
        foreach (var (uid, _) in _medicalIndex.GetOrgans(body).ToArray())
        {
            if (!TryComp<CMUStomachComponent>(uid, out var stomach))
                continue;
            stomach.LastPhysiologyUpdate = now;
            stomach.ActiveCheckElapsed = TimeSpan.Zero;
            AdvanceOrgan((uid, stomach), body, now);
        }
    }

    private void SetLayerEnabled(ref bool field, bool value)
    {
        if (field == value)
            return;
        if (_net.IsServer)
            ServicePhysiology(Timing.CurTime, force: true);
        field = value;
        if (_net.IsServer)
            ServicePhysiology(Timing.CurTime, force: true);
    }

    protected void UpdateServer(float frameTime)
    {
        if (!Enabled)
            return;
        _stomachScanAccumulator += frameTime;
        if (_stomachScanAccumulator < StomachScanInterval)
            return;
        _stomachScanAccumulator %= StomachScanInterval;
        ServicePhysiology(Timing.CurTime);
    }

    private void ServicePhysiology(TimeSpan now, bool force = false)
    {
        var query = EntityQueryEnumerator<CMUStomachComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var stomach, out _))
        {
            if (force || stomach.NextVomitCheck <= now)
                AdvanceOrgan((uid, stomach), GetBody(uid), now);
        }
        if (!Enabled)
            return;
        var missingQuery = EntityQueryEnumerator<MissingStomachComponent>();
        while (missingQuery.MoveNext(out var uid, out _))
        {
            if (!PhysiologyUnavailable(uid))
                Status.TrySetStatusEffectDuration(uid, Nausea, duration: null);
        }
    }

    private bool PhysiologyUnavailable(EntityUid uid)
        => TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid);
    private bool CanAdvanceBody(EntityUid body, bool? inStasis)
    {
        if (!Enabled || PhysiologyUnavailable(body) || _metadata.EntityPaused(body) ||
            TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return false;
        // The marker is still queryable during its shutdown callback.
        return inStasis is { } stasis
            ? !stasis && !HasComp<SynthComponent>(body)
            : Stasis.CanBodyMetabolize(body);
    }

    private void AdvanceOrgan(Entity<CMUStomachComponent> ent, EntityUid? body, TimeSpan now, bool? inStasis = null)
    {
        var stomach = ent.Comp;
        var revision = stomach.PhysiologyRevision;
        var attachedBody = GetBody(ent.Owner);
        if (!TryComp<OrganHealthComponent>(ent, out var health))
            return;
        // Permission is a public callback boundary. Do not claim elapsed time until
        // it returns: nested healing/removal can settle it, while rejuvenation discards it.
        var active = body is { } current && attachedBody == current &&
            !PhysiologyUnavailable(ent.Owner) && !_metadata.EntityPaused(ent.Owner) &&
            CanAdvanceBody(current, inStasis);
        if (stomach.PhysiologyRevision != revision || PhysiologyUnavailable(ent.Owner) ||
            body is { } recipient && PhysiologyUnavailable(recipient) ||
            !TryComp<CMUStomachComponent>(ent, out var currentOrgan) || !ReferenceEquals(currentOrgan, stomach) ||
            GetBody(ent.Owner) != attachedBody || !TryComp<OrganHealthComponent>(ent, out var currentHealth) ||
            !ReferenceEquals(currentHealth, health))
            return;

        var elapsed = now - stomach.LastPhysiologyUpdate;
        var wasActive = stomach.PhysiologyActive;
        var oldStage = stomach.PhysiologyStage;
        stomach.LastPhysiologyUpdate = now;
        stomach.PhysiologyRevision++;
        stomach.PhysiologyStage = health.Stage;
        stomach.PhysiologyActive = active;

        if (wasActive && elapsed > TimeSpan.Zero)
            stomach.ActiveCheckElapsed += elapsed;
        var interval = stomach.VomitCheckInterval;
        if (interval <= TimeSpan.Zero)
        {
            stomach.ActiveCheckElapsed = TimeSpan.Zero;
            stomach.NextVomitCheck = now + TimeSpan.FromSeconds(StomachScanInterval);
            return;
        }

        var trialDue = stomach.ActiveCheckElapsed >= interval;
        // Keep the existing one-trial service policy. Late service does not replay missed
        // vomiting opportunities; the next full active interval starts after this trial.
        if (trialDue)
            stomach.ActiveCheckElapsed = TimeSpan.Zero;
        stomach.NextVomitCheck = now + interval - stomach.ActiveCheckElapsed;
        if (!trialDue || body is not { } patient || PhysiologyUnavailable(ent.Owner) ||
            PhysiologyUnavailable(patient) || !stomach.VomitChance.TryGetValue(oldStage, out var chance) || chance <= 0)
            return;
        if (Random.Prob(Math.Clamp(chance, 0, 1)))
            ApplyVomit(patient);
    }

    private void AdvanceBody(EntityUid body, TimeSpan now, bool? inStasis = null)
    {
        if (_net.IsClient || PhysiologyUnavailable(body))
            return;
        foreach (var (uid, _) in _medicalIndex.GetOrgans(body).ToArray())
        {
            if (TryComp<CMUStomachComponent>(uid, out var stomach))
                AdvanceOrgan((uid, stomach), body, now, inStasis);
        }
    }

    private void OnBodyPhysiologyBoundary(ref CMUOrganPhysiologyBoundaryEvent args)
    {
        if (_net.IsClient || PhysiologyUnavailable(args.Body) || !HasComp<CMUHumanMedicalComponent>(args.Body))
            return;
        if (args.Reset)
            ResetPhysiology(args.Body);
        else
            AdvanceBody(args.Body, args.Time, args.InStasis);
    }

    private void OnOrganPaused(Entity<CMUStomachComponent> ent, ref EntityPausedEvent args)
    {
        if (_net.IsServer)
            AdvanceOrgan(ent, GetBody(ent.Owner), Timing.CurTime);
    }

    private void OnOrganUnpaused(Entity<CMUStomachComponent> ent, ref EntityUnpausedEvent args)
    {
        if (_net.IsServer)
            AdvanceOrgan(ent, GetBody(ent.Owner), Timing.CurTime);
    }

    protected virtual void ApplyVomit(EntityUid body)
    {
    }

    private EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
