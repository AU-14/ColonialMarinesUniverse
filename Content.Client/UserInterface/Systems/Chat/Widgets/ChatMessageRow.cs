using System;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client.Resources;
using Content.Shared._CMU14.Ghost;
using Content.Shared.Chat;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public sealed partial class ChatMessageRow : PanelContainer
{
    [Dependency] private IClientConsoleHost _consoleHost = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    private readonly Label _repeatBadge;
    private readonly RichTextLabel _messageLabel;

    public ChatMessageRow(ChatMessage message, FormattedMessage formatted, Color textColor, Color? accentOverride = null, int? fontSize = null)
    {
        IoCManager.InjectDependencies(this);

        var accent = accentOverride ?? GetAccent(message, textColor);
        var metrics = GetMetrics(fontSize);
        HorizontalExpand = true;
        Margin = new Thickness(0, 0, 0, metrics.OuterBottomMargin);
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = GetBackground(message.Channel),
            BorderColor = accent,
            BorderThickness = new Thickness(2, 0, 0, 0),
            ContentMarginLeftOverride = 4,
            ContentMarginRightOverride = 4,
            ContentMarginTopOverride = metrics.VerticalPadding,
            ContentMarginBottomOverride = metrics.VerticalPadding
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = metrics.HorizontalGap,
            HorizontalExpand = true
        };
        AddChild(row);

        var sideFont = fontSize == null
            ? null
            : _resourceCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", fontSize.Value);

        var prefix = BuildPrefix(message);
        if (prefix != null)
        {
            row.AddChild(new Label
            {
                Text = prefix,
                MinWidth = metrics.PrefixMinWidth,
                MaxWidth = metrics.PrefixMaxWidth,
                ClipText = true,
                Modulate = accent,
                FontOverride = sideFont,
                VerticalAlignment = VAlignment.Top
            });
        }

        if (message.GhostFollowEntity.Valid)
        {
            var followButton = CreateFollowButton(message, metrics, textColor);
            row.AddChild(followButton);
        }

        _messageLabel = new RichTextLabel
        {
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Top,
            LineHeightScale = metrics.LineHeightScale
        };
        _messageLabel.SetMessage(formatted, defaultColor: textColor);
        row.AddChild(_messageLabel);

        _repeatBadge = new Label
        {
            Visible = false,
            MinWidth = metrics.RepeatMinWidth,
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Top,
            Align = Label.AlignMode.Center,
            Modulate = Color.FromHex("#ff6d5f"),
            FontOverride = sideFont
        };
        row.AddChild(_repeatBadge);
    }

    private Button CreateFollowButton(ChatMessage message, RowMetrics metrics, Color textColor)
    {
        var followButtonSize = new Vector2(metrics.FollowButtonSize, metrics.FollowButtonSize);
        var followButtonColor = textColor.WithAlpha(1f);
        var followButton = new Button
        {
            Text = Loc.GetString("cmu-chat-manager-follow-button"),
            ToolTip = Loc.GetString("cmu-chat-manager-follow-button-tooltip"),
            MinSize = followButtonSize,
            MaxSize = followButtonSize,
            Margin = new Thickness(2, 5, 2, 0),
            ModulateSelfOverride = followButtonColor,
            VerticalAlignment = VAlignment.Top,
            StyleClasses = { StyleNano.StyleClassChatGhostFollowButton }
        };

        followButton.Label.HorizontalExpand = true;
        followButton.Label.HorizontalAlignment = HAlignment.Center;
        followButton.Label.VerticalAlignment = VAlignment.Center;
        followButton.Label.Align = Label.AlignMode.Center;
        followButton.Label.FontColorOverride = followButtonColor;
        followButton.OnPressed += _ => _consoleHost.ExecuteCommand($"{CMUGhostFollowCommand.CommandName} {message.GhostFollowEntity}");
        return followButton;
    }

    public void SetRepeatCount(int count)
    {
        _repeatBadge.Visible = count > 1;
        _repeatBadge.Text = $"x{count}";
    }

    public void RefreshLayout()
    {
        _messageLabel.InvalidateMeasure();
        foreach (var control in _messageLabel.Controls)
        {
            control.InvalidateMeasure();
        }

        _repeatBadge.InvalidateMeasure();
        InvalidateMeasure();
    }

    private static string? BuildPrefix(ChatMessage message)
    {
        return GetChannelLabel(message);
    }

    private static RowMetrics GetMetrics(int? fontSize)
    {
        if (fontSize == null)
            return new RowMetrics(2, 4, 0, 1.06f, 42, 58, 25, 16);

        return fontSize.Value switch
        {
            <= 9 => new RowMetrics(1, 3, 0, 1.0f, 34, 46, 20, 14),
            <= 11 => new RowMetrics(1, 3, 0, 1.02f, 38, 52, 22, 15),
            <= 13 => new RowMetrics(2, 4, 0, 1.04f, 40, 56, 24, 16),
            _ => new RowMetrics(2, 4, 0, 1.06f, 42, 58, 25, 18)
        };
    }

    private static string? GetChannelLabel(ChatMessage message)
    {
        if (message.Channel is ChatChannel.Local or ChatChannel.Whisper or ChatChannel.Emotes)
            return null;

        if (IsUnlabeledRadioSystemMessage(message))
            return null;

        if (!string.IsNullOrWhiteSpace(message.Display?.ChannelLabel))
            return message.Display.ChannelLabel.ToUpperInvariant();

        return message.Channel switch
        {
            ChatChannel.Radio => "RAD",
            ChatChannel.LOOC => "LOOC",
            ChatChannel.OOC => "OOC",
            ChatChannel.Dead => "DEAD",
            ChatChannel.Admin => "ADMIN",
            ChatChannel.AdminAlert => "ALERT",
            ChatChannel.AdminChat => "ASAY",
            ChatChannel.MentorChat => "MENTOR",
            ChatChannel.Notifications => "NOTE",
            ChatChannel.Server => "SYS",
            ChatChannel.Damage => "DMG",
            ChatChannel.Visual => "VIS",
            _ => "CHAT"
        };
    }

    private static bool IsUnlabeledRadioSystemMessage(ChatMessage message)
    {
        if (message.Channel != ChatChannel.Radio || message.Display is not { } display)
            return false;

        return display.Kind == ChatDisplayKind.Radio
            && string.IsNullOrWhiteSpace(display.SenderName)
            && string.IsNullOrWhiteSpace(display.SenderPrefix)
            && string.IsNullOrWhiteSpace(display.Verb)
            && string.Equals(display.ChannelLabel, "RAD", StringComparison.OrdinalIgnoreCase);
    }

    private static Color GetBackground(ChatChannel channel)
    {
        if ((channel & ChatChannel.AdminRelated) != 0)
            return Color.FromHex("#23151e");

        return channel switch
        {
            ChatChannel.Radio => Color.FromHex("#121f18"),
            ChatChannel.OOC or ChatChannel.LOOC => Color.FromHex("#12202a"),
            ChatChannel.Dead => Color.FromHex("#13141d"),
            ChatChannel.Server or ChatChannel.Notifications => Color.FromHex("#211c12"),
            ChatChannel.Whisper => Color.FromHex("#151515"),
            _ => Color.FromHex("#101214")
        };
    }

    private static Color GetAccent(ChatMessage message, Color fallback)
    {
        if (message.Display?.AccentColor is { } accent)
            return accent;

        return message.Channel switch
        {
            ChatChannel.Local => Color.FromHex("#6d7f8f"),
            ChatChannel.Whisper => Color.FromHex("#646464"),
            ChatChannel.Emotes => Color.FromHex("#b493d6"),
            ChatChannel.Radio => Color.FromHex("#73d48f"),
            ChatChannel.LOOC => Color.FromHex("#61d7d6"),
            ChatChannel.OOC => Color.FromHex("#73bdf6"),
            ChatChannel.Dead => Color.FromHex("#8d7bd4"),
            ChatChannel.Admin or ChatChannel.AdminAlert => Color.FromHex("#ff5f5f"),
            ChatChannel.AdminChat => Color.FromHex("#ff72c7"),
            ChatChannel.MentorChat => Color.FromHex("#ffb55f"),
            ChatChannel.Server or ChatChannel.Notifications => Color.FromHex("#dda94b"),
            _ => fallback
        };
    }

    private readonly record struct RowMetrics(
        int VerticalPadding,
        int HorizontalGap,
        int OuterBottomMargin,
        float LineHeightScale,
        int PrefixMinWidth,
        int PrefixMaxWidth,
        int RepeatMinWidth,
        int FollowButtonSize);
}
