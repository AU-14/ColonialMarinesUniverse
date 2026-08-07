namespace Content.Shared._RMC14.Xenonids.Hive;

/// <summary>
///    Event that is raised whenever the Queen of a Hive changes;
/// </summary>
/// <param name="OldQueen"></param>
/// <param name="NewQueen"></param>
public record struct XenoHiveQueenChangedEvent(EntityUid? OldQueen, EntityUid? NewQueen);
