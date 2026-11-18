using System.Numerics;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Physics;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 FLOW-704: where the Clan Leader sends the clan. Rejection sampling in a band around the Lodge, on the Lodge's
/// own map only (planets are multi-Z: nothing is projected across maps). A site is a non-empty, unblocked tile that
/// passes <see cref="BadBloodTotemSystem.GetSiteDenial"/> (open sky, away from the Lodge, Totems and landing zones)
/// and keeps <see cref="BadBloodCampRuleComponent.OrderSpacing"/> from every open order. The best are kept: spread
/// from the clan's other claims, pulled toward the marines after the first order, away from recently ordered areas.
/// No site in the band: the band is widened once, then the picker gives up for this try. It never throws: missing
/// grids, missing chunks and empty tiles are only rejections.
/// </summary>
public sealed partial class BadBloodHuntOrderSystem
{
    private readonly List<MapCoordinates> _candidates = new();
    private readonly List<(MapCoordinates Site, float Score)> _scored = new();

    /// <summary>
    /// Fills <paramref name="sites"/> with up to <paramref name="count"/> sites around <paramref name="center"/>, best
    /// first, in the near band (<paramref name="near"/>) or the far band. False, with the list empty, when none was
    /// found even in the widened band.
    /// </summary>
    public bool TryPickSites(
        Entity<BadBloodCampRuleComponent> rule,
        MapCoordinates center,
        bool near,
        List<MapCoordinates> sites,
        int count = 2)
    {
        sites.Clear();
        _candidates.Clear();
        _scored.Clear();
        if (count <= 0 || center.MapId == MapId.Nullspace || !_map.MapExists(center.MapId))
            return false;

        var comp = rule.Comp;
        var min = near ? comp.OrderNearMin : comp.OrderFarMin;
        var max = near ? comp.OrderNearMax : comp.OrderFarMax;
        for (var pass = 0; pass < 2 && _candidates.Count == 0; pass++)
        {
            if (pass == 1)
            {
                min = Math.Max(0f, min - comp.OrderBandWiden);
                max += comp.OrderBandWiden;
            }

            Sample(rule, center, min, max);
        }

        if (_candidates.Count == 0)
            return false;

        // weight 0 for the first order, then a quarter more per order up to the full weight
        var pull = near ? 0f : comp.OrderPullWeight * Math.Min(1f, comp.OrdersIssued / 4f);
        var target = pull > 0f ? GetPullTarget(center.MapId) : null;
        var now = _timing.CurTime;
        foreach (var site in _candidates)
        {
            var score = 0f;
            if (GetNearestClaim(rule, site) is { } nearest)
                score += Math.Min(nearest, comp.OrderSpreadCap) * comp.OrderSpreadWeight;

            if (target is { } pullTo)
                score -= (site.Position - pullTo).Length() * pull;

            if (comp.RecentOrderAreas.TryGetValue(GetAreaName(site), out var orderedAt) &&
                now - orderedAt < comp.OrderRepeatAreaWindow)
            {
                score -= comp.OrderRepeatAreaPenalty;
            }

            _scored.Add((site, score));
        }

        _scored.Sort((a, b) => b.Score.CompareTo(a.Score));
        for (var i = 0; i < _scored.Count && sites.Count < count; i++)
        {
            sites.Add(_scored[i].Site);
        }

        return sites.Count > 0;
    }

    private void Sample(Entity<BadBloodCampRuleComponent> rule, MapCoordinates center, float min, float max)
    {
        var comp = rule.Comp;
        var tries = Math.Max(1, comp.OrderSampleTries);
        var wanted = Math.Max(1, comp.OrderCandidates);
        var minSq = min * min;
        var maxSq = Math.Max(max * max, minSq);

        // candidates keep half the spacing between themselves, so the Hunt Leader is offered two real choices
        var apart = comp.OrderSpacing / 2f;
        for (var i = 0; i < tries && _candidates.Count < wanted; i++)
        {
            // uniform over the annulus' area, not its radius
            var angle = _random.NextFloat() * MathF.Tau;
            var radius = MathF.Sqrt(minSq + _random.NextFloat() * (maxSq - minSq));
            var position = center.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

            if (!TryGetOpenTile(center.MapId, position, out var site))
                continue;

            var distance = (site.Position - center.Position).Length();
            if (distance < min || distance > max)
                continue;

            if (IsNearClaim(rule, site, comp.OrderSpacing) || IsNearCandidate(site, apart))
                continue;

            if (_totems.GetSiteDenial(site, comp.OrderSpacing, comp.OrderLandingZoneDistance) != null)
                continue;

            _candidates.Add(site);
        }
    }

    // the centre of a tile that exists, is not empty and is not blocked by an anchored wall or the like
    private bool TryGetOpenTile(MapId map, Vector2 position, out MapCoordinates site)
    {
        site = MapCoordinates.Nullspace;
        if (!_map.TryFindGridAt(map, position, out var gridUid, out var grid))
            return false;

        if (!_map.TryGetTileRef(gridUid, grid, position, out var tile) || tile.Tile.IsEmpty)
            return false;

        if (_turf.IsTileBlocked(gridUid, tile.GridIndices, CollisionGroup.Impassable, grid))
            return false;

        site = _map.GridTileToWorld(gridUid, grid, tile.GridIndices);
        return site.MapId == map;
    }

    // within spacing of an open order or a site offered to the Hunt Leader
    private bool IsNearClaim(Entity<BadBloodCampRuleComponent> rule, MapCoordinates site, float spacing)
    {
        if (spacing <= 0f)
            return false;

        foreach (var ground in rule.Comp.HuntOrders)
        {
            if (TerminatingOrDeleted(ground))
                continue;

            var there = _transform.GetMapCoordinates(ground);
            if (there.MapId == site.MapId && (there.Position - site.Position).Length() < spacing)
                return true;
        }

        if (rule.Comp.PendingPick is { } pick)
        {
            foreach (var offered in pick.Sites)
            {
                if (offered.MapId == site.MapId && (offered.Position - site.Position).Length() < spacing)
                    return true;
            }
        }

        return false;
    }

    private bool IsNearCandidate(MapCoordinates site, float apart)
    {
        foreach (var candidate in _candidates)
        {
            if ((candidate.Position - site.Position).Length() < apart)
                return true;
        }

        return false;
    }

    // distance to the nearest Totem or open order on the same map, null when the clan holds none there
    private float? GetNearestClaim(Entity<BadBloodCampRuleComponent> rule, MapCoordinates site)
    {
        float? nearest = null;
        foreach (var totem in _rule.GetTotems())
        {
            Consider(totem);
        }

        foreach (var ground in rule.Comp.HuntOrders)
        {
            Consider(ground);
        }

        return nearest;

        void Consider(EntityUid uid)
        {
            if (TerminatingOrDeleted(uid))
                return;

            var there = _transform.GetMapCoordinates(uid);
            if (there.MapId != site.MapId)
                return;

            var distance = (there.Position - site.Position).Length();
            if (nearest == null || distance < nearest)
                nearest = distance;
        }
    }

    // the centroid of the living marines on this map, else a landing zone on it, else nothing
    private Vector2? GetPullTarget(MapId map)
    {
        var sum = Vector2.Zero;
        var count = 0;
        var marines = EntityQueryEnumerator<MarineComponent, TransformComponent>();
        while (marines.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != map || !_mobState.IsAlive(uid) || _badBlood.IsBadBlood(uid))
                continue;

            sum += _transform.GetWorldPosition(xform);
            count++;
        }

        if (count > 0)
            return sum / count;

        var zones = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (zones.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == map)
                return _transform.GetWorldPosition(xform);
        }

        return null;
    }

    private string GetAreaName(MapCoordinates site)
    {
        return site.MapId != MapId.Nullspace && _map.MapExists(site.MapId) && _area.TryGetArea(site, out _, out var area)
            ? area.Name
            : Loc.GetString("rmc-tacmap-alert-no-area");
    }
}
