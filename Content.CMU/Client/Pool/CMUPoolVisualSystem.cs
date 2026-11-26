using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CMU14.Pool;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Pool;

public sealed partial class CMUPoolVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const double SampleSeconds = 0.05;
    private static readonly Vector2 FeltMin = new(-1.25f, -0.34375f);
    private static readonly Vector2 FeltSize = new(2.5f, 0.875f);
    private static readonly Vector2 BallScale = new(0.5f);
    private static readonly ResPath Art = new("CMU14/Structures/Furniture/pool_balls.rsi");
    private static readonly string[] BallLayers =
    [
        "cmu-pool-0", "cmu-pool-1", "cmu-pool-2", "cmu-pool-3", "cmu-pool-4", "cmu-pool-5",
        "cmu-pool-6", "cmu-pool-7", "cmu-pool-8", "cmu-pool-9", "cmu-pool-10", "cmu-pool-11",
        "cmu-pool-12", "cmu-pool-13", "cmu-pool-14", "cmu-pool-15",
    ];
    private readonly Dictionary<EntityUid, View> _views = new();

    internal sealed class View
    {
        public Vector2[] Target = Array.Empty<Vector2>();
        public readonly Vector2[] Previous = new Vector2[CMUPoolSimulation.BallCount];
        public readonly Vector2[] Positions = new Vector2[CMUPoolSimulation.BallCount];
        public int PocketedMask;
        public bool Rolling;
        public int Revision;
        public double Since;
        public float Fraction = -1;
        public bool LayersCurrent;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPoolTableComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<CMUPoolTableComponent> ent, ref ComponentShutdown args)
    {
        _views.Remove(ent.Owner);
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        for (var i = BallLayers.Length - 1; i >= 0; i--)
            _sprites.RemoveLayer((ent.Owner, sprite), BallLayers[i], logMissing: false);
    }

    internal View? GetView(EntityUid uid)
    {
        if (!TryComp<CMUPoolTableComponent>(uid, out var table) || table.Positions.Length != CMUPoolSimulation.BallCount)
            return null;

        if (!_views.TryGetValue(uid, out var view))
        {
            view = new View();
            _views.Add(uid, view);
        }

        var now = _timing.RealTime.TotalSeconds;
        if (!ReferenceEquals(view.Target, table.Positions))
        {

            if (view.Rolling && table.Rolling && view.Revision == table.Revision)
                view.Positions.CopyTo(view.Previous, 0);
            else
                table.Positions.CopyTo(view.Previous, 0);

            view.Target = table.Positions;
            view.Fraction = -1;
            view.Since = now;
            view.Rolling = table.Rolling;
            view.Revision = table.Revision;
            view.PocketedMask = table.PocketedMask;
        }

        var fraction = view.Rolling ? (float) Math.Clamp((now - view.Since) / SampleSeconds, 0, 1) : 1;
        if (view.Fraction == fraction)
            return view;

        view.Fraction = fraction;
        view.LayersCurrent = false;
        for (var i = 0; i < view.Positions.Length; i++)
            view.Positions[i] = Vector2.Lerp(view.Previous[i], view.Target[i], fraction);

        return view;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var query = EntityQueryEnumerator<CMUPoolTableComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var table, out var sprite))
        {
            if (!sprite.Visible || table.Positions.Length != CMUPoolSimulation.BallCount)
                continue;
            var view = GetView(uid);
            if (view == null || view.LayersCurrent)
                continue;

            UpdateLayers((uid, sprite), view);
            view.LayersCurrent = true;
        }
    }

    private void UpdateLayers(Entity<SpriteComponent?> sprite, View view)
    {
        for (var i = 0; i < view.Positions.Length; i++)
        {
            var visible = (view.PocketedMask & (1 << i)) == 0;
            if (!_sprites.LayerMapTryGet(sprite, BallLayers[i], out var layer, false))
            {
                if (!visible)
                    continue;
                layer = _sprites.AddRsiLayer(sprite, new RSI.StateId($"ball-{i}"), Art);
                _sprites.LayerMapSet(sprite, BallLayers[i], layer);
                _sprites.LayerSetScale(sprite, layer, BallScale);
            }

            _sprites.LayerSetVisible(sprite, layer, visible);
            if (!visible)
                continue;

            var normalized = new Vector2(view.Positions[i].X / CMUPoolSimulation.Width,
                1 - view.Positions[i].Y / CMUPoolSimulation.Height);
            _sprites.LayerSetOffset(sprite, layer, FeltMin + normalized * FeltSize);
        }
    }
}
