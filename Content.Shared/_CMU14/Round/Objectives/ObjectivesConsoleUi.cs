using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Round.Objectives;

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
public sealed class ObjectivesConsoleBoundUserInterfaceState(
    List<ObjectiveEntry> objectives,
    int currentWinPoints,
    int requiredWinPoints)
    : BoundUserInterfaceState
{
    public List<ObjectiveEntry> Objectives { get; } = objectives;
    public int CurrentWinPoints { get; } = currentWinPoints;
    public int RequiredWinPoints { get; } = requiredWinPoints;
}

[Serializable, NetSerializable]
public sealed class ObjectivesConsoleRequestObjectivesMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum ObjectivesConsoleKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ObjectivesConsoleRequestIntelMessage(string objectiveId) : BoundUserInterfaceMessage
{
    public readonly string ObjectiveId = objectiveId;
}

[Serializable, NetSerializable]
public sealed class ObjectivesConsoleUnlockIntelMessage(string objectiveId, int tierIndex) : BoundUserInterfaceMessage
{
    public readonly string ObjectiveId = objectiveId;
    public readonly int TierIndex = tierIndex;
}

[Serializable, NetSerializable]
public sealed class ObjectiveIntelTierEntry(int index, string title, string description, double cost)
{
    public int Index { get; } = index;
    public string Title { get; } = title;
    public string Description { get; } = description;
    public double CostToUnlock { get; } = cost;
}

[Serializable, NetSerializable]
public sealed class ObjectiveIntelBoundUserInterfaceMessage(
    string objectiveId,
    string defaultTitle,
    List<ObjectiveIntelTierEntry> tiers,
    int unlockedTier,
    int factionPoints)
    : BoundUserInterfaceMessage
{
    public string ObjectiveId { get; } = objectiveId;
    public string ObjectiveDefaultTitle { get; } = defaultTitle;
    public List<ObjectiveIntelTierEntry> Tiers { get; } = tiers;
    public int UnlockedTier { get; } = unlockedTier;
    public int FactionPoints { get; } = factionPoints;
}

[Serializable, NetSerializable]
public sealed class ObjectiveEntry(
    string id,
    string description,
    ObjectiveStatusDisplay status,
    ObjectiveTypeDisplay type,
    string? progress = null,
    bool repeating = false,
    int? repeatsCompleted = null,
    int? maxRepeatable = null,
    int points = 0)
{
    public string Id { get; } = id;
    public string Description { get; } = description;
    public ObjectiveStatusDisplay Status { get; } = status;
    public ObjectiveTypeDisplay Type { get; } = type;
    public string? Progress { get; } = progress;
    public bool Repeating { get; } = repeating;
    public int? RepeatsCompleted { get; } = repeatsCompleted;
    public int? MaxRepeatable { get; } = maxRepeatable;
    public int Points { get; } = points;
}
