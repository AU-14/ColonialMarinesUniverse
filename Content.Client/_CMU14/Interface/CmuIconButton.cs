using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     A bare icon that behaves like a button: no box, no label, just the glyph and its hit area.
///     Same shape as the chat's settings gear.
/// </summary>
public sealed class CmuIconButton : Button
{
    private static readonly Vector2 IconSize = new(16, 16);

    private readonly TextureRect _icon;
    private string? _texturePath;

    public string? TexturePath
    {
        get => _texturePath;
        set
        {
            _texturePath = value;
            _icon.Texture = string.IsNullOrWhiteSpace(value)
                ? null
                : IoCManager.Resolve<IResourceCache>().GetTexture(value);
        }
    }

    public CmuIconButton()
    {
        // Transparent box rather than none: the box is what makes the whole rectangle clickable.
        StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        MinSize = new Vector2(22, 18);

        AddChild(_icon = new TextureRect
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Stretch = TextureRect.StretchMode.Scale,
            CanShrink = true,
            MinSize = IconSize,
            MaxSize = IconSize,
        });

        UpdateIconColor();
    }

    private void UpdateIconColor()
    {
        // Load-bearing: Button's constructor calls DrawModeChanged before _icon is assigned.
        if (_icon == null)
            return;

        var crt = StyleNano.CrtUiEnabled;

        _icon.ModulateSelfOverride = DrawMode switch
        {
            DrawModeEnum.Pressed => crt ? CrtTerminalPalette.Accent : Color.FromHex("#789B8C"),
            DrawModeEnum.Hover => crt ? CrtTerminalPalette.Text : Color.FromHex("#9699bb"),
            DrawModeEnum.Disabled => crt ? CrtTerminalPalette.TextDim : Color.FromHex("#5a5c72"),
            _ => crt ? CrtTerminalPalette.TextDim : Color.FromHex("#7b7e9e"),
        };
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateIconColor();
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        UpdateIconColor();
    }
}
