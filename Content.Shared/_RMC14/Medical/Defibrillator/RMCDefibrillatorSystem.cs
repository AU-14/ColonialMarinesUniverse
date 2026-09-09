using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Damage;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Medical.Defibrillator;

public sealed partial class RMCDefibrillatorSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCBloodstreamSystem _rmcBloodstream = default!;
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private RMCReagentSystem _rmcReagent = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedHeartSystem _heart = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;

    private readonly HashSet<EntityUid> _reviving = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<DefibrillatorComponent, RMCDefibrillatorDamageModifyEvent>(OnDefibrillatorDamageModify);
        SubscribeLocalEvent<RMCDefibrillatorAudioComponent, EntityTerminatingEvent>(OnDefibrillatorAudioTerminating);
        SubscribeLocalEvent<RMCDefibrillatorBlockedComponent, ExaminedEvent>(OnNoDefibExamine);
    }

    private void OnDefibrillatorDamageModify(Entity<DefibrillatorComponent> ent, ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (args.Cancelled)
            return;
        var attempt = PrepareRevival(args.Target);
        args.Attempt = attempt;
        if (attempt.Cancelled)
        {
            args.Cancelled = true;
            args.Heal = new DamageSpecifier();

            if (!string.IsNullOrEmpty(attempt.CancelReason))
                _popup.PopupEntity(Loc.GetString(attempt.CancelReason), args.Target, PopupType.MediumCaution);
            return;
        }

        if (ent.Comp.RMCZapDamage != null)
        {
            foreach (var (group, amount) in ent.Comp.RMCZapDamage)
            {
                args.Heal = _rmcDamageable.DistributeDamageCached(args.Target, group, amount, args.Heal);
            }
        }

    }

    public RMCDefibrillatorAttemptEvent PrepareRevival(EntityUid target, bool allowBeatingHeart = false)
    {
        var attempt = new RMCDefibrillatorAttemptEvent(target, allowBeatingHeart);
        RaiseLocalEvent(target, attempt);
        return attempt;
    }

    /// <summary>
    /// Consumes one accepted corpse-revival attempt. Physical and chemical defibrillation
    /// share these effect and commit boundaries; eligibility listeners never restart tissue.
    /// Failed accepted effects retain their existing trauma/healing cost, but cannot report
    /// success or restart replacement tissue after a callback changes the patient.
    /// </summary>
    public bool TryRevive(EntityUid target, RMCDefibrillatorAttemptEvent attempt, DamageSpecifier heal,
        EntityUid? origin = null, bool interruptsDoAfters = true, Entity<DefibrillatorComponent>? device = null)
    {
        if (_net.IsClient || TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) ||
            attempt.Target != target || attempt.Cancelled || attempt.Consumed ||
            !TryComp<MobStateComponent>(target, out var mob) || mob.CurrentState != MobState.Dead ||
            !TryComp<MobThresholdsComponent>(target, out var thresholds) ||
            !TryComp<DamageableComponent>(target, out var damage) || !_reviving.Add(target))
            return false;

        attempt.Consumed = true;
        var heart = attempt.Heart;
        bool IsCurrentPatient()
        {
            return !TerminatingOrDeleted(target) && !EntityManager.IsQueuedForDeletion(target) &&
                   !HasComp<UnrevivableComponent>(target) &&
                   TryComp<MobStateComponent>(target, out var currentMob) && ReferenceEquals(currentMob, mob) &&
                   TryComp<MobThresholdsComponent>(target, out var currentThresholds) && ReferenceEquals(currentThresholds, thresholds) &&
                   TryComp<DamageableComponent>(target, out var currentDamage) && ReferenceEquals(currentDamage, damage) &&
                   (heart == null || heart.Body == target && _heart.IsDefibrillationHeartValid(heart)) &&
                   (device is not { } source ||
                    !TerminatingOrDeleted(source.Owner) && !EntityManager.IsQueuedForDeletion(source.Owner) &&
                    TryComp<DefibrillatorComponent>(source.Owner, out var currentDevice) && ReferenceEquals(currentDevice, source.Comp));
        }

        try
        {
            if (!IsCurrentPatient() || heart != null && !_heart.TryApplyDefibrillationTrauma(heart))
                return false;
            if (!IsCurrentPatient() || mob.CurrentState != MobState.Dead)
                return false;
            var effectiveHeal = new DamageSpecifier(heal);
            TryApplyElectrogenetic(target, ref effectiveHeal);
            if (!IsCurrentPatient() || mob.CurrentState != MobState.Dead)
                return false;
            _damageable.TryChangeDamage(target, effectiveHeal, true, origin: origin,
                interruptsDoAfters: interruptsDoAfters);
            if (!IsCurrentPatient() || mob.CurrentState != MobState.Dead ||
                !_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold, thresholds) ||
                _damageable.GetTotalDamage(target) >= threshold)
                return false;

            _mobState.ChangeMobState(target, MobState.Critical, mob, origin);
            // Mob-state listeners may detach/delete/replace the captured heart. Revoke
            // only this attempted revival; never roll tissue or reagent mutations back.
            if (!IsCurrentPatient() || mob.CurrentState == MobState.Dead ||
                heart != null && !_heart.TryCompleteDefibrillation(heart))
            {
                RestoreFailedRevival();
                return false;
            }
            _mobThreshold.VerifyThresholds(target, thresholds, mob, damage);
            if (!IsCurrentPatient() || mob.CurrentState == MobState.Dead ||
                heart != null && heart.HeartComponent.Stopped)
            {
                RestoreFailedRevival();
                return false;
            }
            return true;
        }
        finally
        {
            _reviving.Remove(target);
        }

        void RestoreFailedRevival()
        {
            if (!TerminatingOrDeleted(target) && !EntityManager.IsQueuedForDeletion(target) &&
                TryComp<MobStateComponent>(target, out var current) && ReferenceEquals(current, mob) &&
                mob.CurrentState != MobState.Dead)
                _mobState.ChangeMobState(target, MobState.Dead, mob, origin);
        }
    }

    /// <summary>
    /// Triggers the strongest electrogenetic reagent in a bloodstream and consumes one unit.
    /// Shared by physical defibrillators and the generated Defibrillating property.
    /// </summary>
    public bool TryApplyElectrogenetic(EntityUid target, ref DamageSpecifier heal)
    {
        if (!_rmcBloodstream.TryGetChemicalSolution(target, out var solutionEnt, out var chemicals))
            return false;

        (Reagent Reagent, FixedPoint2 Heal, Electrogenetic Electrogenetic)? highest = null;
        foreach (var quantity in chemicals.Contents)
        {
            if (!_rmcReagent.TryIndex(quantity.Reagent.Prototype, out var reagent))
                continue;

            if (reagent.Metabolisms == null)
                continue;

            foreach (var effects in reagent.Metabolisms.Metabolisms.Values)
            {
                foreach (var effect in effects.Effects)
                {
                    if (effect is not Electrogenetic electrogenetic)
                        continue;

                    if (highest == null || electrogenetic.HealAmount > highest.Value.Heal)
                        highest = (reagent, electrogenetic.HealAmount, electrogenetic);
                }
            }
        }

        if (highest == null)
            return false;

        heal += highest.Value.Electrogenetic.CalculateHeal(_rmcDamageable, target);
        _solutionContainer.RemoveReagent(solutionEnt, highest.Value.Reagent.ID, 1);
        return true;
    }

    private void OnNoDefibExamine(Entity<RMCDefibrillatorBlockedComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.ShowOnExamine)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Examine, ("victim", ent)));
    }

    private void OnDefibrillatorAudioTerminating(Entity<RMCDefibrillatorAudioComponent> ent, ref EntityTerminatingEvent args)
    {
        if (TryComp(ent.Comp.Defibrillator, out DefibrillatorComponent? defibrillator) &&
            defibrillator.ChargeSoundEntity == ent.Owner)
            defibrillator.ChargeSoundEntity = null;
    }

    public void StartChargingAudio(Entity<DefibrillatorComponent> defib, EntityUid user)
    {
        StopChargingAudio(defib);

        if (_net.IsClient)
            return;

        defib.Comp.ChargeSoundEntity = _audio.PlayPvs(defib.Comp.ChargeSound, defib.Owner)?.Entity;
        if (defib.Comp.ChargeSoundEntity is not { } sound)
            return;

        var audio = EnsureComp<RMCDefibrillatorAudioComponent>(sound);
        audio.Defibrillator = defib.Owner;
        Dirty(sound, audio);
    }

    public void StopChargingAudio(Entity<DefibrillatorComponent> defib)
    {
        var sound = defib.Comp.ChargeSoundEntity;
        defib.Comp.ChargeSoundEntity = null;
        _audio.Stop(sound);
        QueueDel(sound);
    }
}
