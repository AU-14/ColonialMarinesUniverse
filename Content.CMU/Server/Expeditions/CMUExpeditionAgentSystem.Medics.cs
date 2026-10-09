using System.Linq;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Body.Components;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Defibrillator;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Medical;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private CMUDefibChargeSystem _defibAdvice = default!;
    [Dependency] private RMCDefibrillatorSystem _rmcDefib = default!;

    private bool RunMedic(EntityUid uid, CMUExpeditionAgentComponent agent, float damage, bool hit, TimeSpan now)
    {
        if (!TryComp<CMUExpeditionMedicComponent>(uid, out var medic))
            return false;
        if (hit || damage >= agent.RetreatDamage || agent.RushTarget != null || GrenadeDanger(Transform(uid).Coordinates))
        {
            CancelMedical(uid, agent, "under-attack", true);
            return false;
        }
        if (medic.Patient == null)
        {
            if (now < medic.NextTriage || agent.Action != null || agent.Treatment != null || agent.PendingWeapon != null ||
                agent.State is CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.Withdraw or CMUExpeditionAgentState.PlanMove)
                return false;
            medic.NextTriage = now + TimeSpan.FromSeconds(1);
            foreach (var expired in medic.RetryAfter.Where(pair => pair.Value <= now || !Exists(pair.Key)).Select(pair => pair.Key).ToArray())
                medic.RetryAfter.Remove(expired);
            if (FindMedicalPatient(uid, agent, medic, now) is not { } selected)
                return false;
            CancelWork(uid, agent);
            CancelPlan(uid, agent, false);
            StopSpacing(uid, agent);
            medic.Patient = selected;
            medic.PatientDoses = 0;
            medic.PatientShocks = 0;
            medic.Shelter = null;
            medic.Deadline = now + TimeSpan.FromSeconds(45);
            medic.LastPatientSeen = now;
            medic.CoverLostAt = TimeSpan.Zero;
            medic.NextCoverCheck = TimeSpan.Zero;
            medic.NextAction = now;
            EnsureComp<CMUExpeditionPatientComponent>(selected).Medic = uid;
            agent.Casualty = selected;
            agent.Action = CMUTacticalAction.MedicalAid;
            agent.Goal = CMUTacticalGoal.Aid;
            medic.Phase = CMUExpeditionMedicalPhase.Approach;
            medic.Decision = "approaching-patient";
        }

        var patient = medic.Patient.Value;
        if (!ValidMedicalPatient(uid, patient) || now >= medic.Deadline ||
            !TryComp<CMUExpeditionPatientComponent>(patient, out var claim) || claim.Medic != uid)
        {
            CancelMedical(uid, agent, "patient-unavailable", true);
            return false;
        }
        if (Visible(uid, patient, medic.SearchRange + 2))
            medic.LastPatientSeen = now;
        else if (now - medic.LastPatientSeen > TimeSpan.FromSeconds(2))
        {
            CancelMedical(uid, agent, "patient-out-of-sight", true);
            return false;
        }
        if (now >= medic.NextCoverCheck)
        {
            medic.NextCoverCheck = now + TimeSpan.FromSeconds(0.5);
            medic.Covered = MedicalCoverAvailable(uid, agent, patient, now, true);
        }
        var exposed = !ShelteredFromKnownThreats(uid, agent, Transform(uid).Coordinates) ||
            !ShelteredFromKnownThreats(uid, agent, Transform(patient).Coordinates);
        if (exposed && !medic.Covered)
        {
            if (medic.CoverLostAt == TimeSpan.Zero)
                medic.CoverLostAt = now;
            if (now - medic.CoverLostAt >= TimeSpan.FromSeconds(1))
            {
                CancelMedical(uid, agent, "cover-lost", true);
                return false;
            }
        }
        else
            medic.CoverLostAt = TimeSpan.Zero;

        if (medic.DoAfter != null)
        {
            // Finished can precede the native completion event by one system update. Only
            // abandoned/cancelled work is stale; let native completion own its effects.
            if (_doAfter.GetStatus(medic.DoAfter) is DoAfterStatus.Invalid or DoAfterStatus.Cancelled ||
                !MedicalWorkSafe(uid, agent, medic, patient))
            {
                CancelMedical(uid, agent, "treatment-interrupted", true);
                return false;
            }
            return true;
        }
        if (now < medic.NextAction)
            return true;

        if (medic.Phase == CMUExpeditionMedicalPhase.Extract)
        {
            if (_mobs.IsAlive(patient))
            {
                ReleaseCasualty(uid, agent);
                ClearCover(agent);
                medic.Phase = CMUExpeditionMedicalPhase.Approach;
            }
            else
            {
                if (!TryComp<PullableComponent>(patient, out var pulled) || pulled.Puller != uid)
                {
                    CancelMedical(uid, agent, "pull-interrupted", true);
                    return false;
                }
                if (ContinueMove(uid, agent, Transform(uid), now))
                    return true;
                if (agent.LastMoveFailed || !ShelteredFromKnownThreats(uid, agent, Transform(patient).Coordinates))
                {
                    CancelMedical(uid, agent, "extraction-blocked", true);
                    return false;
                }
                ReleaseCasualty(uid, agent);
                medic.Extractions++;
                agent.Rescues++;
                medic.Phase = CMUExpeditionMedicalPhase.Approach;
            }
        }
        if (!_interaction.InRangeUnobstructed(uid, patient, 1.25f))
        {
            StowMedicalItem(uid, medic);
            if (agent.CoverDestination is { } destination && _transform.InRange(destination, Transform(patient).Coordinates, 0.75f))
            {
                if (ContinueMove(uid, agent, Transform(uid), now))
                    return true;
                CancelMedical(uid, agent, "approach-blocked", true);
                return false;
            }
            if (!StartPlanMove(uid, agent, Transform(patient).Coordinates, now))
            {
                CancelMedical(uid, agent, "approach-blocked", true);
                return false;
            }
            agent.MoveUntil = now + TimeSpan.FromSeconds(12);
            medic.Phase = CMUExpeditionMedicalPhase.Approach;
            return true;
        }
        _steering.Unregister(uid);
        ClearCover(agent);

        // Conscious combatants keep their position and weapon. Only incapacitated patients are dragged.
        if (!_mobs.IsAlive(patient) && !ShelteredFromKnownThreats(uid, agent, Transform(patient).Coordinates))
        {
            if (!medic.Covered)
                return true;
            medic.Shelter ??= FindPosition(uid, agent, Transform(uid), true)?.Anchor;
            if (medic.Shelter is not { } shelter)
            {
                CancelMedical(uid, agent, "no-extraction-shelter", true);
                return false;
            }
            if (!FreeMedicalHand(uid, agent))
                return true;
            if (!_pulling.TryStartPull(uid, patient) || !StartPlanMove(uid, agent, shelter, now))
            {
                CancelMedical(uid, agent, "extraction-blocked", true);
                return false;
            }
            agent.MoveUntil = now + TimeSpan.FromSeconds(15);
            medic.Phase = CMUExpeditionMedicalPhase.Extract;
            medic.Decision = "extracting-under-cover";
            return true;
        }
        return WorkOnPatient(uid, agent, medic, patient, now);
    }

    private EntityUid? FindMedicalPatient(EntityUid uid, CMUExpeditionAgentComponent agent, CMUExpeditionMedicComponent medic, TimeSpan now)
    {
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, medic.SearchRange, nearby);
        EntityUid? chosen = null;
        var best = float.MinValue;
        foreach (var patient in nearby)
        {
            if (patient == uid || medic.RetryAfter.ContainsKey(patient) || !ValidMedicalPatient(uid, patient) ||
                !Visible(uid, patient, medic.SearchRange) ||
                agent.Home is not { } home || !_transform.InRange(home, Transform(patient).Coordinates, agent.LeashRange) ||
                TryComp<CMUExpeditionPatientComponent>(patient, out var claim) && claim.Medic != uid &&
                TryComp<CMUExpeditionMedicComponent>(claim.Medic, out var owner) && owner.Patient == patient ||
                TryComp<PullableComponent>(patient, out var pulled) && pulled.Puller != null)
                continue;
            var damage = _damage.GetTotalDamage(patient).Float();
            if (_mobs.IsAlive(patient) && damage < medic.TreatDamage && !Bleeding(patient))
                continue;
            if (!HasMedicalSupplies(uid, patient) ||
                !ShelteredFromKnownThreats(uid, agent, Transform(patient).Coordinates) && !MedicalCoverAvailable(uid, agent, patient, now))
                continue;
            // Critical lives take priority, then revivable dead, then wounded combatants.
            Transform(uid).Coordinates.TryDistance(EntityManager, Transform(patient).Coordinates, out var distance);
            var score = (_mobs.IsCritical(patient) ? 300 : _mobs.IsDead(patient) ? 220 : 80) + Math.Min(60, damage) - distance * 3;
            if (TryComp<CMUExpeditionAgentComponent>(patient, out var buddy) && SameSquad(uid, agent, patient, buddy))
                score += 20;
            if (score > best)
            {
                best = score;
                chosen = patient;
            }
        }
        return chosen;
    }

    private bool ValidMedicalPatient(EntityUid uid, EntityUid patient) => Exists(patient) &&
        HasComp<CMUHumanMedicalComponent>(patient) && HasComp<DamageableComponent>(patient) && HasComp<InjurableComponent>(patient) &&
        HasComp<MobStateComponent>(patient) && IsFriendly(uid, patient) && Transform(uid).MapID == Transform(patient).MapID &&
        (!_mobs.IsDead(patient) || _defibAdvice.IsShockable(patient) && !_rmcDefib.PrepareRevival(patient).Cancelled);

    private bool Bleeding(EntityUid patient) => TryComp<BloodstreamComponent>(patient, out var blood) && blood.BleedAmount > 0;

    private bool MedicalCoverAvailable(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid patient, TimeSpan now, bool commit = false)
    {
        var threats = agent.VisibleThreats.Where(point => !Sheltered(uid, Transform(patient).Coordinates, point) ||
            !Sheltered(uid, Transform(uid).Coordinates, point)).ToList();
        if (agent.LastSeen is { } remembered && now < agent.ForgetAt &&
            (!Sheltered(uid, Transform(patient).Coordinates, remembered) || !Sheltered(uid, Transform(uid).Coordinates, remembered)))
            threats.Add(remembered);
        if (threats.Count == 0)
            return true;
        var covering = new List<CMUExpeditionAgentComponent>();
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || other == patient || !SameSquad(uid, agent, other, buddy) || !_mobs.IsAlive(other) ||
                HasComp<ActorComponent>(other) || buddy.Action != null || buddy.RushTarget != null ||
                buddy.LastDamage >= buddy.RetreatDamage || buddy.Stress >= 0.75f || now - buddy.LastHit < TimeSpan.FromSeconds(1) ||
                !_transform.InRange(Transform(other).Coordinates, Transform(patient).Coordinates, 14) ||
                !CoveringFireReady(other, buddy, out var target))
                continue;
            if (buddy.CoveringFor != null && now < buddy.CoveringUntil)
                continue;
            if (threats.RemoveAll(point => _transform.InRange(point, Transform(target).Coordinates, 3)) > 0)
                covering.Add(buddy);
        }
        if (threats.Count != 0)
            return false;
        if (commit)
            foreach (var buddy in covering)
                buddy.MedicalCoverUntil = now + TimeSpan.FromSeconds(2);
        return true;
    }

    private bool MedicalWorkSafe(EntityUid uid, CMUExpeditionAgentComponent agent, CMUExpeditionMedicComponent medic, EntityUid patient) =>
        _npcs.Enabled && !HasComp<ActorComponent>(uid) && _mobs.IsAlive(uid) && ValidMedicalPatient(uid, patient) &&
        _interaction.InRangeUnobstructed(uid, patient, 1.5f) && !GrenadeDanger(Transform(patient).Coordinates) &&
        TryComp<CMUExpeditionPatientComponent>(patient, out var claim) && claim.Medic == uid &&
        _timing.CurTime - claim.LastWound >= TimeSpan.FromSeconds(1) &&
        _timing.CurTime - agent.LastHit >= TimeSpan.FromSeconds(1) && agent.RushTarget == null &&
        (TreatmentSafe(uid, agent) && ShelteredFromKnownThreats(uid, agent, Transform(patient).Coordinates) ||
         _mobs.IsAlive(patient) && medic.Covered && !agent.Crossfire && _timing.CurTime >= agent.SuppressedUntil);

    private bool FreeMedicalHand(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        agent.RifleLoweredUntil = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        if (_guns.TryGetGun(uid, out var gun))
            _wield.TryUnwield(gun.Owner, uid);
        return _hands.GetEmptyHandCount(uid) > 0;
    }

    private void CancelMedical(EntityUid uid, CMUExpeditionAgentComponent agent, string decision, bool failed = false)
    {
        if (!TryComp<CMUExpeditionMedicComponent>(uid, out var medic) || medic.Patient is not { } patient)
            return;
        medic.Patient = null;
        if (medic.DoAfter is { } doAfter)
            _doAfter.Cancel(doAfter);
        medic.DoAfter = null;
        ReleaseCasualty(uid, agent);
        StowMedicalItem(uid, medic);
        if (TryComp<CMUExpeditionPatientComponent>(patient, out var claim) && claim.Medic == uid)
            RemComp<CMUExpeditionPatientComponent>(patient);
        medic.RetryAfter[patient] = _timing.CurTime + TimeSpan.FromSeconds(failed ? 8 : 3);
        medic.NextTriage = _timing.CurTime + TimeSpan.FromSeconds(1);
        medic.Phase = CMUExpeditionMedicalPhase.Idle;
        medic.Decision = decision;
        agent.Action = null;
        agent.Casualty = null;
        ClearCover(agent);
        _steering.Unregister(uid);
        agent.State = CMUExpeditionAgentState.Guard;
    }

    private void StowMedicalItem(EntityUid uid, CMUExpeditionMedicComponent medic)
    {
        if (medic.Item is { } item && Exists(item) && _hands.IsHolding(uid, item, out _) &&
            (!Supplies(uid, out var bag) || !_hands.TryDropIntoContainer(uid, item, bag.Container)))
            _hands.TryDrop(uid, item);
        medic.Item = null;
    }
}
