using System.Linq;
using Content.Server._RMC14.Medical;
using Content.Server.Medical;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Content.Shared.PowerCell;
using Content.Shared.Stacks;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedDefibrillatorSystem _defibrillator = default!;
    [Dependency] private HypospraySystem _hypospray = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private PowerCellSystem _medicalPower = default!;
    [Dependency] private SharedRMCBloodstreamSystem _medicalBlood = default!;
    [Dependency] private SharedSolutionContainerSystem _medicalSolutions = default!;

    private void InitializeMedics()
    {
        SubscribeLocalEvent<CMUExpeditionPatientComponent, HealingDoAfterEvent>(OnMedicDressing,
            after: new[] { typeof(HealingSystem) });
        SubscribeLocalEvent<CMUExpeditionPatientComponent, DamageChangedEvent>(OnMedicalPatientHurt);
        SubscribeLocalEvent<CMUExpeditionPatientComponent, TargetDefibrillatedEvent>(OnMedicalPatientDefibrillated);
        SubscribeLocalEvent<CMUExpeditionMedicalToolComponent, DefibrillatorZapDoAfterEvent>(OnMedicShock,
            after: new[] { typeof(DefibrillatorSystem) });
        SubscribeLocalEvent<CMUExpeditionMedicalToolComponent, HyposprayDoAfterEvent>(OnMedicInjection,
            after: new[] { typeof(RMCHypospraySystem) });
        SubscribeLocalEvent<CMUExpeditionMedicComponent, SelfBeforeDefibrillatorZapsEvent>(OnMedicBeforeShock);
        SubscribeLocalEvent<CMUExpeditionMedicComponent, SelfBeforeInjectEvent>(OnMedicBeforeInjection);
    }

    private void OnMedicalPatientHurt(Entity<CMUExpeditionPatientComponent> ent, ref DamageChangedEvent args)
    {
        // Oxygen loss while critical must not prevent stabilization; new physical wounds do.
        if (args.InterruptsDoAfters && args.DamageIncreased && args.DamageDelta is { } delta &&
            delta.DamageDict.Any(pair => pair.Value > 0 && pair.Key.Id is "Blunt" or "Slash" or "Piercing" or "Heat"))
            ent.Comp.LastWound = _timing.CurTime;
    }

    private IEnumerable<EntityUid> MedicalItems(EntityUid uid)
    {
        foreach (var hand in _hands.EnumerateHands(uid))
            if (_hands.TryGetHeldItem(uid, hand, out var item))
                yield return item.Value;
        foreach (var item in SupplyItems(uid))
            yield return item;
    }

    private EntityUid? MedicalDefib(EntityUid uid) => MedicalItems(uid)
        .Where(item => HasComp<DefibrillatorComponent>(item) && _medicalPower.HasActivatableCharge(item))
        .Cast<EntityUid?>().FirstOrDefault();

    private bool HasMedicalSupplies(EntityUid uid, EntityUid patient) =>
        _mobs.IsDead(patient) ? MedicalDefib(uid) != null :
        MedicalItems(uid).Any(item => UsefulDressing(item, patient) || UsefulInjection(item, patient));

    private bool UsefulDressing(EntityUid item, EntityUid patient)
    {
        if (!TryComp<HealingComponent>(item, out var healing) ||
            TryComp<StackComponent>(item, out var stack) && stack.Count <= 0)
            return false;
        var damage = _damage.GetAllDamage(patient).DamageDict;
        return healing.BloodlossModifier < 0 && Bleeding(patient) ||
            healing.Damage.DamageDict.Any(pair => pair.Value < 0 && damage.TryGetValue(pair.Key, out var amount) && amount > 0);
    }

    private bool UsefulInjection(EntityUid item, EntityUid patient)
    {
        if (!TryComp<HyposprayComponent>(item, out var hypo) || !HasComp<CMUExpeditionMedicalToolComponent>(item) ||
            !_medicalSolutions.TryGetSolution(item, hypo.SolutionName, out _, out var dose) || dose.Volume < hypo.TransferAmount ||
            !_medicalBlood.TryGetChemicalSolution(patient, out _, out var chemicals))
            return false;
        // Do not stack the cocktail on an existing dose, including medication given by another medic/player.
        return dose.Contents.Count > 0 && dose.Contents.All(reagent => chemicals.GetReagentQuantity(reagent.Reagent) < 1);
    }

    private bool WorkOnPatient(EntityUid uid, CMUExpeditionAgentComponent agent, CMUExpeditionMedicComponent medic,
        EntityUid patient, TimeSpan now, bool stabilizeBleeding = false)
    {
        if (!MedicalWorkSafe(uid, agent, medic, patient))
        {
            CancelMedical(uid, agent, "unsafe-treatment", true);
            return false;
        }
        if (medic.PatientDoses >= 6 || medic.PatientShocks >= 2)
        {
            CancelMedical(uid, agent, "reassess-patient", true);
            return false;
        }
        if (_mobs.IsAlive(patient) && _damage.GetTotalDamage(patient).Float() < medic.TreatDamage && !Bleeding(patient))
        {
            CancelMedical(uid, agent, "patient-stable");
            return false;
        }
        var items = MedicalItems(uid).ToList();
        var dressing = items.Where(item => UsefulDressing(item, patient) &&
            (!stabilizeBleeding || Comp<HealingComponent>(item).BloodlossModifier < 0)).Cast<EntityUid?>().FirstOrDefault();
        var injector = items.Where(item => UsefulInjection(item, patient)).Cast<EntityUid?>().FirstOrDefault();
        var defib = MedicalDefib(uid);
        var dead = _mobs.IsDead(patient);
        var needsPreparation = dead && defib is { } device && !_defibAdvice.Analyze(device, patient).Sufficient;
        EntityUid? chosen = stabilizeBleeding ? dressing : !_mobs.IsAlive(patient) && injector != null ? injector :
            dead && !needsPreparation ? defib : dressing ?? (dead ? defib : injector);
        if (chosen is not { } item)
        {
            CancelMedical(uid, agent, "no-useful-supplies", true);
            return false;
        }
        if (medic.Item != item)
            StowMedicalItem(uid, medic);
        if (!_hands.IsHolding(uid, item, out _) &&
            (!FreeMedicalHand(uid, agent) || !_hands.TryPickupAnyHand(uid, item)))
        {
            medic.Decision = "freeing-medical-hand";
            return true;
        }
        medic.Item = item;
        ActivateWeapon(uid, item); // Select the held tool, not the rifle, for native hand validation.
        agent.State = CMUExpeditionAgentState.Healing;
        agent.RifleLoweredUntil = now + TimeSpan.FromSeconds(0.5);
        _steering.Unregister(uid);
        if (TryComp<HealingComponent>(item, out var healing))
        {
            medic.Phase = CMUExpeditionMedicalPhase.Treat;
            medic.Decision = "dressing-patient";
            var args = new DoAfterArgs(EntityManager, uid, healing.Delay, new HealingDoAfterEvent(), patient,
                target: patient, used: item)
            {
                NeedHand = true, BreakOnMove = true, BreakOnDamage = true, DamageThreshold = 0.1f,
                ExtraCheck = () => medic.Patient == patient && MedicalWorkSafe(uid, agent, medic, patient) &&
                    _hands.IsHolding(uid, item, out _),
            };
            if (_doAfter.TryStartDoAfter(args, out medic.DoAfter))
                return true;
        }
        else if (TryComp<HyposprayComponent>(item, out var hypo))
        {
            medic.Phase = CMUExpeditionMedicalPhase.Inject;
            medic.Decision = "stabilizing-patient";
            _medicalSolutions.TryGetSolution(item, hypo.SolutionName, out _, out var dose);
            medic.InjectionVolume = dose?.Volume.Float() ?? 0;
            if (_hypospray.TryDoInject((item, hypo), patient, uid))
            {
                if (CaptureMedicalDoAfter(uid, medic, item, patient))
                    return true;
            }
        }
        else if (dead && TryComp<DefibrillatorComponent>(item, out var defibrillator))
        {
            medic.Phase = CMUExpeditionMedicalPhase.Revive;
            medic.ShockCompleted = false;
            medic.Decision = "reviving-patient";
            ReleaseCasualty(uid, agent); // A rescuer must release the body before a live shock.
            _defibAdvice.SetAdvisedJoules(item, _defibAdvice.Analyze(item, patient).Joules);
            if ((_itemToggle.IsActivated(item) || _itemToggle.TryActivate(item, uid)) &&
                _defibrillator.TryStartZap((item, defibrillator), patient, uid))
            {
                if (CaptureMedicalDoAfter(uid, medic, item, patient))
                    return true;
            }
        }
        CancelMedical(uid, agent, "medical-action-failed", true);
        return false;
    }

    private bool CaptureMedicalDoAfter(EntityUid uid, CMUExpeditionMedicComponent medic, EntityUid item, EntityUid patient)
    {
        // Native device APIs own their delays and do not return the ID. Capture only this device's
        // running action so interruption cannot cancel unrelated player/native work.
        if (TryComp<DoAfterComponent>(uid, out var actions))
            foreach (var action in actions.DoAfters.Values)
                if (!action.Cancelled && !action.Completed && action.Args.Used == item && action.Args.Target == patient &&
                    action.Args.Event is DefibrillatorZapDoAfterEvent or HyposprayDoAfterEvent)
                    medic.DoAfter = action.Id;
        // A zero-delay native injection may already have completed synchronously.
        return medic.DoAfter != null || medic.Patient == patient && medic.Item == null;
    }

    private void OnMedicDressing(Entity<CMUExpeditionPatientComponent> ent, ref HealingDoAfterEvent args)
    {
        if (ent.Comp.Medic != args.User || !TryComp<CMUExpeditionMedicComponent>(args.User, out var medic) ||
            medic.Patient != ent.Owner || medic.DoAfter != args.DoAfter.Id)
            return;
        args.Repeat = false;
        FinishMedicalDose(args.User, medic, args.Cancelled || !args.Handled);
    }

    private void OnMedicShock(Entity<CMUExpeditionMedicalToolComponent> ent, ref DefibrillatorZapDoAfterEvent args)
    {
        if (!TryComp<CMUExpeditionMedicComponent>(args.User, out var medic) || medic.Item != ent.Owner || medic.Patient != args.Target)
            return;
        FinishMedicalDose(args.User, medic, args.Cancelled || !args.Handled || !medic.ShockCompleted);
    }

    private void OnMedicalPatientDefibrillated(Entity<CMUExpeditionPatientComponent> ent, ref TargetDefibrillatedEvent args)
    {
        if (ent.Comp.Medic != args.User || !TryComp<CMUExpeditionMedicComponent>(args.User, out var medic) ||
            medic.Patient != ent.Owner || medic.Item != args.Defibrillator.Owner)
            return;
        medic.ShockCompleted = true;
        medic.Shocks++;
        medic.PatientShocks++;
        if (!_mobs.IsDead(ent.Owner))
        {
            medic.Revivals++;
            if (TryComp<CMUExpeditionAgentComponent>(ent.Owner, out var recovered))
            {
                recovered.RecoveryUntil = _timing.CurTime + TimeSpan.FromSeconds(20);
                recovered.Duty = CMUSquadDuty.Recover;
                recovered.DutyUntil = TimeSpan.Zero;
                Decision(recovered, "post-revival", "recover-before-advancing");
            }
        }
    }

    private void OnMedicInjection(Entity<CMUExpeditionMedicalToolComponent> ent, ref HyposprayDoAfterEvent args)
    {
        if (!TryComp<CMUExpeditionMedicComponent>(args.User, out var medic) || medic.Item != ent.Owner || medic.Patient != args.Target)
            return;
        var consumed = !TryComp<HyposprayComponent>(ent, out var hypo) ||
            !_medicalSolutions.TryGetSolution(ent.Owner, hypo.SolutionName, out _, out var solution) ||
            solution.Volume.Float() < medic.InjectionVolume;
        FinishMedicalDose(args.User, medic, args.Cancelled || !args.Handled || !consumed);
    }

    private void FinishMedicalDose(EntityUid uid, CMUExpeditionMedicComponent medic, bool failed)
    {
        medic.DoAfter = null;
        if (failed)
        {
            if (TryComp<CMUExpeditionAgentComponent>(uid, out var agent))
                CancelMedical(uid, agent, "medical-action-interrupted", true);
            return;
        }
        medic.Doses++;
        medic.PatientDoses++;
        medic.NextAction = _timing.CurTime + TimeSpan.FromSeconds(0.6);
        StowMedicalItem(uid, medic);
        medic.Phase = CMUExpeditionMedicalPhase.Treat;
    }

    private void OnMedicBeforeShock(Entity<CMUExpeditionMedicComponent> ent, ref SelfBeforeDefibrillatorZapsEvent args)
    {
        if (HasComp<ActorComponent>(ent))
            return;
        if (ent.Comp.Patient != args.DefibTarget || ent.Comp.Item != args.Defib || !_mobs.IsDead(args.DefibTarget) ||
            !_hands.IsHolding(ent.Owner, args.Defib, out _) || !TryComp<CMUExpeditionAgentComponent>(ent, out var agent) ||
            !MedicalWorkSafe(ent, agent, ent.Comp, args.DefibTarget))
            args.Cancel();
    }

    private void OnMedicBeforeInjection(Entity<CMUExpeditionMedicComponent> ent, ref SelfBeforeInjectEvent args)
    {
        if (HasComp<ActorComponent>(ent))
            return;
        if (ent.Comp.Patient != args.TargetGettingInjected || ent.Comp.Item != args.UsedInjector ||
            !_hands.IsHolding(ent.Owner, args.UsedInjector, out _) || !UsefulInjection(args.UsedInjector, args.TargetGettingInjected) ||
            !TryComp<CMUExpeditionAgentComponent>(ent, out var agent) || !MedicalWorkSafe(ent, agent, ent.Comp, args.TargetGettingInjected))
            args.Cancel();
    }
}
