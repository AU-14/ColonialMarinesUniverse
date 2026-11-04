using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Fighter;

/// <summary>A compact readout for the corner cockpit panels.</summary>
public sealed class FighterCockpitReadout : Control
{
    private readonly Font _font = new VectorFont(IoCManager.Resolve<IResourceCache>()
        .GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 9);

    public string Text { get; set; } = string.Empty;
    public bool Warning { get; set; }
    public float Emphasis { get; set; }

    public FighterCockpitReadout()
    {
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (PixelSize.X <= 8 || PixelSize.Y <= 0) return;
        if (Emphasis > .01f) handle.DrawRect(PixelSizeBox, Color.White.WithAlpha(Emphasis * .12f));
        var lines = Text.Split('\n');
        var scale = Math.Min(UIScale, PixelSize.Y / (Math.Max(1, lines.Length) * 16f));
        foreach (var line in lines)
        {
            var width = handle.GetDimensions(_font, line, scale).X;
            if (width > PixelSize.X - 8) scale *= (PixelSize.X - 8) / width;
        }
        var lineHeight = _font.GetLineHeight(scale);
        var y = (PixelSize.Y - lines.Length * lineHeight) / 2f;
        foreach (var line in lines)
        {
            handle.DrawString(_font, new Vector2(2 * UIScale, y), line, scale,
                Color.FromHex(Warning ? "#DAB476" : "#CBD3DE"));
            y += lineHeight;
        }
    }
}
