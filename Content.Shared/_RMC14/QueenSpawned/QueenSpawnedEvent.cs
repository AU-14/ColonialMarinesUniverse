namespace Content.Shared._RMC14.QueenSpawned;

public sealed class QueenSpawnedEvent : EntityEventArgs
{
    public EntityUid Queen { get; }

    public QueenSpawnedEvent(EntityUid queen)
    {
        Queen = queen;
    }
}
