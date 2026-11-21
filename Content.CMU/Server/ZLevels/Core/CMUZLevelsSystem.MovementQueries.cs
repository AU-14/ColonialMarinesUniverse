using Content.Shared.CMU14.Dropship.MultiDeck;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    private EntityQuery<DropshipDeckComponent> _movementDeckQuery;

    private void InitializeMovementDeckQueries()
    {
        _movementDeckQuery = GetEntityQuery<DropshipDeckComponent>();
    }

    protected override void FindMovementDecks(EntityUid mapUid, Box2 bounds, ref List<Entity<MapGridComponent>> candidates)
    {
        var state = (_movementDeckQuery, candidates);
        _map.FindGridsIntersectingFiltered(mapUid, bounds, ref state,
            static (EntityUid uid, MapGridComponent grid,
                ref (EntityQuery<DropshipDeckComponent> Query, List<Entity<MapGridComponent>> Candidates) state) =>
            {
                state.Candidates.Add((uid, grid));
                return true;
            },
            static (EntityUid uid, MapGridComponent _,
                ref (EntityQuery<DropshipDeckComponent> Query, List<Entity<MapGridComponent>> Candidates) state) =>
                state.Query.HasComp(uid));
    }
}
