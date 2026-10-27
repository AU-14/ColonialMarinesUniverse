using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Lobby;

/// <summary>Anonymous static silhouette, independent of the hidden character's actual appearance.</summary>
public sealed class CmuObscuredPortrait : Control
{
    public CmuObscuredPortrait()
    {
        MinSize = MaxSize = new Vector2(64, 88);
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var background = CmuLobbyPresentation.Surface;
        var foreground = CmuLobbyPresentation.Muted;
        handle.DrawRect(PixelSizeBox, Color.InterpolateBetween(background, Color.Black, 0.35f));
        var cell = 4 * UIScale;
        for (var y = 0; y < 22; y++)
        for (var x = 0; x < 16; x++)
        {
            var silhouette = y is >= 2 and <= 6 && x is >= 5 and <= 10 ||
                y is >= 8 and <= 14 && x is >= 3 and <= 12 ||
                y is >= 15 and <= 20 && (x is >= 5 and <= 7 || x is >= 9 and <= 10);
            var noise = ((x * 37 + y * 71 + x * y * 13) % 29) / 28f;
            var strength = silhouette ? 0.2f + noise * 0.3f : noise * 0.08f;
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y) * cell, new Vector2(cell)),
                Color.InterpolateBetween(background, foreground, strength));
        }
    }
}
