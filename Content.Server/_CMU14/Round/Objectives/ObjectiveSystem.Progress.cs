using Content.Server._CMU14.Round.Objectives.Components;
using Content.Shared._CMU14.Round.Objectives.Components;

namespace Content.Server._CMU14.Round.Objectives;

public abstract partial class ObjectiveSystem
{
    [Dependency] protected ObjectiveControlSystem ObjCtrl = default!;
    [Dependency] protected ObjectiveInterestSystem ObjInt = default!;

    protected bool TryCreditObjective(
        EntityUid uid,
        CMUObjectiveComponent auComp,
        Dictionary<string, int> perFaction,
        string factionKey,
        int required)
    {
        perFaction.TryAdd(factionKey, 0);
        perFaction[factionKey]++;
        if (perFaction[factionKey] < required)
            return false;

        ObjInt.UnregisterInterest(uid);
        ObjCtrl.CompleteObjectiveForFaction(uid, auComp, factionKey, sawmill: _logs);
        return true;
    }

    protected void CleanupSpawnedByObjective(EntityUid uid, Action<EntityUid> unmark)
    {
        var query = EntityQueryEnumerator<ObjSpawnedByComponent>();
        while (query.MoveNext(out var ent, out var spawnedBy))
        {
            if (spawnedBy.ObjectiveUid != uid)
                continue;

            unmark(ent);

            if (Exists(ent))
                QueueDel(ent);
        }
    }
}
