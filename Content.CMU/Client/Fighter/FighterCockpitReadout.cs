using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Fighter;

/// <summary>A compact readout for the separate flight and reconnaissance panel.</summary>
public sealed class FighterCockpitReadout : Control
{
    private readonly Font _font = new VectorFont(IoCManager.Resolve<IResourceCache>()
        .GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);

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
        if (Emphasis > .01f) handle.DrawRect(PixelSizeBox, Color.FromHex("#AEC987").WithAlpha(Emphasis * .2f));
        var lines = Text.Split('\n');
        var scale = Math.Clamp(PixelSize.Y / (Math.Max(1, lines.Length) * 22f), .75f, 2.5f);
        foreach (var line in lines)
        {
            var width = handle.GetDimensions(_font, line, scale).X;
            if (width > PixelSize.X - 8) scale *= (PixelSize.X - 8) / width;
        }
        var lineHeight = _font.GetLineHeight(scale);
        var y = (PixelSize.Y - lines.Length * lineHeight) / 2f;
        foreach (var line in lines)
        {
            var size = handle.GetDimensions(_font, line, scale);
            handle.DrawString(_font, new Vector2((PixelSize.X - size.X) / 2, y), line, scale,
                Color.FromHex(Warning ? "#DAB476" : "#B2CD98"));
            y += lineHeight;
        }
    }
}
