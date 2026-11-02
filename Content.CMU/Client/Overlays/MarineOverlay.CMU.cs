using System.Numerics;
using Content.Client.CMU14.Overlays;
using Robust.Client.Graphics;

namespace Content.Client._RMC14.Marines;

public sealed partial class MarineOverlay
{
    private CMUCachedDrawing? _questionDrawing;

    private void DrawCachedQuestionMark(IRenderHandle render, DrawingHandleWorld handle, Vector2 center)
    {
        _questionDrawing ??= new CMUCachedDrawing(IoCManager.Resolve<IClyde>(), _prototype);
        if (_questionDrawing.Texture == null)
        {
            // Bake the same eight black offsets and red fill at the original three-pixel scale.
            var size = new Vector2i(_unidentifiedIcon.Width * 3 + 2, _unidentifiedIcon.Height * 3 + 2);
            _questionDrawing.Update(render, size, Color.White, screen =>
            {
                var bounds = UIBox2.FromDimensions(Vector2.One, new Vector2(size.X - 2, size.Y - 2));
                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                {
                    if (x != 0 || y != 0)
                        screen.DrawTextureRect(_unidentifiedIcon, bounds.Translated(new Vector2(x, y)), Color.Black);
                }

                screen.DrawTextureRect(_unidentifiedIcon, bounds, UnidentifiedColor);
            });
        }

        var texture = _questionDrawing.Texture!;
        var shader = handle.GetShader();
        handle.UseShader(_questionDrawing.Shader);
        handle.DrawTextureRect(texture,
            Box2.CenteredAround(center, new Vector2(texture.Width, texture.Height) / EyeManager.PixelsPerMeter));
        handle.UseShader(shader);
    }

    protected override void DisposeBehavior()
    {
        _questionDrawing?.Dispose();
        base.DisposeBehavior();
    }
}
