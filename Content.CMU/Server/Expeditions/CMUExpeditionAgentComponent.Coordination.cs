using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public CMUExpeditionCombatRole CombatRole;
    [DataField] public TimeSpan PositionCommitDuration = TimeSpan.FromSeconds(3);
    public EntityUid? CoveringShooter;
    public EntityUid? CoveringFor;
    public TimeSpan CoveringUntil;
    public TimeSpan ManeuverUntil;
    public EntityUid? ManeuverWaitTarget;
    public TimeSpan? ManeuverWaitSince;
    public EntityCoordinates? UncoveredManeuverDestination;
    public string SquadDecision = "idle";
    public EntityCoordinates? ContactDestination;
    public EntityCoordinates? FightingPosition;
    public TimeSpan PositionCommittedUntil;
    public EntityUid? FlankAssignment;
    public TimeSpan FlankAssignmentUntil;
    public int CoveredMoves;
    public int InterruptedMoves;
    public EntityUid? LastFiredWeapon;
    public EntityCoordinates? TrafficGoal;
    public TimeSpan TrafficTicket;
    public TimeSpan TrafficActiveUntil;
    public EntityUid? TrafficYieldTo;
    public EntityCoordinates? TrafficYieldPoint;
    public EntityCoordinates? TrafficPassExit;
    public TimeSpan NextTrafficPocket;
    public TimeSpan? TrafficWaitingSince;
    public EntityCoordinates? TrafficBlockedPoint;
    public TimeSpan AvoidTrafficUntil;
    public string TrafficDecision = "clear";
}

public enum CMUExpeditionCombatRole : byte
{
    Rifleman,
    Support,
    Flanker,
    Marksman,
    Breacher,
    Medic,
}
