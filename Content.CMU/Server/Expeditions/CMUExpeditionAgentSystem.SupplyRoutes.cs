using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Medical;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool FreshMedicalTool(EntityUid item) => HasComp<CMUExpeditionMedicalToolComponent>(item) &&
        (HasComp<DefibrillatorComponent>(item) && _medicalPower.HasActivatableCharge(item) ||
         TryComp<HyposprayComponent>(item, out var hypo) &&
         _medicalSolutions.TryGetSolution(item, hypo.SolutionName, out _, out var dose) && dose.Volume >= hypo.TransferAmount);

    private bool WantsMedicalTool(EntityUid uid, EntityUid item) => HasComp<CMUExpeditionMedicComponent>(uid) &&
        FreshMedicalTool(item) && !MedicalItems(uid).Any(other => FreshMedicalTool(other) &&
            HasComp<DefibrillatorComponent>(other) == HasComp<DefibrillatorComponent>(item));

    private bool RunnerNeedsItem(EntityUid uid, EntityUid item) =>
        TryComp<CMUExpeditionAgentComponent>(uid, out var agent) && PlanFor(agent) is { } plan && plan.Runner == uid &&
        plan.Members.Any(member => member != uid && CanOrderSquadMember(member) && (WantsSupply(member, item, true) || WantsMedicalTool(member, item))) &&
        SupplyItems(uid).Count(other => MetaData(other).EntityPrototype?.ID == MetaData(item).EntityPrototype?.ID) < 2;

    private void RememberSupplies(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (PlanFor(agent) is not { } plan)
            return;
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, 10, nearby);
        foreach (var source in nearby.OrderBy(item => item.Id))
        {
            if ((!HasComp<StorageComponent>(source) && !HasComp<EntityStorageComponent>(source)) ||
                _containers.IsEntityOrParentInContainer(source) || !Visible(uid, source, 10))
                continue;
            if (plan.Supplies.Count >= 12 && !plan.Supplies.ContainsKey(source))
                break;
            plan.Supplies[source] = (Transform(source).Coordinates, now + TimeSpan.FromSeconds(90));
        }
    }

    private bool SquadNeedsSupplies(SquadPlan plan) => plan.Members.Any(member => CanOrderSquadMember(member) &&
        (CarriedWeapons(member).Any(gun => !(TryComp<CMUExpeditionWeaponRoleComponent>(gun, out var role) && role.Rocket) && AmmoReserve(member, gun) == 0) ||
         Comp<CMUExpeditionAgentComponent>(member).AntiVehicle && !HasReadyRocket(member) ||
         HasComp<CMUExpeditionMedicComponent>(member) && !MedicalItems(member).Any(FreshMedicalTool)));

    private bool RunSupplyRoute(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (PlanFor(agent) is not { } plan)
            return false;
        if (agent.DeliveryRecipient is { } recipient)
        {
            if (now >= agent.SupplyRunUntil || !CanOrderSquadMember(recipient) || !IsFriendly(uid, recipient) ||
                agent.Target != null || now - agent.LastHit < TimeSpan.FromSeconds(2) ||
                !SupplyItems(uid).Any(item => CanDonate(uid, item) && WantsSupply(recipient, item, true)))
            {
                agent.FailedDeliveries[recipient] = now + TimeSpan.FromSeconds(30);
                agent.DeliveryRecipient = null;
                agent.DeliveryPoint = null;
                if (plan.Runner == uid)
                    plan.Runner = null;
                FinishSupplyRun(uid, agent, plan, allowDelivery: false);
                return false;
            }
            if (_interaction.InRangeUnobstructed(uid, recipient))
            {
                _steering.Unregister(uid);
                agent.State = CMUExpeditionAgentState.Guard;
                return false; // ShareSupplies performs the real, interruptible handoff on the next think.
            }
            if (DeliveryApproach(uid, agent, recipient, out var deliveryPoint))
            {
                agent.State = CMUExpeditionAgentState.Investigate;
                agent.SupplyDecision = "delivering-requested-supplies";
                Decision(agent, "supply-delivery", "moving-to-low-squadmate");
                Move(uid, deliveryPoint);
                return true;
            }
            return false;
        }
        if (now >= agent.NextSupplyRun)
        {
            agent.NextSupplyRun = now + TimeSpan.FromSeconds(5);
            RememberSupplies(uid, agent, now);
            if (agent.SupplySource == null && plan.Runner == null && agent.OrderedDestination == null &&
                agent.TravelGoal == null && LogisticsSafe(uid, agent) && SquadNeedsSupplies(plan))
            {
                foreach (var (source, memory) in plan.Supplies)
                {
                    if (!Exists(source) || agent.Home is not { } home || !_transform.InRange(home, memory.Point, agent.LeashRange) ||
                        !_transform.InRange(Transform(uid).Coordinates, memory.Point, 24))
                        continue;
                    agent.SupplySource = source;
                    agent.SupplyRunUntil = now + TimeSpan.FromSeconds(20);
                    plan.Runner = uid;
                    agent.Duty = CMUSquadDuty.Runner;
                    agent.DutyUntil = agent.SupplyRunUntil;
                    break;
                }
            }
        }
        if (agent.SupplySource is not { } selected)
            return false;
        if (now >= agent.SupplyRunUntil || !Exists(selected) || agent.Target != null ||
            now - agent.LastHit < TimeSpan.FromSeconds(2) || agent.RushTarget != null || agent.TravelGoal != null)
        {
            FinishSupplyRun(uid, agent, plan, allowDelivery: agent.Target == null && agent.RushTarget == null &&
                now - agent.LastHit >= TimeSpan.FromSeconds(2) && agent.TravelGoal == null);
            return false;
        }
        if (!plan.Supplies.TryGetValue(selected, out var remembered))
        {
            FinishSupplyRun(uid, agent, plan);
            return false;
        }
        if (_transform.InRange(Transform(uid).Coordinates, remembered.Point, 3.5f))
        {
            if (RunAmmoFallback(uid, agent, now, _guns.TryGetGun(uid, out var gun) && WeaponAmmo(gun) > 0))
                return true;
            // Do not repeatedly walk to an exhausted, locked or incompatible source.
            if (agent.ScavengeTarget == null && now >= agent.NextScavenge - TimeSpan.FromSeconds(.2))
            {
                plan.Supplies.Remove(selected);
                FinishSupplyRun(uid, agent, plan);
            }
            return false;
        }
        Decision(agent, "supply-run", "remembered-visible-storage");
        agent.SupplyDecision = "travelling-to-supply-cache";
        InvestigateContact(uid, agent, remembered.Point, now);
        return true;
    }

    private bool DeliveryApproach(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid recipient, out EntityCoordinates point)
    {
        point = default;
        var target = _transform.GetMapCoordinates(recipient);
        bool Usable(EntityCoordinates candidate) => ValidOrderPoint(uid, candidate) && !Reserved(uid, candidate) &&
            _interaction.InRangeUnobstructed(_transform.ToMapCoordinates(candidate), target, 1.5f,
                predicate: entity => entity == uid || entity == recipient);
        if (agent.DeliveryPoint is { } previous && Usable(previous))
        {
            point = previous;
            return true;
        }
        if (!TrySquadCoordinates(Transform(recipient).Coordinates, out var center))
            return false;
        foreach (var candidate in NearbySquadPositions(center, 1))
        {
            if (!Usable(candidate))
                continue;
            // Move builds the bounded route around corners; only the final handoff needs a clear ray.
            agent.DeliveryPoint = point = candidate;
            agent.Route.Clear();
            agent.RouteDestination = null;
            return true;
        }
        return false;
    }

    private void FinishSupplyRun(EntityUid uid, CMUExpeditionAgentComponent agent, SquadPlan plan, bool allowDelivery = true)
    {
        agent.SupplySource = null;
        agent.DutyUntil = TimeSpan.Zero;
        if (plan.Runner == uid)
            plan.Runner = null;
        agent.NextSupplyRun = _timing.CurTime + TimeSpan.FromSeconds(15);
        foreach (var failed in agent.FailedDeliveries.Where(pair => pair.Value <= _timing.CurTime || !Exists(pair.Key)).Select(pair => pair.Key).ToArray())
            agent.FailedDeliveries.Remove(failed);
        if (agent.FailedDeliveries.Count > 32)
            agent.FailedDeliveries.Clear();
        if (allowDelivery && agent.Target == null)
            foreach (var recipient in plan.Members)
            {
                if (recipient == uid || !CanOrderSquadMember(recipient) || !IsFriendly(uid, recipient) ||
                    agent.FailedDeliveries.ContainsKey(recipient) ||
                    !_transform.InRange(Transform(uid).Coordinates, Transform(recipient).Coordinates, 14) ||
                    !SupplyItems(uid).Any(item => CanDonate(uid, item) && WantsSupply(recipient, item, true)))
                    continue;
                agent.DeliveryRecipient = recipient;
                agent.DeliveryPoint = null;
                agent.SupplyRunUntil = _timing.CurTime + TimeSpan.FromSeconds(20);
                plan.Runner = uid;
                agent.Duty = CMUSquadDuty.Runner;
                agent.DutyUntil = agent.SupplyRunUntil;
                return;
            }
        if (agent.Target == null && agent.RushTarget == null && _timing.CurTime - agent.LastHit >= TimeSpan.FromSeconds(2) &&
            agent.TravelGoal == null && plan.Rally is { } rally && ValidOrderPoint(uid, rally))
        {
            agent.OrderedDestination = rally;
            agent.OrderRally = rally;
            agent.OrderRoute.Clear();
        }
    }
}
