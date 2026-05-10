using System.Numerics;
using Content.Client.Resources;
using Content.Shared.Chat;
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
                ? new Vector2(14, 14)
                : new Vector2(13, 13);
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
        switch (DrawMode)
        {
            case DrawModeEnum.Normal:
                _textureRect.ModulateSelfOverride = ColorNormal;
                break;

            case DrawModeEnum.Pressed:
                _textureRect.ModulateSelfOverride = ColorPressed;
                break;

            case DrawModeEnum.Hover:
                _textureRect.ModulateSelfOverride = ColorHovered;
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _chatUIController.FilterableChannelsChanged -= OnFilterableChannelsChanged;
        _chatUIController.UnreadMessageCountsUpdated -= Popup.UpdateUnread;
    }
}
