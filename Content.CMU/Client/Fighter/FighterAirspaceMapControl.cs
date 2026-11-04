using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Fighter;

/// <summary>North-up sector map; all contacts and targeting state come from the server.</summary>
public sealed class FighterAirspaceMapControl : Control
{
    private static readonly Color[] TerrainColors = [Color.FromHex("#14201D"), Color.FromHex("#3C4B43"),
        Color.FromHex("#23444C"), Color.FromHex("#30432A"), Color.FromHex("#5C5A43")];
    private readonly Font _font = new VectorFont(IoCManager.Resolve<IResourceCache>()
        .GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
    private FighterAirspaceRadarEvent? _state;
    private byte[] _terrain = [];

    public FighterAirspaceMapControl()
    {
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void SetState(FighterAirspaceRadarEvent state, byte[] terrain)
    {
        _state = state;
        _terrain = terrain;
    }

    protected override void Draw(DrawingHandleScreen h)
    {
        h.DrawRect(PixelSizeBox, TerrainColors[0]);
        if (_state is not { } state || state.Battlefield.Width <= 0 || state.Battlefield.Height <= 0) return;
        var bounds = state.Battlefield;
        var scale = Math.Max(.01f, Math.Min((PixelSize.X - 18) / bounds.Width, (PixelSize.Y - 18) / bounds.Height));
        Vector2 Project(Vector2 point) => (Vector2) PixelSize / 2 + (point - bounds.Center) * new Vector2(scale, -scale);
        var n = FighterChartComponent.Resolution;
        if (_terrain.Length == n * n)
        {
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n;)
            {
                var value = _terrain[y * n + x];
                var end = x + 1;
                while (end < n && _terrain[y * n + end] == value) end++;
                var p = Project(bounds.BottomLeft + bounds.Size * new Vector2((float) x / n, (float) (y + 1) / n));
                h.DrawRect(UIBox2.FromDimensions(p, bounds.Size * scale * new Vector2((float) (end - x) / n, 1f / n) + new Vector2(.5f)),
                    TerrainColors[Math.Min(value, (byte) 4)]);
                x = end;
            }
        }

        var grid = FighterAirCombat.Grid(bounds);
        var sector = FighterAirCombat.SectorAt(bounds, state.OperatorPosition);
        var accent = Color.FromHex("#68DCCC");
        for (var cell = 0; cell < grid.X * grid.Y; cell++)
        {
            var box = FighterAirCombat.SectorBounds(bounds, cell);
            var rect = new UIBox2(Project(box.TopLeft), Project(box.BottomRight));
            if (cell == sector) h.DrawRect(rect, accent.WithAlpha(state.Aiming ? .20f : .08f));
            h.DrawRect(rect, cell == sector ? accent : Color.FromHex("#74877770"), false);
            if (box.Width * scale > 30 && box.Height * scale > 22)
                h.DrawString(_font, Project(box.TopLeft) + new Vector2(3, 12), FighterAirCombat.Label(bounds, cell),
                    Color.FromHex("#CADAC1"));
        }
        if (sector >= 0)
        {
            var position = Project(state.OperatorPosition);
            h.DrawCircle(position, 4, accent);
            h.DrawCircle(position, 7, accent, false);
        }
        foreach (var contact in state.Contacts)
        {
            var color = contact.Disposition switch
            {
                FighterContactDisposition.Friendly => Color.FromHex("#9AF078"),
                FighterContactDisposition.Hostile => Color.FromHex("#FF8660"),
                _ => Color.FromHex("#E5D986"),
            };
            var point = Project(contact.Position);
            var forward = FighterFlight.Forward(contact.Heading) * new Vector2(1, -1);
            var side = new Vector2(-forward.Y, forward.X);
            var tip = point + forward * 8;
            var tail = point - forward * 5;
            h.DrawLine(tail, tip, color);
            h.DrawLine(tip, tail + side * 5, color);
            h.DrawLine(tip, tail - side * 5, color);
            if (contact.Locked)
            {
                h.DrawCircle(point, 13, Color.White, false);
                h.DrawLine(Project(state.OperatorPosition), point, color.WithAlpha(.6f));
            }
            var label = Loc.GetString("cmu-manpad-radar-height", ("height", (int) MathF.Round(contact.Height)));
            var labelSize = h.GetDimensions(_font, label, 1);
            var labelPosition = point + new Vector2(12, 4);
            labelPosition.X = Math.Clamp(labelPosition.X, 0, Math.Max(0, PixelSize.X - labelSize.X));
            labelPosition.Y = Math.Clamp(labelPosition.Y, labelSize.Y, Math.Max(labelSize.Y, PixelSize.Y));
            h.DrawString(_font, labelPosition, label, color);
        }
        h.DrawString(_font, new Vector2(3, 12), Loc.GetString("cmu-fighter-north"), Color.White);
    }
}
