using System.Numerics;
using Content.Shared.CMU14.Weapons.ATGM;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Weapons.ATGM;

/// <summary>
///     Draws a loose, sagging "telephone wire" from each guided missile's launcher (or its spawn
///     point) to the missile. The wire exists exactly as long as the missile component, which the
///     server removes on detonation.
/// </summary>
/// <remarks>
///     Each missile also gets a client-side breadcrumb trail so the wire roughly follows the path the
///     missile actually flew instead of cutting straight across its turns. All buffers are reused;
///     the only allocations are one pooled <see cref="Trail"/> per missile.
/// </remarks>
public sealed class CMUATGMWireOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private static readonly Color WireColor = new(0.17f, 0.17f, 0.17f, 0.85f);

    // Wire resolution and shape.
    private const int Segments = 24;
    private const float SagPerTile = 0.08f;
    private const float MaxSag = 1.5f;
    private const float SwayFraction = 0.12f;
    private const float SwaySpeed = 0.9f;

    // Second stroke offset so the 1px line strip reads as a ~2px wire.
    private const float StrokeOffset = 0.03f;

    // Breadcrumbs: when the trail fills up it is decimated and the spacing doubles, so long flights
    // keep a bounded, evenly spread set of points.
    private const int MaxCrumbs = 16;
    private const float InitialCrumbSpacing = 0.75f;

    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;

    private readonly Dictionary<EntityUid, Trail> _trails = new();
    private readonly Stack<Trail> _trailPool = new();
    private readonly List<EntityUid> _staleTrails = new();

    // Control polyline: start + crumbs + missile.
    private readonly Vector2[] _control = new Vector2[MaxCrumbs + 2];
    private readonly float[] _controlDist = new float[MaxCrumbs + 2];
    private readonly Vector2[] _wire = new Vector2[Segments + 1];
    private readonly Vector2[] _wireStroke = new Vector2[Segments + 1];

    private int _drawStamp;

    public CMUATGMWireOverlay(IEntityManager entManager, IGameTiming timing, SharedTransformSystem transform)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = transform;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        var stamp = ++_drawStamp;

        // "Down" on screen, expressed in world space, so the wire hangs the same way whatever the eye rotation.
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var sagDir = (-eyeRotation).RotateVec(new Vector2(0f, -1f));
        var time = (float) _timing.RealTime.TotalSeconds;

        var query = _entManager.EntityQueryEnumerator<CMUGuidedMissileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var missile, out var xform))
        {
            if (xform.MapID == MapId.Nullspace)
                continue;

            var missilePos = _transform.GetWorldPosition(xform, xformQuery);

            // Crumbs are recorded for missiles on every map, so drawing another viewport's map
            // neither prunes nor starves them.
            var trail = GetTrail(uid, xform.MapID);
            trail.Stamp = stamp;
            trail.Record(missilePos);

            if (xform.MapID != args.MapId)
                continue;

            // Prefer the live launcher; it can be outside PVS or gone, in which case use the spawn point.
            Vector2 start;
            if (missile.Launcher is { } launcher
                && xformQuery.TryGetComponent(launcher, out var launcherXform)
                && launcherXform.MapID == args.MapId)
            {
                start = _transform.GetWorldPosition(launcherXform, xformQuery);
            }
            else if (missile.WireOrigin.MapId == args.MapId)
            {
                start = missile.WireOrigin.Position;
            }
            else
            {
                continue;
            }

            var count = BuildControl(start, trail, missilePos);
            var chord = Vector2.Distance(start, missilePos);
            if (chord < 0.05f)
                continue;

            // Slow per-missile sway so separate wires do not move in lockstep.
            var phase = time * SwaySpeed + uid.Id * 1.7f;
            var sag = MathF.Min(chord * SagPerTile, MaxSag) * (1f + SwayFraction * MathF.Sin(phase));

            if (!BuildWire(count, sagDir, sag, out var bounds))
                continue;

            if (!bounds.Enlarged(StrokeOffset).Intersects(args.WorldAABB))
                continue;

            var strokeShift = sagDir * StrokeOffset;
            for (var i = 0; i <= Segments; i++)
            {
                _wireStroke[i] = _wire[i] + strokeShift;
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _wire, WireColor);
            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _wireStroke, WireColor);
        }

        PruneTrails(stamp);
    }

    /// <summary>
    ///     Fills <see cref="_control"/> with start, breadcrumbs, and the missile, returning the point count.
    /// </summary>
    private int BuildControl(Vector2 start, Trail trail, Vector2 missilePos)
    {
        var count = 0;
        _control[count++] = start;
        for (var i = 0; i < trail.Count; i++)
        {
            _control[count++] = trail.Points[i];
        }

        _control[count++] = missilePos;
        return count;
    }

    /// <summary>
    ///     Resamples the control polyline evenly by arc length into <see cref="_wire"/> and hangs it with a
    ///     parabolic sag (zero at both ends, deepest mid-span), like a cord strung between two posts.
    /// </summary>
    private bool BuildWire(int count, Vector2 sagDir, float sag, out Box2 bounds)
    {
        var total = 0f;
        _controlDist[0] = 0f;
        for (var i = 1; i < count; i++)
        {
            total += Vector2.Distance(_control[i - 1], _control[i]);
            _controlDist[i] = total;
        }

        bounds = default;
        if (total <= 0f)
            return false;

        var seg = 1;
        for (var i = 0; i <= Segments; i++)
        {
            var t = (float) i / Segments;
            var dist = t * total;
            while (seg < count - 1 && _controlDist[seg] < dist)
            {
                seg++;
            }

            var segStart = _controlDist[seg - 1];
            var segLength = _controlDist[seg] - segStart;
            var local = segLength > 0f ? (dist - segStart) / segLength : 0f;
            var point = Vector2.Lerp(_control[seg - 1], _control[seg], local);

            point += sagDir * (sag * 4f * t * (1f - t));
            _wire[i] = point;

            bounds = i == 0 ? new Box2(point, point) : bounds.ExtendToContain(point);
        }

        return true;
    }

    private Trail GetTrail(EntityUid uid, MapId map)
    {
        if (_trails.TryGetValue(uid, out var trail))
        {
            // A missile changing maps keeps no meaningful path.
            if (trail.Map != map)
                trail.Reset(map);

            return trail;
        }

        trail = _trailPool.Count > 0 ? _trailPool.Pop() : new Trail();
        trail.Reset(map);
        _trails[uid] = trail;
        return trail;
    }

    /// <summary>
    ///     Drops trails whose missile no longer has the component (detonated, deleted, or left PVS).
    /// </summary>
    private void PruneTrails(int stamp)
    {
        _staleTrails.Clear();
        foreach (var (uid, trail) in _trails)
        {
            if (trail.Stamp != stamp)
                _staleTrails.Add(uid);
        }

        foreach (var uid in _staleTrails)
        {
            if (_trails.Remove(uid, out var trail))
                _trailPool.Push(trail);
        }
    }

    /// <summary>
    ///     Client-only record of where one missile has been, oldest first. Excludes the missile's
    ///     current position, which is appended at draw time.
    /// </summary>
    private sealed class Trail
    {
        public readonly Vector2[] Points = new Vector2[MaxCrumbs];
        public int Count;
        public MapId Map;
        public int Stamp;
        public float Spacing;
        public bool HasLast;
        public Vector2 Last;

        public void Reset(MapId map)
        {
            Count = 0;
            Map = map;
            Spacing = InitialCrumbSpacing;
            HasLast = false;
        }

        public void Record(Vector2 position)
        {
            // The first sample is the launch area, which the wire start already covers.
            if (!HasLast)
            {
                HasLast = true;
                Last = position;
                return;
            }

            if (Vector2.DistanceSquared(position, Last) < Spacing * Spacing)
                return;

            Last = position;
            if (Count == MaxCrumbs)
            {
                // Keep every other crumb: bounded memory, still evenly spread along the path.
                var kept = 0;
                for (var i = 1; i < Count; i += 2)
                {
                    Points[kept++] = Points[i];
                }

                Count = kept;
                Spacing *= 2f;
            }

            Points[Count++] = position;
        }
    }
}
