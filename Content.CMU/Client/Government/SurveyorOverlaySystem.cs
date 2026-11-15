// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Shared.CMU14.Government;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Government;

// With the surveyor's tablet in the hand the ground shows what is marked: every zone is a coloured square with its name, and the square of the next mark follows the one who holds the tablet (green where
// it may be put, red where it would overlap a zone or no zone can be added any more), cut into the plots of five tiles.
public sealed class SurveyorOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private SurveyorOverlay? _overlay;

    public override void Shutdown()
    {
        base.Shutdown();
        Hide();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player
            || !_hands.TryGetActiveItem(player, out var held)
            || !TryComp(held, out SurveyorTabletComponent? tablet))
        {
            Hide();
            return;
        }

        if (_overlay == null)
        {
            _overlay = new SurveyorOverlay();
            _overlays.AddOverlay(_overlay);
        }

        _overlay.Player = player;
        _overlay.Tablet = tablet;
    }

    private void Hide()
    {
        if (_overlay == null)
            return;

        _overlays.RemoveOverlay(_overlay);
        _overlay = null;
    }
}

public sealed class SurveyorOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public EntityUid Player;
    public SurveyorTabletComponent? Tablet;

    private Font? _font;

    public SurveyorOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    public static Color ColorOf(ZoneKind kind) => kind switch
    {
        ZoneKind.Housing => Color.FromHex("#4aa3ff"),
        ZoneKind.Trade => Color.FromHex("#ffb000"),
        _ => Color.FromHex("#c864ff"),
    };

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Tablet == null || args.MapId == MapId.Nullspace || args.ViewportControl == null)
            return;

        var transform = _entMan.System<SharedTransformSystem>();

        if (args.Space == OverlaySpace.ScreenSpace)
        {
            DrawLabels(args, transform);
            return;
        }

        var handle = args.WorldHandle;

        foreach (var mark in Tablet.Marks)
        {
            if (!_entMan.TryGetEntity(mark.Grid, out var grid) || !_entMan.EntityExists(grid) || _entMan.GetComponent<TransformComponent>(grid.Value).MapID != args.MapId)
                continue;

            handle.SetTransform(transform.GetWorldMatrix(grid.Value));
            var box = new Box2(mark.Min, mark.Min + new Vector2i(mark.Size, mark.Size));
            var color = ColorOf(mark.Kind);
            handle.DrawRect(box, color.WithAlpha(0.18f));
            handle.DrawRect(box, color.WithAlpha(0.9f), filled: false);
            DrawPlots(handle, box, color.WithAlpha(0.35f));
        }

        var playerXform = _entMan.GetComponent<TransformComponent>(Player);
        if (playerXform.GridUid is { } playerGrid && playerXform.MapID == args.MapId)
        {
            var matrix = transform.GetWorldMatrix(playerGrid);
            handle.SetTransform(matrix);

            var preview = PreviewBox(transform, playerGrid, out var min);
            var color = Blocked(playerGrid, min) ? Color.Red : Color.LimeGreen;
            handle.DrawRect(preview, color.WithAlpha(0.28f));
            handle.DrawRect(preview, color.WithAlpha(0.95f), filled: false);
            DrawPlots(handle, preview, color.WithAlpha(0.5f));
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    // The square of the next mark: the middle of it is where the one who holds the tablet stands, as the server cuts it.
    private Box2 PreviewBox(SharedTransformSystem transform, EntityUid grid, out Vector2i min)
    {
        var size = Tablet!.Size;
        var point = Vector2.Transform(transform.GetWorldPosition(Player), transform.GetInvWorldMatrix(grid));
        min = new Vector2i((int) MathF.Floor(point.X) - size / 2, (int) MathF.Floor(point.Y) - size / 2);
        return new Box2(min, min + new Vector2i(size, size));
    }

    private bool Blocked(EntityUid grid, Vector2i min)
    {
        if (Tablet!.Full)
            return true;

        var size = Tablet.Size;
        foreach (var other in Tablet.Marks)
        {
            if (!_entMan.TryGetEntity(other.Grid, out var otherGrid) || otherGrid != grid)
                continue;

            if (min.X < other.Min.X + other.Size && other.Min.X < min.X + size && min.Y < other.Min.Y + other.Size && other.Min.Y < min.Y + size)
                return true;
        }

        return false;
    }

    // The lines between the plots (a plot is five tiles on a side).
    private static void DrawPlots(DrawingHandleWorld handle, Box2 box, Color color)
    {
        for (var x = box.Left + LandPlot.Side; x < box.Right - 0.01f; x += LandPlot.Side)
            handle.DrawLine(new Vector2(x, box.Bottom), new Vector2(x, box.Top), color);

        for (var y = box.Bottom + LandPlot.Side; y < box.Top - 0.01f; y += LandPlot.Side)
            handle.DrawLine(new Vector2(box.Left, y), new Vector2(box.Right, y), color);
    }

    private void DrawLabels(in OverlayDrawArgs args, SharedTransformSystem transform)
    {
        _font ??= new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 12);

        foreach (var mark in Tablet!.Marks)
        {
            if (!_entMan.TryGetEntity(mark.Grid, out var grid) || !_entMan.EntityExists(grid) || _entMan.GetComponent<TransformComponent>(grid.Value).MapID != args.MapId)
                continue;

            var middle = new Vector2(mark.Min.X + mark.Size / 2f, mark.Min.Y + mark.Size / 2f);
            var world = Vector2.Transform(middle, transform.GetWorldMatrix(grid.Value));
            var text = $"{KindName(mark.Kind)} {mark.Size}x{mark.Size}";
            var width = args.ScreenHandle.GetDimensions(_font, text, 1f).X;
            args.ScreenHandle.DrawString(_font, args.ViewportControl!.WorldToScreen(world) - new Vector2(width / 2f, 8f), text, ColorOf(mark.Kind));
        }
    }

    private string KindName(ZoneKind kind)
    {
        foreach (var proto in _proto.EnumeratePrototypes<ColonyZoneKindPrototype>())
        {
            if (proto.Kind == kind)
                return Loc.GetString(proto.Name);
        }

        return kind.ToString();
    }
}
