using Robust.Shared.Serialization;
using System.Collections.Generic;

namespace Content.Shared.AU14.Objectives;

[Serializable, NetSerializable]
public enum ObjectiveStatusDisplay
{
    Uncompleted,
    Completed,
    Failed
}

[Serializable, NetSerializable]
public enum ObjectiveTypeDisplay
{
    Minor,
    Major,
    Win
}

[Serializable, NetSerializable]
public sealed class ObjectivesConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<ObjectiveEntry> Objectives { get; }
    public int CurrentWinPoints { get; }
    public int RequiredWinPoints { get; }
    public ObjectivesConsoleBoundUserInterfaceState(List<ObjectiveEntry> objectives, int currentWinPoints, int requiredWinPoints)
    {
        Objectives = objectives;
        CurrentWinPoints = currentWinPoints;
        RequiredWinPoints = requiredWinPoints;
    }
}

[Serializable, NetSerializable]
public sealed class ObjectivesConsoleRequestObjectivesMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public enum ObjectivesConsoleKey : byte
{
    Key
}
[Serializable, NetSerializable]
public sealed class ObjectiveEntry
{
    public string Description { get; }
    public ObjectiveStatusDisplay Status { get; }
    public ObjectiveTypeDisplay Type { get; }
    public string? Progress { get; }
    public bool Repeating { get; }
    public int? RepeatsCompleted { get; }
    public int? MaxRepeatable { get; }
    public int Points { get; }
    public ObjectiveEntry(string description, ObjectiveStatusDisplay status, ObjectiveTypeDisplay type, string? progress = null, bool repeating = false, int? repeatsCompleted = null, int? maxRepeatable = null, int points = 0)
    {
        Description = description;
        Status = status;
        Type = type;
        Progress = progress;
        Repeating = repeating;
        RepeatsCompleted = repeatsCompleted;
        MaxRepeatable = maxRepeatable;
        Points = points;
    }
}
