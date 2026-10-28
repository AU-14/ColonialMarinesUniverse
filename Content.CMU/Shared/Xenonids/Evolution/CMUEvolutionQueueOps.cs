namespace Content.Shared.CMU14.Xenonids.Evolution;

/// <summary>
///     Pure list operations for the evolution tier queue, split out for unit testing.
/// </summary>
public static class CMUEvolutionQueueOps
{
    /// <summary>
    ///     Inserts an entry into the queue. Hive leaders automatically queue ahead of
    ///     non-leaders but behind leaders already waiting. Returns the inserted index.
    /// </summary>
    public static int Enqueue(List<CMUEvolutionQueueEntry> queue,
        CMUEvolutionQueueEntry entry,
        Predicate<EntityUid> isLeader)
    {
        var index = queue.Count;
        if (isLeader(entry.Xeno))
        {
            index = 0;
            while (index < queue.Count && isLeader(queue[index].Xeno))
            {
                index++;
            }
        }

        queue.Insert(index, entry);
        return index;
    }

    /// <summary>
    ///     Moves or removes the given xeno's entry. Returns false if the entry is missing
    ///     or the move is a no-op.
    /// </summary>
    public static bool Move(List<CMUEvolutionQueueEntry> queue, EntityUid xeno, CMUEvolutionQueueMove move)
    {
        var index = queue.FindIndex(entry => entry.Xeno == xeno);
        if (index < 0)
            return false;

        var entry = queue[index];
        switch (move)
        {
            case CMUEvolutionQueueMove.Front:
                if (index == 0)
                    return false;
                queue.RemoveAt(index);
                queue.Insert(0, entry);
                return true;
            case CMUEvolutionQueueMove.Up:
                if (index == 0)
                    return false;
                queue[index] = queue[index - 1];
                queue[index - 1] = entry;
                return true;
            case CMUEvolutionQueueMove.Down:
                if (index == queue.Count - 1)
                    return false;
                queue[index] = queue[index + 1];
                queue[index + 1] = entry;
                return true;
            case CMUEvolutionQueueMove.Remove:
                queue.RemoveAt(index);
                return true;
            default:
                return false;
        }
    }
}
