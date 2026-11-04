using System.Numerics;
using Content.Client._RMC14.Interaction;
using Content.Client.Clickable;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Client.Gameplay;

public partial class GameplayStateBase
{
    private EntityUid? CMUGetTopmostClickable(MapCoordinates coordinates, IEye eye)
    {
        var state = new CMUClickQuery(
            _entityManager.System<ClickableSystem>(),
            _entityManager.System<RMCClientInteractionSystem>(),
            _playerManager.LocalEntity,
            coordinates.Position,
            eye,
            _comparer);

        _entityManager.System<SpriteTreeSystem>().QueryAabb(
            ref state,
            static (ref CMUClickQuery query, in ComponentTreeEntry<SpriteComponent> entry) =>
            {
                if (query.Interaction.IsInteractionTransparency(entry.Uid, query.Viewer, query.Eye) ||
                    !query.Clickable.CheckClick((entry.Uid, null, entry.Component, entry.Transform),
                        query.Position, query.Eye, true, out var depth, out var order, out var bottom))
                {
                    return true;
                }

                var hit = (entry.Uid, depth, order, bottom);
                if (!query.Found || query.Comparer.Compare(hit, query.Best) < 0)
                {
                    query.Found = true;
                    query.Best = hit;
                }

                // Tree order is unrelated to sprite draw order, so every candidate must be checked.
                return true;
            },
            coordinates.MapId,
            Box2.CenteredAround(coordinates.Position, Vector2.One));

        return state.Found ? state.Best.Uid : null;
    }

    // Query-local state keeps nested picks independent without retaining entity/component references.
    private struct CMUClickQuery(
        ClickableSystem clickable,
        RMCClientInteractionSystem interaction,
        EntityUid? viewer,
        Vector2 position,
        IEye eye,
        ClickableEntityComparer comparer)
    {
        public readonly ClickableSystem Clickable = clickable;
        public readonly RMCClientInteractionSystem Interaction = interaction;
        public readonly EntityUid? Viewer = viewer;
        public readonly Vector2 Position = position;
        public readonly IEye Eye = eye;
        public readonly ClickableEntityComparer Comparer = comparer;
        public bool Found;
        public (EntityUid Uid, int Depth, uint Order, float Bottom) Best;
    }
}
