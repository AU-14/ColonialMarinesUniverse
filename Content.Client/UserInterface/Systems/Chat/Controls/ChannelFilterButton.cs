using System.Numerics;
using Content.Client._CMU14.Interface;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared.Chat;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChannelFilterButton : ChatPopupButton<ChannelFilterPopup>
{
    private static readonly Color ColorNormal = Color.FromHex("#7b7e9e");
    private static readonly Color ColorHovered = Color.FromHex("#9699bb");
    private static readonly Color ColorPressed = Color.FromHex("#789B8C");
    private const int LegacyFilterDropdownOffset = 120;
    private readonly TextureRect? _textureRect;
    private readonly IResourceCache _resourceCache;
    private readonly ChatUIController _chatUIController;
    private ChatChannel _allowedChannels = ~ChatChannel.None;
    private bool _legacyMode;

    public ChannelFilterButton()
    {
        _resourceCache = IoCManager.Resolve<IResourceCache>();
        _chatUIController = UserInterfaceManager.GetUIController<ChatUIController>();
        ToolTip = Loc.GetString("hud-chatbox-settings-tooltip");

        // Show the gear alone rather than a boxed button. A transparent StyleBoxOverride removes
        // the panel while leaving the button's whole rectangle clickable - the icon itself is a
        // TextureRect and would otherwise be the only hittable part. MinSize keeps that rectangle
        // comfortably larger than the icon.
        // NOTE: ChatInputBox re-sets MinSize on the instance it builds (it has to, legacy mode
        // wants zero), so keep the two in step.
        StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        MinSize = new Vector2(28, 22);

        AddChild(
            (_textureRect = new TextureRect
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Stretch = TextureRect.StretchMode.Scale,
                CanShrink = true
            })
        );
        SetLegacyMode(false);

        _chatUIController.FilterableChannelsChanged += OnFilterableChannelsChanged;
        _chatUIController.UnreadMessageCountsUpdated += Popup.UpdateUnread;
        OnFilterableChannelsChanged(_chatUIController.FilterableChannels);
    }

    public void SetLegacyMode(bool legacy)
    {
        _legacyMode = legacy;
        ToolTip = legacy
            ? Loc.GetString("hud-chatbox-settings-filters")
            : Loc.GetString("hud-chatbox-settings-tooltip");

        if (_textureRect != null)
        {
            var iconSize = legacy
                ? new Vector2(18, 18)
                : new Vector2(20, 20);
            _textureRect.MinSize = iconSize;
            _textureRect.MaxSize = iconSize;
            _textureRect.Texture = _resourceCache.GetTexture(legacy
                ? "/Textures/Interface/Nano/filter.svg.96dpi.png"
                : "/Textures/Interface/VerbIcons/settings.svg.192dpi.png");
        }
    }

    public void SetAllowedChannels(ChatChannel channels)
    {
        _allowedChannels = channels;
        OnFilterableChannelsChanged(_chatUIController.FilterableChannels);
    }

    private void OnFilterableChannelsChanged(ChatChannel channels)
    {
        Popup.SetChannels(channels & _allowedChannels);
    }

    protected override UIBox2 GetPopupPosition()
    {
        var globalPos = GlobalPosition;
        var (minX, minY) = Popup.MinSize;
        var width = Math.Max(minX, Popup.MinWidth);
        if (_legacyMode)
        {
            return UIBox2.FromDimensions(
                globalPos - new Vector2(LegacyFilterDropdownOffset, 0),
                new Vector2(width, minY));
        }

        var offset = Math.Min(width, globalPos.X);
        return UIBox2.FromDimensions(
            globalPos - new Vector2(offset, 0),
            new Vector2(width, minY));
    }

    private void UpdateChildColors()
    {
        if (_textureRect == null) return;

        // The stock colours are blue-greys (#7b7e9e and friends), which left the gear as the one
        // off-palette thing on an otherwise green input row. Under CRT it takes the text ladder
        // instead: dim at rest, body text on hover, accent while the popup is open.
        var crt = StyleNano.CrtUiEnabled;
        switch (DrawMode)
        {
            case DrawModeEnum.Normal:
                _textureRect.ModulateSelfOverride = crt ? CrtTerminalPalette.TextDim : ColorNormal;
                break;

            case DrawModeEnum.Pressed:
                _textureRect.ModulateSelfOverride = crt ? CrtTerminalPalette.Accent : ColorPressed;
                break;

            case DrawModeEnum.Hover:
                _textureRect.ModulateSelfOverride = crt ? CrtTerminalPalette.Text : ColorHovered;
                break;

            case DrawModeEnum.Disabled:
                break;
        }
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateChildColors();
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        UpdateChildColors();
    }

    [System.Obsolete]
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _chatUIController.FilterableChannelsChanged -= OnFilterableChannelsChanged;
        _chatUIController.UnreadMessageCountsUpdated -= Popup.UpdateUnread;
    }
}
