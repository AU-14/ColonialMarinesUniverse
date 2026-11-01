using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void QueueUtilityCleanup(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid item)
    {
        if (HasComp<ActorComponent>(uid) || !Exists(item) || !_hands.IsHolding(uid, item, out _))
            return;
        // Cancellation can be reentrant. Keep every still-held item owned until its
        // native transfer succeeds; a later item must not replace an earlier one.
        if (agent.UtilityCleanupItem == null)
        {
            agent.UtilityCleanupItem = item;
            agent.NextUtilityCleanup = TimeSpan.Zero;
        }
        else if (agent.UtilityCleanupItem != item && !agent.UtilityCleanupQueue.Contains(item))
            agent.UtilityCleanupQueue.Enqueue(item);
        RetryUtilityCleanup(uid, agent, _timing.CurTime);
    }

    private void RetryUtilityCleanup(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.UtilityCleanupItem is not { } item)
            return;
        if (HasComp<ActorComponent>(uid))
        {
            agent.UtilityCleanupItem = null;
            agent.UtilityCleanupQueue.Clear();
            agent.NextUtilityCleanup = TimeSpan.Zero;
            return;
        }
        if (!Exists(item) || !_hands.IsHolding(uid, item, out _))
        {
            FinishUtilityCleanup(agent);
            return;
        }
        if (now < agent.NextUtilityCleanup)
            return;
        // Knockdown can reject both storage and dropping. Retain the actual item and
        // retry at a bounded cadence after recovery, without blocking escape movement.
        agent.NextUtilityCleanup = now + TimeSpan.FromSeconds(0.5);
        if (!StoreSupply(uid, item) && !_hands.TryDrop(uid, item))
        {
            agent.WeaponDecision = "utility-cleanup-blocked";
            return;
        }
        FinishUtilityCleanup(agent);
    }

    private static void FinishUtilityCleanup(CMUExpeditionAgentComponent agent)
    {
        agent.UtilityCleanupItem = agent.UtilityCleanupQueue.TryDequeue(out var next) ? next : null;
        agent.NextUtilityCleanup = TimeSpan.Zero;
    }
}
