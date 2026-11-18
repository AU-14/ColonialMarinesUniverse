using System.Numerics;
using Content.Shared.CMU14.BuildOrders;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.Ghost.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.BuildOrders;

/// <summary>
///     Draws the commander's two quick calls in the world: a pulsing ring around whoever they highlighted, and an arrow
///     pointing the way when they order a push.
/// </summary>
public sealed class CMUCommandMarkOverlay(
    IEntityManager entities,
    IPlayerManager players,
    IGameTiming timing,
    ForceOnForceSystem factions) : Overlay
{
    private static readonly Color RingColor = new(1f, 0.25f, 0.25f, 1f);
    private static readonly Color ArrowColor = new(1f, 0.8f, 0.2f, 1f);

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var transforms = entities.System<SharedTransformSystem>();
        var local = players.LocalEntity;
        var faction = local == null ? null : factions.GetFaction(local.Value);
        var observer = local == null || entities.HasComponent<GhostComponent>(local.Value);
        var pulse = 0.5f + 0.5f * MathF.Sin((float) timing.RealTime.TotalSeconds * 8f);

        // Highlighted people, for the commander's side only.
        var marked = entities.EntityQueryEnumerator<CMUMarkedComponent, TransformComponent>();
        while (marked.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != args.MapId ||
                (!observer && !string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var position = transforms.GetWorldPosition(xform);
            var radius = 0.75f + 0.15f * pulse;
            handle.DrawCircle(position, radius, RingColor.WithAlpha(0.9f), false);
            handle.DrawCircle(position, radius * 0.85f, RingColor.WithAlpha(0.5f), false);
            handle.DrawCircle(position, radius * 0.7f, RingColor.WithAlpha(0.18f + 0.12f * pulse));
        }

        // The push arrow, ahead of the player in the ordered direction.
        if (local is not { } player ||
            !entities.TryGetComponent(player, out CMUPushArrowComponent? arrow) ||
            !entities.TryGetComponent(player, out TransformComponent? playerXform) ||
            playerXform.MapID != args.MapId)
        {
            return;
        }

        var direction = arrow.Direction switch
        {
            CMUPushDirection.North => new Vector2(0f, 1f),
            CMUPushDirection.East => new Vector2(1f, 0f),
            CMUPushDirection.South => new Vector2(0f, -1f),
            _ => new Vector2(-1f, 0f),
        };

        var origin = transforms.GetWorldPosition(playerXform);
        var tail = origin + direction * 1.8f;
        var tip = origin + direction * (3.4f + 0.25f * pulse);
        var side = new Vector2(-direction.Y, direction.X);
        var color = ArrowColor.WithAlpha(0.65f + 0.35f * pulse);

        // Drawn a few times side by side so the line is thick enough to read.
        for (var i = -2; i <= 2; i++)
        {
            var offset = side * (i * 0.04f);
            handle.DrawLine(tail + offset, tip + offset, color);
            handle.DrawLine(tip - direction * 0.7f + side * 0.5f + offset, tip + offset, color);
            handle.DrawLine(tip - direction * 0.7f - side * 0.5f + offset, tip + offset, color);
        }
    }
}
