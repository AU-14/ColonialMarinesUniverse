namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    // Explicit orders, incapacitation and loss of AI control all revoke the same
    // transient work. Persistent orders and observed contacts belong to the caller.
    private void CancelAgentActivity(EntityUid uid, CMUExpeditionAgentComponent agent, string reason)
    {
        agent.ReturningHome = false;
        CancelVault(agent);
        CancelPortalClimb(uid, agent);
        CancelAimedWeapon(agent);
        CancelFlare(uid, agent);
        CancelFireResponse(uid, agent);
        CancelWork(uid, agent);
        CancelPlan(uid, agent, false);
        CancelTreatment(agent);
        StopSpacing(uid, agent);
        ClearTraffic(agent);
        ClearDoorFocus(agent);
        ClearLastStand(uid, agent);
        ClearCornerResponse(agent);
        agent.PendingWeapon = null;
        agent.SupplyTransfer = null;
        agent.SupplyRecipient = null;
        agent.WaitingForDoor = null;
        agent.CoveringFor = null;
        agent.CoveringUntil = TimeSpan.Zero;
        agent.ContactDestination = null;
        agent.ContactMoveUntil = TimeSpan.Zero;
        agent.FlankAssignment = null;
        agent.FlankAssignmentUntil = TimeSpan.Zero;
        agent.FightingPosition = null;
        agent.PositionCommittedUntil = TimeSpan.Zero;
        agent.MovingFire = false;
        agent.ResumeVolley = false;
        agent.FiringAtFlash = false;
        agent.MoveProgressDestination = null;
        ClearCover(agent);
        _steering.Unregister(uid);
        Decision(agent, "interrupted", reason);
    }

    private void BeginHomeReturn(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.ReturningHome)
            return;
        CancelAgentActivity(uid, agent, "outside-leash");
        agent.ReturningHome = true;
    }
}
