using System.Linq;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool LogisticsSafe(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        _mobs.IsAlive(uid) && !HasComp<ActorComponent>(uid) && agent.UtilityCleanupItem == null && agent.Action == null && agent.PendingWeapon == null &&
        agent.FlareItem == null && agent.WorkItem == null && !agent.PreparingWork && agent.Treatment == null &&
        agent.ScavengeTarget == null && agent.RushTarget == null && !CommittedMovement(agent) &&
        agent.Target == null && _timing.CurTime - agent.LastContact > TimeSpan.FromSeconds(6) &&
        _timing.CurTime - agent.LastHit > TimeSpan.FromSeconds(3) && !GrenadeDanger(Transform(uid).Coordinates);

    private bool ShareSupplies(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!LogisticsSafe(uid, agent))
        {
            agent.SupplyTransfer = null;
            agent.SupplyRecipient = null;
            return false;
        }
        if (agent.SupplyTransfer is { } item)
        {
            if (agent.SupplyRecipient is not { } recipient || !TryComp<CMUExpeditionAgentComponent>(recipient, out var buddy) ||
                !SameSquad(uid, agent, recipient, buddy) || !LogisticsSafe(recipient, buddy) ||
                !_interaction.InRangeUnobstructed(uid, recipient) || !Exists(item) ||
                !CanDonate(uid, item) || !WantsSupply(recipient, item, true) || !CanStoreSupply(recipient, item))
            {
                agent.SupplyTransfer = null;
                agent.SupplyRecipient = null;
                return false;
            }
            _steering.Unregister(uid);
            if (now < agent.SupplyShareAt)
                return true;
            // Both participants own accessible storage and consent through their AI. Transfer
            // the actual item using native insertion after a bounded, interruptible handoff.
            if (StoreSupply(recipient, item))
            {
                agent.SuppliesShared++;
                buddy.SuppliesReceived++;
                buddy.NextPlan = now;
                buddy.NextWeaponChoice = now;
                agent.SupplyDecision = "supplies-handed-over";
                buddy.SupplyDecision = "supplies-received";
            }
            agent.SupplyTransfer = null;
            agent.SupplyRecipient = null;
            return false;
        }
        if (now < agent.NextSupplyShare || agent.OrderedDestination != null)
            return false;
        agent.NextSupplyShare = now + TimeSpan.FromSeconds(5);
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, 2, nearby);
        foreach (var recipient in nearby.OrderBy(other => other.Id))
        {
            if (recipient == uid || !TryComp<CMUExpeditionAgentComponent>(recipient, out var buddy) ||
                !SameSquad(uid, agent, recipient, buddy) || !LogisticsSafe(recipient, buddy) ||
                buddy.SupplyTransfer != null || !_interaction.InRangeUnobstructed(uid, recipient))
                continue;
            foreach (var supply in SupplyItems(uid).ToArray())
            {
                if (!CanDonate(uid, supply) || !WantsSupply(recipient, supply, true) || !CanStoreSupply(recipient, supply))
                    continue;
                agent.SupplyTransfer = supply;
                agent.SupplyRecipient = recipient;
                agent.SupplyShareAt = now + TimeSpan.FromSeconds(0.8);
                agent.SupplyDecision = "sharing-with-squadmate";
                _steering.Unregister(uid);
                return true;
            }
        }
        return false;
    }
}
