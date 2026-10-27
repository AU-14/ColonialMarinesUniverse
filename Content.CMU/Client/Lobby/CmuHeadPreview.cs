using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Lobby;

/// <summary>Layers the original head and marking textures without spawning a preview entity per card.</summary>
public sealed class CmuHeadPreview : Control
{
    private readonly Texture _male;
    private readonly Texture _female;
    private readonly Texture _eyes;
    private readonly IReadOnlyList<Texture> _markings;
    private readonly Func<(bool Female, Color Skin, Color Eye, Color Hair)> _colors;

    public CmuHeadPreview(SpriteSystem sprites, IReadOnlyList<Texture> markings,
        Func<(bool Female, Color Skin, Color Eye, Color Hair)> colors)
    {
        _male = sprites.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Mobs/Species/Human/parts.rsi"), "head_m"));
        _female = sprites.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Mobs/Species/Human/parts.rsi"), "head_f"));
        _eyes = sprites.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Mobs/Customization/eyes.rsi"), "eyes"));
        _markings = markings;
        _colors = colors;
        MinSize = new Vector2(128, 100);
        MaxHeight = 100;
        HorizontalExpand = true;
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var colors = _colors();
        var size = 128 * UIScale;
        var rect = UIBox2.FromDimensions(new Vector2((PixelWidth - size) / 2, 0), new Vector2(size));
        handle.DrawTextureRect(colors.Female ? _female : _male, rect, colors.Skin);
        handle.DrawTextureRect(_eyes, rect, colors.Eye);
        foreach (var marking in _markings) handle.DrawTextureRect(marking, rect, colors.Hair);
    }
}
