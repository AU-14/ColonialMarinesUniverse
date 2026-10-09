using Content.Shared.CMU14.Expeditions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public int Squad;
    [DataField] public bool PlanningEnabled = true;
    public CMUTacticalGoal Goal;
    public Queue<CMUTacticalAction> Plan = new();
    public CMUTacticalAction? Action;
    public TimeSpan NextPlan;
    public TimeSpan ActionUntil;
    public TimeSpan ActionStarted;
    public EntityCoordinates? ActionDestination;
    public EntityCoordinates? ActiveMoveDestination;
    public EntityUid? ActionItem;
    public DoAfterId? ActionDoAfter;
    public bool ActionComplete;
    public EntityUid? Casualty;
    public EntityCoordinates? RescueShelter;
    public EntityCoordinates? GrenadeTarget;
    public bool SmokeGrenade;
    public TimeSpan NextGrenade;
    public TimeSpan FirstContact;
    public TimeSpan LastGrenade;
    public TimeSpan GrenadeReservationUntil;
    public TimeSpan GrenadeWindowEnd;
    public TimeSpan SquadGrenadeReady;
    public EntityCoordinates? OrderedDestination;
    public readonly List<EntityCoordinates> PatrolPoints = new();
    public readonly Queue<EntityCoordinates> OrderRoute = new();
    public bool Patrolling;
    public int PatrolIndex;
    public TimeSpan NextOrderRoute;
    public bool OrderBlocked;
    public bool Entrench;
    public EntityUid? WorkItem;
    public bool PreparingWork;
    public DoAfterId? WorkDoAfter;
    public bool WorkBuild;
    public TimeSpan NextWork;
    public int Fortifications;
    public readonly HashSet<string> FriendlyFactions = new();
    public readonly HashSet<string> TargetFactions = new();
    public TimeSpan NextRescue;
    public TimeSpan NextFlank;
    public TimeSpan NextRadio;
    public EntityUid? RadioTarget;
    public EntityCoordinates? RadioPosition;
    public TimeSpan RadioObservedAt;
    public TimeSpan RadioDeliveryAt;
    [DataField] public TimeSpan RadioMemoryDuration = TimeSpan.FromSeconds(12);
    public bool ContactFromRadio;
    public int ReportsReceived;
    public int ReportsAccepted;
    public TimeSpan NextRadioResponse;
    public string RadioDecision = "idle";
    public int Reloads;
    public int GrenadesThrown;
    public int Rescues;
    public int Flanks;
    public int FailedPlans;
    public int PlannerExpanded;
    public readonly Dictionary<CMUTacticalAction, TimeSpan> FailedActions = new();
    public readonly Queue<EntityCoordinates> Route = new();
    public EntityCoordinates? RouteDestination;
    public int LastRouteCells;
    public double LastRouteMilliseconds;
    public int Searches;
    public double TotalSearchMilliseconds;
    public double MaxSearchMilliseconds;
    public int RepeatedPeekHits;
    public TimeSpan LastPeekHit;
    public float LearnedFlankCost = 1;
    public float LearnedDangerCost = 1;
    public bool LearningLoaded;
    public float ActionInitialDamage;
    public float PeekInitialDamage;
    public bool LastMoveFailed;
    public TimeSpan? BlockedShotSince;
}

[RegisterComponent]
public sealed partial class CMUExpeditionGrenadeComponent : Component
{
    [DataField] public bool Smoke;
    [DataField] public float SafeRadius = 6;
}

[RegisterComponent]
public sealed partial class CMUExpeditionRadioComponent : Component;
