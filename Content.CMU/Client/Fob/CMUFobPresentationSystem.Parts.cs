using System.Numerics;
using Content.Shared.CMU14.Fob;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.Fob;

public sealed partial class CMUFobPresentationSystem
{
    private List<CMUFobSpritePart> GetParts(RSI.State state, RsiDirection direction, CMUFobDeviceAnimation device)
    {
        var key = (state, direction, device);
        if (_parts.TryGetValue(key, out var cached))
            return cached;
        // Pixel bounds refer to the imported RSI frame, not the packed texture atlas.
        UIBox2[] moving = device switch
        {
            CMUFobDeviceAnimation.HeavyGun => direction switch
            {
                RsiDirection.North => [new(24, 15, 30, 28), new(34, 15, 40, 28)],
                RsiDirection.East => [new(42, 24, 62, 32), new(42, 32, 62, 40)],
                RsiDirection.West => [new(2, 24, 22, 32), new(2, 32, 22, 40)],
                _ => [new(24, 39, 30, 53), new(34, 39, 40, 53)],
            },
            CMUFobDeviceAnimation.Rocket => [new(40, 20, 53, 41)],
            CMUFobDeviceAnimation.Platform => [new(14, 40, 24, 64), new(40, 40, 50, 64)],
            CMUFobDeviceAnimation.Light => [new(0, 0, 32, 32), new(0, 32, 32, 76)],
            CMUFobDeviceAnimation.Sensor => [new(7, 7, 25, 17)],
            CMUFobDeviceAnimation.Relay => [new(5, 18, 21, 43)],
            CMUFobDeviceAnimation.Terminal => [new(8, 33, 25, 41), new(5, 44, 27, 53)],
            CMUFobDeviceAnimation.Crate => [new(3, 3, 29, 18)],
            _ => [],
        };
        var remaining = new List<UIBox2> {new(0, 0, state.Size.X, state.Size.Y)};
        var parts = new List<CMUFobSpritePart>();
        var frame = state.GetFrame(direction, 0);
        for (var i = 0; i < moving.Length; i++)
        {
            var cut = moving[i];
            var next = new List<UIBox2>();
            foreach (var box in remaining)
            {
                var left = Math.Max(box.Left, cut.Left);
                var top = Math.Max(box.Top, cut.Top);
                var right = Math.Min(box.Right, cut.Right);
                var bottom = Math.Min(box.Bottom, cut.Bottom);
                if (left >= right || top >= bottom)
                {
                    next.Add(box);
                    continue;
                }
                if (box.Top < top) next.Add(new(box.Left, box.Top, box.Right, top));
                if (bottom < box.Bottom) next.Add(new(box.Left, bottom, box.Right, box.Bottom));
                if (box.Left < left) next.Add(new(box.Left, top, left, bottom));
                if (right < box.Right) next.Add(new(right, top, box.Right, bottom));
            }
            remaining = next;
            parts.Add(new(cut, i, Crop(frame, cut)));
        }
        // Static pixels first: barrels slide over their sockets instead of vanishing behind them.
        foreach (var box in remaining)
            parts.Insert(0, new CMUFobSpritePart(box, -1, Crop(frame, box)));
        _parts.Add(key, parts);
        return parts;
    }

    private static Texture Crop(Texture texture, UIBox2 bounds)
    {
        var origin = Vector2.Zero;
        // RSI frames already reference an atlas. Flatten the regions so the renderer sees one atlas level.
        while (texture is AtlasTexture atlas)
        {
            origin += atlas.SubRegion.TopLeft;
            texture = atlas.SourceTexture;
        }
        return new AtlasTexture(texture, UIBox2.FromDimensions(origin + bounds.TopLeft, bounds.Size));
    }
}
