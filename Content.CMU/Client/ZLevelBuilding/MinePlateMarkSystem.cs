// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ZLevelBuilding;

// The plates over the pits look like the floor to everybody but the CLF: they see an exclamation mark over each tile that has one. (The plates are the same for friend and foe, and
// whoever steps on one falls in; the CLF only know where they are.)
public sealed class MinePlateMarkSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private MinePlateMarkOverlay? _overlay;

    public override void Shutdown()
    {
        base.Shutdown();
        Hide();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is { } player && HasComp<CLFMemberComponent>(player))
        {
            if (_overlay == null)
            {
                _overlay = new MinePlateMarkOverlay();
                _overlays.AddOverlay(_overlay);
            }

            return;
        }

        Hide();
    }

    private void Hide()
    {
        if (_overlay == null)
            return;

        _overlays.RemoveOverlay(_overlay);
        _overlay = null;
    }
}

// An exclamation mark above the middle of each tile with a plate: a bar and a dot, red with a dark edge, that pulses a little so that it is not lost on the ground.
public sealed partial class MinePlateMarkOverlay : Overlay
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly SharedTransformSystem _transform;

    public MinePlateMarkOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity == null)
            return;

        var pulse = 0.75f + 0.25f * MathF.Sin((float) _timing.CurTime.TotalSeconds * 5f);
        var fill = new Color(0.95f, 0.1f, 0.1f, pulse);
        var edge = new Color(0.1f, 0f, 0f, pulse);
        var handle = args.WorldHandle;

        var query = _entities.EntityQueryEnumerator<MinePlateComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var at = _transform.GetWorldPosition(xform);
            if (!args.WorldBounds.Contains(at))
                continue;

            // The mark stands over the tile, a little above the middle.
            var bar = Box2.CenteredAround(at + new Vector2(0f, 0.22f), new Vector2(0.14f, 0.34f));
            var dot = Box2.CenteredAround(at + new Vector2(0f, -0.08f), new Vector2(0.14f, 0.14f));

            handle.DrawRect(bar.Enlarged(0.03f), edge);
            handle.DrawRect(dot.Enlarged(0.03f), edge);
            handle.DrawRect(bar, fill);
            handle.DrawRect(dot, fill);
        }
    }
}
