using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Chat;

/// <summary>Magnifier toggle beside the chat settings gear; turns the chat search box on and off.</summary>
public sealed class ChatSearchButton : Button
{
    private static readonly Color ColorNormal = Color.FromHex("#7b7e9e");
    private static readonly Color ColorActive = Color.FromHex("#789B8C");

    private readonly TextureRect _icon;

    public ChatSearchButton()
    {
        ToggleMode = true;
        ToolTip = Loc.GetString("cmu-chat-search-tooltip");
        StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        MinSize = new Vector2(28, 22);

        _icon = new TextureRect
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Stretch = TextureRect.StretchMode.Scale,
            CanShrink = true,
            MinSize = new Vector2(18, 18),
            MaxSize = new Vector2(18, 18),
            Texture = IoCManager.Resolve<IResourceCache>().GetTexture("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"),
            ModulateSelfOverride = ColorNormal,
        };
        AddChild(_icon);

        OnToggled += args => _icon.ModulateSelfOverride = args.Pressed ? ColorActive : ColorNormal;
    }
}
