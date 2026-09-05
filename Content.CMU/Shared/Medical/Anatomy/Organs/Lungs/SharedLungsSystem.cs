using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs.Events;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Events;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;

public abstract partial class SharedLungsSystem : EntitySystem
{
    [Dependency] private InitialBodySystem _initialBody = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected StatusEffectsSystem Status = default!;
    [Dependency] protected CMStasisBagSystem Stasis = default!;

    private static readonly EntProtoId PulmonaryEdema = "StatusEffectCMUPulmonaryEdema";
    private static readonly FixedPoint2 MissingLungsAsphyxPerSecond = FixedPoint2.New(5);

    private const float AsphyxScanInterval = 1f;
    private float _asphyxScanAccumulator;
    private readonly HashSet<EntityUid> _asphyxBodies = new();

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LungsComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<LungsComponent, ComponentStartup>(OnLungsStartup,
            after: new[] { typeof(SharedOrganHealthSystem) });
        SubscribeLocalEvent<LungsComponent, OrganRemovedFromBodyEvent>(OnLungsRemovedFromBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<LungsComponent, OrganAddedToBodyEvent>(OnLungsAddedToBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<CMUMedicalAggregateComponent, CMUMedicalChangedEvent>(OnTopologyChanged);
        SubscribeLocalEvent<CMUHumanMedicalComponent, LungEfficiencyMultiplyEvent>(OnEfficiencyMultiply);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnLungsStartup(Entity<LungsComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextAsphyxTick = Timing.CurTime + TimeSpan.FromSeconds(1);
        ent.Comp.NextBloodCoughCheck = Timing.CurTime + ent.Comp.BloodCoughInterval;
        if (!_net.IsClient && TryComp<OrganHealthComponent>(ent, out var health) &&
            ent.Comp.Efficiency != GetEfficiency(health.Stage))
        {
            ent.Comp.Efficiency = GetEfficiency(health.Stage);
            Dirty(ent);
        }
    }

    private void OnLungsRemovedFromBody(Entity<LungsComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (_net.IsClient)
            return;

        if (!_medicalEnabled || !_organEnabled)
            return;
        if (TerminatingOrDeleted(args.OldBody))
            return;

        ReconcileRespiratoryStatus(args.OldBody);
    }

    private void OnLungsAddedToBody(Entity<LungsComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (_net.IsClient)
            return;

        // Insertion is also a reconstruction boundary for preserved or pre-injured donors.
        if (TryComp<OrganHealthComponent>(ent, out var health) &&
            ent.Comp.Efficiency != GetEfficiency(health.Stage))
        {
            ent.Comp.Efficiency = GetEfficiency(health.Stage);
            Dirty(ent);
        }
        ReconcileRespiratoryStatus(args.Body);
    }

    private void OnStageChanged(Entity<LungsComponent> ent, ref OrganStageChangedEvent args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.Efficiency = GetEfficiency(args.New);
        Dirty(ent);

        ReconcileRespiratoryStatus(args.Body);
    }

    private void OnTopologyChanged(Entity<CMUMedicalAggregateComponent> ent, ref CMUMedicalChangedEvent args)
    {
        if (_net.IsClient || !_medicalEnabled || !_organEnabled ||
            (args.Changes & CMUMedicalChangeFlags.Topology) == 0 ||
            !HasComp<CMUHumanMedicalComponent>(ent))
            return;

        if (!HasComp<MissingLungsComponent>(ent) &&
            !_initialBody.HasInitialOrgan(ent.Owner, "Lungs") &&
            !TryGetRespiratoryCapacity(ent.Owner, out _))
            return;

        // Base-species lungs have no OrganHealth event, and intact subtree transfers
        // retain their organ relations. Reconcile once after the structural batch.
        ReconcileRespiratoryStatus(ent.Owner);
    }

    /// <summary>
    ///     The best attached lung supplies respiratory capacity. Additional injured
    ///     lungs retain their local injury consequences but cannot reduce that capacity.
    ///     An absent result is distinct from an attached lung with zero efficiency.
    /// </summary>
    public bool TryGetRespiratoryCapacity(EntityUid body, out LungRespiratoryCapacity capacity)
    {
        capacity = default;
        var found = false;
        foreach (var organ in MedicalIndex.GetOrgans(body))
        {
            if (organ.Comp.Body != body || TerminatingOrDeleted(organ.Owner))
                continue;

            var cmuLung = TryComp<LungsComponent>(organ.Owner, out var lungs);
            if (!cmuLung && !HasComp<LungComponent>(organ.Owner))
                continue;
            if (!TryComp<ChildOrganComponent>(organ.Owner, out var relation) || relation.Parent is not { } parent ||
                !TryComp<BodyPartComponent>(parent, out var part) || part.Body != body)
                continue;

            // Other organic species and compatible donor organs use base lungs
            // without CMU's injury model. They retain ordinary respiratory capacity.
            var efficiency = cmuLung ? lungs!.Efficiency : 1f;
            var stage = cmuLung && TryComp<OrganHealthComponent>(organ.Owner, out var health)
                ? health.Stage
                : OrganDamageStage.Healthy;
            var rate = cmuLung ? lungs!.AsphyxPerSecond.GetValueOrDefault(stage) : FixedPoint2.Zero;
            if (found && (efficiency < capacity.Efficiency ||
                efficiency == capacity.Efficiency &&
                (rate > capacity.AsphyxiationPerSecond || rate == capacity.AsphyxiationPerSecond && stage.IsAtLeast(capacity.Stage))))
                continue;

            capacity = new LungRespiratoryCapacity(organ.Owner, efficiency, stage, rate);
            found = true;
        }
        return found;
    }

    private void ReconcileRespiratoryStatus(EntityUid body)
    {
        if (_net.IsClient || TerminatingOrDeleted(body))
            return;

        var present = TryGetRespiratoryCapacity(body, out var capacity);
        if (present)
            RemComp<MissingLungsComponent>(body);
        else if (!HasComp<MissingLungsComponent>(body))
            EnsureComp<MissingLungsComponent>(body).NextAsphyxTick = Timing.CurTime;

        var impaired = !present || capacity.Stage.IsAtLeast(OrganDamageStage.Damaged);
        if (Status.TryGetStatusEffect(body, PulmonaryEdema, out var existing))
        {
            // Retire this source before a same-tick donor replacement or injury can
            // renew it. Generic queued removal cannot cancel its pending deletion.
            if (!impaired || EntityManager.IsQueuedForDeletion(existing.Value))
                Del(existing.Value);
            else
                return;
        }

        if (impaired)
            Status.TrySetStatusEffectDuration(body, PulmonaryEdema, duration: null);
    }

    private static float GetEfficiency(OrganDamageStage stage) => stage switch
    {
        OrganDamageStage.Bruised => 0.85f,
        OrganDamageStage.Damaged => 0.6f,
        OrganDamageStage.Failing => 0.3f,
        OrganDamageStage.Dead => 0.0f,
        _ => 1.0f,
    };

    private void OnEfficiencyMultiply(Entity<CMUHumanMedicalComponent> ent, ref LungEfficiencyMultiplyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        args.Multiplier *= TryGetRespiratoryCapacity(ent.Owner, out var capacity) ? capacity.Efficiency : 0f;
    }

    protected void UpdateServer(float frameTime)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        _asphyxScanAccumulator += frameTime;
        if (_asphyxScanAccumulator < AsphyxScanInterval)
            return;
        _asphyxScanAccumulator = 0f;
        _asphyxBodies.Clear();

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<LungsComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var lungs, out var oh))
        {
            if (lungs.NextAsphyxTick > now)
                continue;
            lungs.NextAsphyxTick = now + TimeSpan.FromSeconds(1);

            var body = GetBody(uid);
            if (body is null || IsPaused(body.Value) || !Stasis.CanBodyMetabolize(body.Value))
                continue;

            if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            if (lungs.AsphyxPerSecond.TryGetValue(oh.Stage, out var rate) && rate > FixedPoint2.Zero &&
                _asphyxBodies.Add(body.Value) && TryGetRespiratoryCapacity(body.Value, out var capacity) &&
                capacity.AsphyxiationPerSecond > FixedPoint2.Zero)
                ApplyAsphyx(body.Value, capacity.Organ, capacity.AsphyxiationPerSecond);

            TickBloodCough((uid, lungs, oh), body.Value, now);
        }

        var missingQuery = EntityQueryEnumerator<MissingLungsComponent>();
        while (missingQuery.MoveNext(out var uid, out var missing))
        {
            if (TryGetRespiratoryCapacity(uid, out _))
            {
                ReconcileRespiratoryStatus(uid);
                continue;
            }

            TickMissingLungs((uid, missing), now);
        }
        _asphyxBodies.Clear();
    }

    private void TickMissingLungs(Entity<MissingLungsComponent> ent, TimeSpan now)
    {
        if (ent.Comp.NextAsphyxTick > now)
            return;
        ent.Comp.NextAsphyxTick = now + TimeSpan.FromSeconds(1);

        if (!Stasis.CanBodyMetabolize(ent.Owner))
            return;

        if (TryComp<MobStateComponent>(ent.Owner, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        ReconcileRespiratoryStatus(ent.Owner);

        if (_asphyxBodies.Add(ent.Owner))
            ApplyAsphyx(ent.Owner, ent.Owner, MissingLungsAsphyxPerSecond);
    }

    private void TickBloodCough(
        Entity<LungsComponent, OrganHealthComponent> ent,
        EntityUid body,
        TimeSpan now)
    {
        if (ent.Comp1.NextBloodCoughCheck > now)
            return;
        ent.Comp1.NextBloodCoughCheck = now + ent.Comp1.BloodCoughInterval;

        if (!ent.Comp1.BloodCoughChance.TryGetValue(ent.Comp2.Stage, out var chance) ||
            chance <= 0f ||
            !Random.Prob(chance))
        {
            return;
        }

        ApplyBloodCough(body, ent.Owner, ent.Comp1.BloodLossPerCough);
    }

    protected virtual void ApplyAsphyx(EntityUid body, EntityUid lung, FixedPoint2 amount)
    {
    }

    protected virtual void ApplyBloodCough(EntityUid body, EntityUid lung, FixedPoint2 bloodLoss)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
