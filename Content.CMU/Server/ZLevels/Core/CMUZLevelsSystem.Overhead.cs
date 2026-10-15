using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Server.GameStates;
using Robust.Shared;
using Robust.Shared.Player;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    [Dependency] private ISharedPlayerManager _overheadPvsPlayers = default!;

    private const float OverheadPvsCellSize = 32f;
    private const int OverheadPoolLimit = 64;
    private const int OverheadMemberLimit = 1024;
    private readonly Dictionary<(EntityUid Network, Vector2i Cell), List<(EntityUid Uid, Vector2 Position)>> _overheadPvsCells = new();
    private readonly Stack<List<(EntityUid Uid, Vector2 Position)>> _overheadPvsCellPool = new();
    private readonly Dictionary<EntityUid, List<EntityUid>> _overheadPvsViews = new();
    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _overheadPvsSessions = new();
    private readonly Stack<List<EntityUid>> _overheadPvsViewPool = new();
    private readonly Stack<HashSet<EntityUid>> _overheadPvsSessionPool = new();
    private readonly Dictionary<(EntityUid Entity, EntityUid Map), bool> _overheadPvsProjections = new();

    public int LastOverheadPvsCandidateChecks { get; private set; }

    public readonly record struct PvsStorageUsage(int ActiveCells, int ActiveViews, int ActiveSessions,
        int PooledBuffers, int PooledMemberCapacity, int DictionaryCapacity);

    public PvsStorageUsage GetPvsStorageUsage()
    {
        var members = 0;
        foreach (var list in _overheadPvsCellPool) members += list.Capacity;
        foreach (var list in _overheadPvsViewPool) members += list.Capacity;
        foreach (var set in _overheadPvsSessionPool) members += set.EnsureCapacity(0);
        return new(_overheadPvsCells.Count, _overheadPvsViews.Count, _overheadPvsSessions.Count,
            _overheadPvsCellPool.Count + _overheadPvsViewPool.Count + _overheadPvsSessionPool.Count,
            members, _overheadPvsCells.EnsureCapacity(0) + _overheadPvsViews.EnsureCapacity(0) +
                     _overheadPvsSessions.EnsureCapacity(0) + _overheadPvsProjections.EnsureCapacity(0));
    }

    public void PrepareOverheadPvs()
    {
        TrimPvsDictionaryAfterPeak(_overheadPvsCells);
        TrimPvsDictionaryAfterPeak(_overheadPvsViews);
        TrimPvsDictionaryAfterPeak(_overheadPvsSessions);
        TrimPvsDictionaryAfterPeak(_overheadPvsProjections);
        foreach (var list in _overheadPvsCells.Values)
        {
            list.Clear();
            if (list.Capacity <= OverheadMemberLimit && _overheadPvsCellPool.Count < OverheadPoolLimit)
                _overheadPvsCellPool.Push(list);
        }
        _overheadPvsCells.Clear();
        foreach (var list in _overheadPvsViews.Values)
        {
            list.Clear();
            if (list.Capacity <= OverheadMemberLimit && _overheadPvsViewPool.Count < OverheadPoolLimit)
                _overheadPvsViewPool.Push(list);
        }
        _overheadPvsViews.Clear();
        foreach (var set in _overheadPvsSessions.Values)
        {
            set.Clear();
            ReturnOverheadSession(set);
        }
        _overheadPvsSessions.Clear();
        _overheadPvsProjections.Clear();
        LastOverheadPvsCandidateChecks = 0;
        if (!_zLevelsEnabled)
            return;
        using var profile = Prof.Group("CMU Z Overhead PVS Prepare");
        var query = EntityQueryEnumerator<CMUZFallingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid is not { } map || !TryGetZNetwork(map, out var network) || TerminatingOrDeleted(uid))
                continue;
            var position = _transform.GetWorldPosition(xform);
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
                continue;
            var key = (network.Value.Owner, OverheadPvsCell(position));
            if (!_overheadPvsCells.TryGetValue(key, out var cell))
            {
                cell = _overheadPvsCellPool.TryPop(out var pooled) ? pooled : new();
                _overheadPvsCells.Add(key, cell);
            }
            cell.Add((uid, position));
        }

        if (_overheadPvsCells.Count == 0)
            return;
        var range = _config.GetCVar(CVars.NetMaxUpdateRange);
        foreach (var session in _overheadPvsPlayers.Sessions)
        {
            var selected = _overheadPvsSessionPool.TryPop(out var pooled) ? pooled : new();
            if (session.AttachedEntity is { } attached)
                AddOverheadPvsView(attached, range, selected);
            foreach (var view in session.ViewSubscriptions)
                AddOverheadPvsView(view, range, selected);
            if (selected.Count > 0)
                _overheadPvsSessions.Add(session, selected);
            else
                ReturnOverheadSession(selected);
        }
        if (Prof.IsEnabled)
            Prof.WriteValue("CMU Z Overhead PVS Candidates", LastOverheadPvsCandidateChecks);
    }

    private void AddOverheadPvsView(EntityUid view, float range, HashSet<EntityUid> selected)
    {
        // Warm probes are not real viewpoints and must never reveal entities through a roof.
        if (IsZLevelProbe(view) || TerminatingOrDeleted(view) ||
            !TryComp(view, out TransformComponent? xform) || xform.MapUid is not { } map ||
            !TryGetZNetwork(map, out var network))
            return;
        if (_overheadPvsViews.TryGetValue(view, out var cached))
        {
            selected.UnionWith(cached);
            return;
        }

        var visible = _overheadPvsViewPool.TryPop(out var pooled) ? pooled : new();
        _overheadPvsViews.Add(view, visible);
        if (TryComp<EyeComponent>(view, out var eye))
            range *= eye.PvsScale;
        var center = _transform.GetWorldPosition(xform);
        if (!float.IsFinite(range) || range < 0 || !float.IsFinite(center.X) || !float.IsFinite(center.Y))
            return;
        var start = OverheadPvsCell(center - new Vector2(range));
        var end = OverheadPvsCell(center + new Vector2(range));
        // Huge admin camera ranges should cost at most the number of occupied cells, not range squared.
        var cellCount = ((double) end.X - start.X + 1) * ((double) end.Y - start.Y + 1);
        if (cellCount > _overheadPvsCells.Count)
        {
            foreach (var (key, cell) in _overheadPvsCells)
            {
                if (key.Network == network.Value.Owner && key.Cell.X >= start.X && key.Cell.X <= end.X &&
                    key.Cell.Y >= start.Y && key.Cell.Y <= end.Y)
                    SelectOverheadPvsCell(cell, map, center, range, visible);
            }
        }
        else
        {
            for (var x = (long) start.X; x <= end.X; x++)
            for (var y = (long) start.Y; y <= end.Y; y++)
            {
                if (_overheadPvsCells.TryGetValue((network.Value.Owner, new Vector2i((int) x, (int) y)), out var cell))
                    SelectOverheadPvsCell(cell, map, center, range, visible);
            }
        }
        selected.UnionWith(visible);
    }

    private void SelectOverheadPvsCell(List<(EntityUid Uid, Vector2 Position)> cell, EntityUid map,
        Vector2 center, float range, List<EntityUid> visible)
    {
        foreach (var (uid, position) in cell)
        {
            LastOverheadPvsCandidateChecks++;
            var delta = Vector2.Abs(position - center);
            if (delta.X > range || delta.Y > range)
                continue;
            var key = (uid, map);
            if (!_overheadPvsProjections.TryGetValue(key, out var projects))
            {
                projects = TryGetOverheadEntityProjection(uid, map, out _, out _);
                _overheadPvsProjections.Add(key, projects);
            }
            if (projects)
                visible.Add(uid);
        }
    }

    private static Vector2i OverheadPvsCell(Vector2 position) => new(
        (int) Math.Clamp(Math.Floor((double) position.X / OverheadPvsCellSize), int.MinValue, int.MaxValue),
        (int) Math.Clamp(Math.Floor((double) position.Y / OverheadPvsCellSize), int.MinValue, int.MaxValue));

    private void ReturnOverheadSession(HashSet<EntityUid> set)
    {
        if (set.EnsureCapacity(0) <= OverheadMemberLimit && _overheadPvsSessionPool.Count < OverheadPoolLimit)
            _overheadPvsSessionPool.Push(set);
    }

    private static void TrimPvsDictionaryAfterPeak<TKey, TValue>(Dictionary<TKey, TValue> entries) where TKey : notnull
    {
        // Retain steady-state working capacity. Shrink only after a substantial decline,
        // before Clear loses the previous publication's active population count.
        var capacity = entries.EnsureCapacity(0);
        if (capacity > 4096 && entries.Count < capacity / 4) entries.TrimExcess();
    }

    private void ClearOverheadPvsStorage()
    {
        _overheadPvsCells.Clear();
        _overheadPvsCells.TrimExcess();
        _overheadPvsViews.Clear();
        _overheadPvsViews.TrimExcess();
        _overheadPvsSessions.Clear();
        _overheadPvsSessions.TrimExcess();
        _overheadPvsProjections.Clear();
        _overheadPvsProjections.TrimExcess();
        _overheadPvsCellPool.Clear();
        _overheadPvsCellPool.TrimExcess();
        _overheadPvsViewPool.Clear();
        _overheadPvsViewPool.TrimExcess();
        _overheadPvsSessionPool.Clear();
        _overheadPvsSessionPool.TrimExcess();
    }

    private void OnExpandOverheadEntityPvs(ref ExpandPvsEvent args)
    {
        if (!_zLevelsEnabled)
            return;
        if (_overheadPvsSessions.TryGetValue(args.Session, out var selected))
            (args.Entities ??= new List<EntityUid>(selected.Count)).AddRange(selected);
    }
}
