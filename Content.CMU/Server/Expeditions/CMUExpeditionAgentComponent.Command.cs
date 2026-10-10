using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public bool PendingInterruptingDamage;
    public EntityUid? SquadRoot;
    public string Doctrine = "balanced";
    public CMUSquadDuty Duty;
    public string SquadPhase = "holding";
    public EntityCoordinates? DutyPoint;
    public TimeSpan NextDutyMove;
    public TimeSpan NextRegroupRoute;
    public TimeSpan DutyUntil;
    public string DecisionOwner = "idle";
    public string DecisionReason = "idle";
    public uint DecisionRevision;
    public TimeSpan DecisionUntil;
    public double LastThinkMilliseconds;
    public double AverageThinkMilliseconds;
    public double MaxThinkMilliseconds;
    public long ThinkSamples;
    public long StateTransitions;
    public bool ReturningHome;
    public readonly Queue<string> DecisionHistory = new();
    public readonly List<(EntityCoordinates Point, TimeSpan Until, int Hits)> BadCover = new();
    public float? BasePreferredRange;
    public float? BaseAggression;
    public float? BaseCourage;
    public TimeSpan? BasePositionCommit;
    public EntityCoordinates? HeardPoint;
    public string HeardKind = "none";
    public TimeSpan HeardAt;
    public TimeSpan HeardUntil;
    public TimeSpan NextHearing;
    public EntityUid? SupplySource;
    public EntityUid? DeliveryRecipient;
    public EntityCoordinates? DeliveryPoint;
    public readonly Dictionary<EntityUid, TimeSpan> FailedDeliveries = new();
    public TimeSpan SupplyRunUntil;
    public TimeSpan NextSupplyRun;
    public TimeSpan RecoveryUntil;
    public EntityCoordinates? TravelGoal;
    public EntityUid? TravelPortal;
    public int TravelOffset;
    public TimeSpan PortalUntil;
    public bool PortalActivated;
    public TimeSpan NextPortalSearch;
    public readonly Dictionary<EntityUid, TimeSpan> FailedPortals = new();
}

public enum CMUSquadDuty : byte { Reserve, Overwatch, Advance, RearGuard, Medic, AntiArmor, Recover, Runner }
