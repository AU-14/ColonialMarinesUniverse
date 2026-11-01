using System.Numerics;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client.CMU14.Xenomorphs.Swarm;

public sealed class CMUSwarmFlagOverlay(IEntityManager entities, IPlayerManager players) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (players.LocalEntity is not { } viewer || !entities.HasComponent<XenoComponent>(viewer) ||
            entities.System<SharedXenoHiveSystem>().GetHive(viewer) is not { } hive)
            return;
        var transform = entities.System<SharedTransformSystem>();
        var query = entities.EntityQueryEnumerator<CMUSwarmOrderFlagComponent, TransformComponent>();
        while (query.MoveNext(out var flag, out var xform))
        {
            if (flag.Hive != hive.Owner || xform.MapID != args.MapId)
                continue;
            var point = transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Enlarged(11).Contains(point))
                continue;
            var handle = args.WorldHandle;
            var color = flag.Kind switch
            {
                CMUSwarmOrderKind.Attack => Color.OrangeRed,
                CMUSwarmOrderKind.Defend => Color.Cyan,
                CMUSwarmOrderKind.Rally => Color.Yellow,
                CMUSwarmOrderKind.Build => Color.Orange,
                CMUSwarmOrderKind.Weed => Color.LimeGreen,
                _ => Color.LightGreen,
            };
            handle.DrawLine(point, point + new Vector2(0, 1), color);
            handle.DrawRect(new Box2(point + new Vector2(0, 0.5f), point + new Vector2(0.65f, 1)), color, false);
            if (flag.Kind == CMUSwarmOrderKind.Attack)
            {
                handle.DrawLine(point + new Vector2(-0.3f, -0.3f), point + new Vector2(0.3f, 0.3f), color);
                handle.DrawLine(point + new Vector2(-0.3f, 0.3f), point + new Vector2(0.3f, -0.3f), color);
            }
            else
                handle.DrawCircle(point, flag.Kind == CMUSwarmOrderKind.Defend ? 0.8f : 0.3f, color, false);
            if (flag.Kind == CMUSwarmOrderKind.Roam)
                handle.DrawRect(new Box2(point - new Vector2(10.5f, 7.5f), point + new Vector2(10.5f, 7.5f)), color.WithAlpha(0.3f), false);
        }
    }
}
