using System.Numerics;
using Content.Shared.CMU14.WeedKiller;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.WeedKiller;

public sealed partial class CMUWeedKillerSmokeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly EntProtoId SmokePrototype = "RMCGasWeedKiller";
    private readonly List<SmokeBatch> _smoke = new();
    private readonly List<EntityUid> _effects = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUWeedKillerSmokeEvent>(OnSmoke);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearSmoke());
    }

    public override void Shutdown()
    {
        ClearSmoke();
        base.Shutdown();
    }

    private void OnSmoke(CMUWeedKillerSmokeEvent ev)
    {
        if (ev.StartedAt + CMUWeedKillerSmokeEvent.Lifetime > _timing.CurTime)
            _smoke.Add(new SmokeBatch(ev));
    }

    public override void FrameUpdate(float frameTime)
    {
        for (var i = _effects.Count - 1; i >= 0; i--)
        {
            if (Deleted(_effects[i]))
                _effects.RemoveAt(i);
        }
        var eye = _eye.CurrentEye.Position;
        // Keep the existing sprite renderer (including 3D and FOV), and only create nearby cosmetics.
        var range = _config.GetCVar(CVars.NetMaxUpdateRange);
        var bounds = Box2.CenteredAround(eye.Position, new Vector2(range * 2));
        var remaining = 32;
        for (var i = _smoke.Count - 1; i >= 0; i--)
        {
            var batch = _smoke[i];
            var smoke = batch.Event;
            var lifetime = (smoke.StartedAt + CMUWeedKillerSmokeEvent.Lifetime - _timing.CurTime).TotalSeconds;
            if (lifetime <= 0)
            {
                _smoke.RemoveAt(i);
                continue;
            }

            if (!TryGetEntity(smoke.Grid, out var grid)
                || !TryComp(grid, out TransformComponent? transform)
                || !TryComp(grid, out MapGridComponent? mapGrid)
                || transform.MapID != eye.MapId)
                continue;

            var matrix = _transform.GetWorldMatrix(transform);
            for (var tileIndex = 0; tileIndex < smoke.Tiles.Length && remaining > 0; tileIndex++)
            {
                if (batch.Spawned[tileIndex])
                    continue;
                var local = (Vector2) smoke.Tiles[tileIndex] * mapGrid.TileSize + mapGrid.TileSizeHalfVector;
                if (!bounds.Contains(Vector2.Transform(local, matrix)))
                    continue;

                var effect = Spawn(SmokePrototype, new EntityCoordinates(grid.Value, local));
                Comp<TimedDespawnComponent>(effect).Lifetime = (float) lifetime;
                _effects.Add(effect);
                batch.Spawned[tileIndex] = true;
                remaining--;
            }
        }
    }

    private void ClearSmoke()
    {
        _smoke.Clear();
        foreach (var effect in _effects)
            QueueDel(effect);
        _effects.Clear();
    }

    private sealed class SmokeBatch(CMUWeedKillerSmokeEvent ev)
    {
        public readonly CMUWeedKillerSmokeEvent Event = ev;
        public readonly bool[] Spawned = new bool[ev.Tiles.Length];
    }
}
