using Robust.Shared.Map;
using Content.Shared.DoAfter;

namespace Content.Server.CMU14.Expeditions;

/// <summary>Server-owned decisions; native steering and gun systems execute movement and fire.</summary>
[RegisterComponent]
public sealed partial class CMUExpeditionAgentComponent : Component
{
    [DataField] public float DetectionRange = 18;
    [DataField] public float FireRange = 12;
    [DataField] public float LeashRange = 28;
    [DataField] public CMUExpeditionDisposition Disposition = CMUExpeditionDisposition.Steady;
    [DataField] public float Aggression = 0.5f;
    [DataField] public float Courage = 0.5f;
    [DataField] public float PreferredFireRange = 8;
    [DataField] public float RetreatDamage = 55;
    [DataField] public float HealDamage = 18;
    [DataField] public float EmergencyHealDamage = 85;
    [DataField] public TimeSpan HealOpportunityDelay = TimeSpan.FromSeconds(14);
    [DataField] public TimeSpan MemoryDuration = TimeSpan.FromSeconds(6);
    [DataField] public TimeSpan AimDuration = TimeSpan.FromSeconds(0.18);
    [DataField] public TimeSpan PeekAimDuration = TimeSpan.FromSeconds(0.08);
    [DataField] public int BurstSize = 3;
    [DataField] public TimeSpan BurstDuration = TimeSpan.FromSeconds(1.2);
    [DataField] public TimeSpan BurstPause = TimeSpan.FromSeconds(0.55);
    [DataField] public TimeSpan LostSightDelay = TimeSpan.FromSeconds(1.5);
    [DataField] public TimeSpan RepositionCooldown = TimeSpan.FromSeconds(4);
    [DataField] public TimeSpan RepositionTimeout = TimeSpan.FromSeconds(5);
    [DataField] public float MinimumFireRange = 4;
    public EntityCoordinates? Home;
    public EntityCoordinates? LastSeen;
    public EntityCoordinates? CoverDestination;
    public EntityCoordinates? CoverAnchor;
    public EntityCoordinates? PeekPosition;
    public EntityUid? Target;
    public TimeSpan ForgetAt;
    public TimeSpan LastContact;
    public TimeSpan NextTargetSwitch;
    public EntityCoordinates? InvestigationDestination;
    public EntityCoordinates? InvestigationContact;
    public TimeSpan NextInvestigation;
    public TimeSpan? LostAimSince;
    public TimeSpan FireAt;
    public TimeSpan BurstEnd;
    public TimeSpan MoveUntil;
    public EntityCoordinates? MoveProgressDestination;
    public float MoveProgressDistance;
    public TimeSpan MoveProgressAt;
    public TimeSpan RifleLoweredUntil;
    public int ShotsFired;
    public bool ResumeVolley;
    public string LastFireCheck = "idle";
    public float LastDamage;
    public TimeSpan SuppressedUntil;
    public TimeSpan NextSuppressionResponse;
    public EntityCoordinates? FailedPosition;
    public TimeSpan AvoidPositionUntil;
    public TimeSpan LastHit;
    public TimeSpan? WoundedSince;
    public TimeSpan LastEmotionUpdate;
    public float Stress;
    public float Initiative;
    public CMUExpeditionEmotion Emotion;
    public int SupportingAllies;
    public bool HasCoveringAlly;
    public int FollowupBursts;
    public TimeSpan NextPeekAdjustment;
    public int LastSearchCells;
    public double LastSearchMilliseconds;
    public TimeSpan NextThink;
    public TimeSpan NextReposition;
    public TimeSpan NextRetreat;
    public TimeSpan HoldUntil;
    public TimeSpan NextHeal;
    public DoAfterId? Treatment;
    public List<EntityCoordinates> VisibleThreats = new();
    public CMUExpeditionAgentState State;
}

public enum CMUExpeditionAgentState : byte
{
    Guard,
    Investigate,
    Watch,
    Aim,
    Engage,
    HoldAngle,
    Recover,
    Reposition,
    Peeking,
    Withdraw,
    Retreat,
    Healing,
    OutOfAmmo,
    PlanMove,
    Reloading,
    Rescuing,
    Throwing,
    Disabled,
    Incapacitated,
    RecoverWeapon,
    Scavenge,
}

public enum CMUExpeditionDisposition : byte { Steady, Aggressive, Cautious }
public enum CMUExpeditionEmotion : byte { Calm, Alert, Confident, Shaken, Desperate }
