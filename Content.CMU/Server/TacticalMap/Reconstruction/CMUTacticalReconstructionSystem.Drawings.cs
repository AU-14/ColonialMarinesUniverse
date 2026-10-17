using System.Linq;
using System.Numerics;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    private sealed record CanvasBaseline(List<TacticalMapLine> Lines, Dictionary<Vector2i, string> Labels);
    private readonly Dictionary<(EntityUid Network, string Faction), CanvasBaseline> _canvasBaselines = new();

    private void ImportCanvas(Survey survey, EntityUid scope, string faction, List<CMUReconOrder> orders)
    {
        var key = (DrawingScope: scope, Faction: faction);
        if (!EntityManager.System<TacticalMapSystem>().TryReconstructionCanvas(key.DrawingScope, key.Faction, out var lines, out var labels)) return;
        if (_canvasBaselines.TryGetValue(key, out var old) && ReferenceEquals(old.Lines, lines) &&
            old.Labels.Count == labels.Count && old.Labels.All(p => labels.TryGetValue(p.Key, out var text) && text == p.Value)) return;

        var min = Vector2i.Zero;
        var max = Vector2i.Zero;
        if (lines.Any(line => line.WorldPoints == null) && TryComp<AreaGridComponent>(survey.Root, out var areas))
            foreach (var tile in areas.Colors.Keys)
            {
                min = Vector2i.ComponentMin(min, tile);
                max = Vector2i.ComponentMax(max, tile);
            }
        var oldText = new Dictionary<Vector2i, CMUReconOrder>();
        foreach (var order in orders)
            if (order.Text != null) oldText[order.Tile] = order;
        orders.Clear();
        var lineLimit = EntityManager.System<TacticalMapSystem>().LineLimit;
        foreach (var line in lines.Take(Math.Max(0, lineLimit)))
        {
            var points = line.WorldPoints ?? [CMUReconDrawingCoordinates.ToWorld(line.Start, min, max), CMUReconDrawingCoordinates.ToWorld(line.End, min, max)];
            if (points.Length is < 1 or > CMUReconRoutes.MaxPoints || !float.IsFinite(line.Thickness)) continue;
            var depth = Math.Clamp(line.Depth, survey.Atlas.MinDepth, survey.Atlas.MinDepth + survey.Atlas.Maps.Length - 1);
            if (!CMUReconRoutes.ValidStroke(points, survey.Atlas.Origin, survey.Atlas.Width, survey.Atlas.Height, CMUReconInk.Yellow)) continue;
            var last = points[^1];
            orders.Add(new CMUReconOrder(++_orderId, new Vector2i((int) MathF.Floor(last.X), (int) MathF.Floor(last.Y)),
                depth, CMUReconOrderKind.Route, points, Width: Math.Clamp(line.Thickness, 1, 8), Color: line.Color));
        }
        foreach (var (tile, text) in labels.Take(256))
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (oldText.TryGetValue(tile, out var previous) && previous.Text == text)
                orders.Add(previous with { Id = ++_orderId });
            else orders.Add(new CMUReconOrder(++_orderId, tile, Math.Clamp(0, survey.Atlas.MinDepth, survey.Atlas.MinDepth + survey.Atlas.Maps.Length - 1),
                CMUReconOrderKind.Text, [(Vector2) tile], Text: text[..Math.Min(text.Length, CMUReconRoutes.MaxTextLength)]));
        }
        _canvasBaselines[key] = new CanvasBaseline(lines, new Dictionary<Vector2i, string>(labels));
    }

    private void PublishCanvas(Survey survey)
    {
        var key = (survey.DrawingScope, survey.Faction);
        if (!_orders.TryGetValue(key, out var orders)) return;
        var lines = new List<TacticalMapLine>();
        var labels = new Dictionary<Vector2i, string>();
        foreach (var order in orders)
        {
            if (order.Text is { } text) labels[order.Tile] = text;
            else if (order.Waypoints is { Length: > 0 } points)
                lines.Add(new TacticalMapLine(default, default, order.Color ?? CMUReconDrawingCoordinates.InkColor(order.Ink), order.Width, points, order.Depth));
        }
        EntityManager.System<TacticalMapSystem>().SetReconstructionCanvas(survey.Source, survey.Actor, survey.DrawingScope, survey.Faction, lines, labels);
        _canvasBaselines[key] = new CanvasBaseline(lines, new Dictionary<Vector2i, string>(labels));
    }
}
