namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>Raised on the Joe after its level changes and the new level's grants are applied.</summary>
[ByRefEvent]
public readonly record struct EWJoeLevelChangedEvent(int OldLevel, int NewLevel);
