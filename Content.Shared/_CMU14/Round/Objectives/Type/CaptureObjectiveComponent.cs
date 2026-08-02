using Content.Shared._CMU14.Round.Objectives.Component;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent, NetworkedComponent]
public sealed partial class CaptureObjectiveComponent : Robust.Shared.GameObjects.Component
{
    public enum CaptureObjectiveStatus
    {
        Failed,
        Captured,
        Uncaptured,
        Completed
    }

    public enum FlagActionState
    {
        Idle,
        Hoisting,
        Lowering
    }

    [DataField] public bool OnceOnly { get; private set; }
    [DataField] public int MaxHoldTimes { get; private set; }
    [DataField] public float PointIncrementTime { get; private set; } = 5.0f;
    [DataField] public bool VisibleOnMap { get; private set; } = true;
    [DataField] public string Airfield { get; private set; } = string.Empty;
    [DataField] public float HoistTime { get; private set; } = 5.0f;

    public string CurrentController = string.Empty;
    public int TimesIncremented = 0;

    [DataField] public float FlagInitialHealth { get; private set; } = 100f;
    public float FlagHealth = 20f;

    public string GovforFlagState = "uaflag_worn";
    public string OpforFlagState = "uaflag";
    public Dictionary<string, int> TimesIncrementedPerFaction { get; set; } = new();

    public FlagActionState ActionState = FlagActionState.Idle;
    public EntityUid? ActionUser;
    public string? ActionUserFaction;

    public CaptureObjectiveStatus GetObjectiveStatus(string playerFaction, CMUObjectiveComponent? objComp = null)
    {
        var factionKey = playerFaction.ToLowerInvariant();
        if (objComp != null && objComp.StatusesPerFaction.TryGetValue(factionKey, out var status))
        {
            if (status == CMUObjectiveComponent.ObjectiveStatus.Completed)
                return CaptureObjectiveStatus.Completed;
            if (status == CMUObjectiveComponent.ObjectiveStatus.Failed)
                return CaptureObjectiveStatus.Failed;
        }

        if (!string.IsNullOrEmpty(CurrentController) && CurrentController.ToLowerInvariant() == factionKey)
            return CaptureObjectiveStatus.Captured;

        return CaptureObjectiveStatus.Uncaptured;
    }
}
