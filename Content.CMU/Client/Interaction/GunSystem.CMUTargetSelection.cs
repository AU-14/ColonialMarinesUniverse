using System.Numerics;
using Content.Client.Gameplay;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private NetEntity? CMUGetCursorTarget(GameplayStateBase? screen, IEye eye, MapCoordinates coordinates)
    {
        // This eligible cursor hit already takes precedence over the fallback in the shooting request.
        if (screen?.GetClickedEntity(coordinates, eye) is { } clicked && CheckFixtures(clicked))
            return GetNetEntity(clicked);
        return GetBestTarget(eye, coordinates);
    }

    private NetEntity? CMUGetBestTarget(IEye eye, MapCoordinates coordinates)
    {
        var query = new CMUGunTargetQuery(this, eye, coordinates);
        _spriteTree.QueryAabb(ref query,
            static (ref CMUGunTargetQuery state, in ComponentTreeEntry<SpriteComponent> entry) =>
            {
                if (!state.Gun.CheckFixtures(entry.Uid))
                    return true;
                var candidate = state.Gun.CheckTarget((entry.Uid, entry.Component, entry.Transform),
                    state.Eye, state.Coordinates);
                if (!state.Found || state.Gun._comparer.Compare(candidate, state.Best) < 0)
                {
                    state.Found = true;
                    state.Best = candidate;
                }
                return true;
            }, coordinates.MapId, Box2.CenteredAround(coordinates.Position, Vector2.One));

        return query.Found && (query.Best.Alive || query.Best.Occluded) ? GetNetEntity(query.Best.Uid) : null;
    }

    private struct CMUGunTargetQuery(GunSystem gun, IEye eye, MapCoordinates coordinates)
    {
        public readonly GunSystem Gun = gun;
        public readonly IEye Eye = eye;
        public readonly MapCoordinates Coordinates = coordinates;
        public bool Found;
        public (EntityUid Uid, bool Alive, bool Occluded, int Depth, uint RenderOrder, float Bottom, float Distance) Best;
    }
}
