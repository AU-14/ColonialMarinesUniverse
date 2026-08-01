using Robust.Shared.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace Content.Shared.AU14.Objectives;

[Serializable, NetSerializable]
public enum ObjectiveStatusDisplay
{
    Uncompleted,
    Completed,
    Failed,
    Captured,
    Uncaptured
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
public sealed class ObjectivesConsoleRequestIntelMessage : BoundUserInterfaceMessage
{
    public readonly string ObjectiveId;
    public ObjectivesConsoleRequestIntelMessage(string objectiveId)
    {
        ObjectiveId = objectiveId;
    }
}

[Serializable, NetSerializable]
public sealed class ObjectivesConsoleUnlockIntelMessage : BoundUserInterfaceMessage
{
    public readonly string ObjectiveId;
    public readonly int TierIndex;
    public ObjectivesConsoleUnlockIntelMessage(string objectiveId, int tierIndex)
    {
        ObjectiveId = objectiveId;
        TierIndex = tierIndex;
    }
}

[Serializable, NetSerializable]
public sealed class ObjectiveIntelTierEntry
{
    public int Index { get; }
    public string Title { get; }
    public string Description { get; }
    public double CostToUnlock { get; }

    public ObjectiveIntelTierEntry(int index, string title, string description, double cost)
    {
        Index = index;
        Title = title;
        Description = description;
        CostToUnlock = cost;
    }
}

[Serializable, NetSerializable]
public sealed class ObjectiveIntelBoundUserInterfaceState : BoundUserInterfaceState
{
    public string ObjectiveId { get; }
    public string ObjectiveDefaultTitle { get; }

    public List<ObjectiveIntelTierEntry> Tiers { get; }

    public int UnlockedTier { get; }
    public int FactionPoints { get; }

    public ObjectiveIntelBoundUserInterfaceState(string objectiveId, string defaultTitle, List<ObjectiveIntelTierEntry> tiers, int unlockedTier, int factionPoints)
    {
        ObjectiveId = objectiveId;
        ObjectiveDefaultTitle = defaultTitle;
        Tiers = tiers;
        UnlockedTier = unlockedTier;
        FactionPoints = factionPoints;
    }
}

// Update ObjectiveEntry to carry an ID so client can request intel for that objective
[Serializable, NetSerializable]
public sealed class ObjectiveEntry
{
    public string Id { get; }
    public string Description { get; }
    public ObjectiveStatusDisplay Status { get; }
    public ObjectiveTypeDisplay Type { get; }
    public string? Progress { get; }
    public bool Repeating { get; }
    public int? RepeatsCompleted { get; }
    public int? MaxRepeatable { get; }
    public int Points { get; }
    public ObjectiveEntry(string id, string description, ObjectiveStatusDisplay status, ObjectiveTypeDisplay type, string? progress = null, bool repeating = false, int? repeatsCompleted = null, int? maxRepeatable = null, int points = 0)
    {
        Id = id;
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
