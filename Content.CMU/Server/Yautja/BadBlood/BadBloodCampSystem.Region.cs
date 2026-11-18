using System.Numerics;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.ZLevels.Core.Components;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 FLOW-606, the Lodge region: once the marines have landed, the coarse square the Lodge stands in
/// (<see cref="BadBloodCampRuleComponent.LodgeRegionSize"/> tiles a side, on the rangefinder grid the Totem read uses)
/// is boxed on the marine tactical map and ARES names it once. It is redrawn when a Lodge is pitched or ends up in
/// another square, and removed when the Lodge falls. The box is not the Lodge's blip: <see cref="IsLodgeRevealed"/>
/// and the reveal components are left to the core.
/// </summary>
public sealed partial class BadBloodCampSystem
{
    /// <summary>
    /// The colour of the Lodge region's box. Tests find the box in the map's shared lines by it.
    /// </summary>
    public static readonly Color LodgeRegionColor = Color.FromHex("#B3261E");

    [Dependency] private SharedMarineAnnounceSystem _regionAnnounce = default!;
    [Dependency] private BadBloodTotemIntelSystem _regionIntel = default!;
    [Dependency] private TacticalMapSystem _regionTacMap = default!;

    /// <summary>
    /// How many times ARES has named a Lodge region to the marines. Instrumentation for the tests.
    /// </summary>
    public int LodgeRegionAnnounced { get; private set; }

    partial void InitializeRegion()
    {
        SubscribeLocalEvent<BadBloodMarinesLandedEvent>(OnRegionMarinesLanded);
    }

    private void OnRegionMarinesLanded(ref BadBloodMarinesLandedEvent ev)
    {
        RefreshLodgeRegion(ev.Rule);
    }

    // catches a Lodge that has been moved into another square
    partial void TickRegion(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        RefreshLodgeRegion(rule);
    }

    partial void LodgePitchedRegion(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule, bool first)
    {
        RefreshLodgeRegion(rule);
    }

    partial void LodgeDestroyedRegion(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule)
    {
        ClearLodgeRegion(rule);
    }

    /// <summary>
    /// Boxes the standing Lodge's square on the marine map once the marines have landed, and names it over ARES,
    /// unless that square is already shown. Clears the box when there is no Lodge.
    /// </summary>
    private void RefreshLodgeRegion(Entity<BadBloodCampRuleComponent> rule)
    {
        if (!rule.Comp.MarinesLanded)
            return;

        var size = Math.Max(1, rule.Comp.LodgeRegionSize);
        if (!TryGetLodge(out var lodge) || !_regionIntel.TryGetLodgeSquare(size, out var corner, out var lodgeTile))
        {
            ClearLodgeRegion(rule);
            return;
        }

        if (rule.Comp.LodgeRegionCorner == corner)
            return;

        ClearLodgeRegion(rule);
        rule.Comp.LodgeRegionCorner = corner;

        if (TryGetLodgeRegionCanvas(lodge.Owner, corner - lodgeTile, size, out var map, out var canvas, out var depth, out var half))
        {
            _regionTacMap.DrawTacticalMapRectangle(map, LodgeRegionColor, canvas, half, half, depth: depth);
            rule.Comp.LodgeRegionMap = map.Owner;
            rule.Comp.LodgeRegionCanvas = canvas;
            rule.Comp.LodgeRegionHalfSize = half;
            rule.Comp.LodgeRegionDepth = depth;
        }

        LodgeRegionAnnounced++;
        _regionAnnounce.AnnounceToMarines(Loc.GetString("cmu-bad-blood-lodge-region-marines",
            ("x1", corner.X),
            ("x2", corner.X + size - 1),
            ("y1", corner.Y),
            ("y2", corner.Y + size - 1)));
    }

    /// <summary>
    /// Removes the drawn box, by the colour and geometry it was drawn with, and forgets the shown square.
    /// </summary>
    private void ClearLodgeRegion(Entity<BadBloodCampRuleComponent> rule)
    {
        if (rule.Comp.LodgeRegionMap is { } mapUid && TryComp(mapUid, out TacticalMapComponent? map))
        {
            _regionTacMap.RemoveTacticalMapRectangle((mapUid, map),
                LodgeRegionColor,
                rule.Comp.LodgeRegionCanvas,
                rule.Comp.LodgeRegionHalfSize,
                rule.Comp.LodgeRegionHalfSize,
                depth: rule.Comp.LodgeRegionDepth);
        }

        rule.Comp.LodgeRegionMap = null;
        rule.Comp.LodgeRegionCorner = null;
    }

    // the canvas lookup from ObjHotspotSystem.TryGetZoneCanvas: the tracking map of the Lodge's grid, its area grid's
    // tile bounds, and the classic canvas at three pixels per tile. squareFromLodge is the square's lowest tile less
    // the Lodge's own tile, so the square lands on the Lodge's grid whatever offset the rangefinder grid has
    private bool TryGetLodgeRegionCanvas(EntityUid lodge, Vector2i squareFromLodge, int size,
        out Entity<TacticalMapComponent> map, out Vector2 canvas, out int depth, out float half)
    {
        map = default;
        canvas = default;
        depth = 0;
        half = 0;

        var xform = Transform(lodge);
        if (xform.GridUid is not { } grid || !_regionTacMap.TryGetTrackingMap(grid, out map))
            return false;

        if (!TryComp<AreaGridComponent>(map.Owner, out var areas) || areas.Colors.Count == 0)
            return false;

        var min = Vector2i.Zero;
        var max = Vector2i.Zero;
        foreach (var tile in areas.Colors.Keys)
        {
            min = Vector2i.ComponentMin(min, tile);
            max = Vector2i.ComponentMax(max, tile);
        }

        // the square's lowest tile edge plus half a side is its centre, so the box's edges land on tile boundaries
        var lowest = _transform.GetGridOrMapTilePosition(lodge, xform) + squareFromLodge;
        canvas = CMUReconDrawingCoordinates.ToCanvas(new Vector2(lowest.X + size / 2f, lowest.Y + size / 2f), min, max);
        if (TryComp<CMUZLevelMapComponent>(grid, out var zLevel))
            depth = zLevel.Depth;

        half = size * 1.5f;
        return true;
    }
}
