namespace Content.Shared._RMC14.Xenonids.JoinXeno;

public sealed class GetLarvaPoolCandidateCountEvent(EntityUid? hive = null) : EntityEventArgs
{
    public EntityUid? Hive { get; } = hive;

    public int Count { get; set; }
}
