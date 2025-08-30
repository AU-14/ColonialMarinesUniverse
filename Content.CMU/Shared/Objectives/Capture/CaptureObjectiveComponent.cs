using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Capture;

[RegisterComponent, NetworkedComponent]

public sealed partial class CaptureObjectiveComponent : Component
{


    [DataField("linkedgenerator", required: false)]
    public string linkedgenerator { get; private set; } = string.Empty;

    [DataField("siege", required: false)]
    public bool Siege { get; private set; } = false;
    // not yet implemented
    [DataField("", required: false)]
    public bool OnceOnly { get; private set; } = false;

    [DataField("maxholdtimes", required: false)]
    public int MaxHoldTimes { get; private set; } = 0;

    [DataField("pointincrementtime", required: false)]
    public float PointIncrementTime { get; private set; } = 5.0f;

    [DataField("visibleonmap", required: false)]
    public bool VisibleOnMap { get; private set; } = true;

    [DataField("commlink", required: false)]
    public bool Commlink { get; private set; } = false;

    [DataField("airfield",required: false)]
    public string Airfield { get; private set; } = string.Empty;

    [DataField("hoisttime", required: false)]
    public float HoistTime { get; private set; } = 5.0f;

    public string CurrentController = string.Empty;

    public int timesincremented = 0;
// for maxholdtimes
    [DataField("flaghealth", required: false)]

    public float FlagInitialHealth { get; private set; } = 100f;

public float FlagHealth = 20f;
    public enum CaptureObjectiveStatus
    {
        Failed,
        Captured,
        Uncaptured,
        Completed
    }

    public CaptureObjectiveStatus GetObjectiveStatus(string playerFaction, AuObjectiveComponent? objComp = null)
    {
        var factionKey = playerFaction.ToLowerInvariant();
        // If the objective is completed/failed for this faction, return that
        if (objComp != null && objComp.FactionStatuses.TryGetValue(factionKey, out var status))
        {
            if (status == AuObjectiveComponent.ObjectiveStatus.Completed)
                return CaptureObjectiveStatus.Completed;
            if (status == AuObjectiveComponent.ObjectiveStatus.Failed)
                return CaptureObjectiveStatus.Failed;
        }
        // If the objective is still active, show captured for theF current holder
        if (!string.IsNullOrEmpty(CurrentController) && CurrentController.ToLowerInvariant() == factionKey)
            return CaptureObjectiveStatus.Captured;
        // If the flag is not held, show uncaptured
        if (string.IsNullOrEmpty(CurrentController))
            return CaptureObjectiveStatus.Uncaptured;
        // If another faction is holding, show uncaptured
        return CaptureObjectiveStatus.Uncaptured;
    }

    public string GovforFlagState = "uaflag_worn";
    public string OpforFlagState = "uaflag";

    // Tracks how many times each faction has held the flag (for progress display)
    public Dictionary<string, int> TimesIncrementedPerFaction { get; set; } = new();

    public enum FlagActionState
    {
        Idle,
        Hoisting,
        Lowering
    }

    public FlagActionState ActionState = FlagActionState.Idle;
    public EntityUid? ActionUser = null;
    public float ActionTimeRemaining = 0f;
    public string? ActionUserFaction = null;

}
