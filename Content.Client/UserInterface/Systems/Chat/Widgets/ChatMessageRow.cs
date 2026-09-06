using System;
using System.Numerics;
using Content.Client._CMU14.Interface;
using Content.Client.Stylesheets;
using Content.Client.Resources;
using Content.Client.UserInterface.RichText;
using Content.Client._RMC14.Chat;
using Content.Shared.CMU14.Ghost;
using Content.Shared.CMU14.Xenonids.Watch;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public sealed partial class ChatMessageRow : PanelContainer
{
    internal static readonly Type[] AllowedMarkupTags =
    [
        typeof(BoldItalicTag),
        typeof(BoldTag),
        typeof(BulletTag),
        typeof(ChatCommandLinkTag),
        typeof(ColorTag),
        typeof(FontTag),
        typeof(HeadingTag),
        typeof(ItalicTag),
        typeof(LanguageIconTag),
        typeof(MonoTag),
        typeof(ScrambleTag),
    ];

    [Dependency] private IClientConsoleHost _consoleHost = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IConfigurationManager _config = default!;

    /// <summary>
    ///     Line spacing for CRT message bodies. The uavOsd face has very tight vertical metrics, so
    ///     the ~1.06 the base theme uses leaves wrapped messages with almost no gap between lines.
    ///     The readable font does not have that problem and looks gappy at 1.25.
    /// </summary>
    /// <remarks>
    ///     Has to be set on the control even though <c>CrtChatText</c> carries the same value: a
    ///     direct set of <see cref="RichTextLabel.LineHeightScale"/> beats the stylesheet, so leaving
    ///     it to the rule would mean whatever the base theme's metrics happened to be. Keep the two
    ///     in step.
    /// </remarks>
    private static float CrtLineHeightScale => StyleNano.ChatReadableFont ? 1.0f : 1.25f;

    private readonly Label _repeatBadge;
    private readonly RichTextLabel _messageLabel;

    public ChatMessageRow(ChatMessage message, FormattedMessage formatted, Color textColor, Color? accentOverride = null, int? fontSize = null)
    {
        IoCManager.InjectDependencies(this);

        var accent = accentOverride ?? GetAccent(message, textColor);
        var metrics = GetMetrics(fontSize);

        // Fill and a left rule, never a full outline: four sides is a framed box inside the pane's box.
        var isAnnouncement = IsUnlabeledRadioSystemMessage(message);
        var isTinted = isAnnouncement || message.Display?.BackgroundColorOverride != null;

        HorizontalExpand = true;
        Margin = new Thickness(0, 0, 0, isTinted ? Math.Max(metrics.OuterBottomMargin, 4) : metrics.OuterBottomMargin);
        // The channel accent is a triangle in the top-right corner rather than a stripe down the
        // left edge, so rows read as tagged rather than bracketed.
        PanelOverride = new ChatAccentStyleBox
        {
            BackgroundColor = GetBackground(message, accent, CrtTintSaturation(_config.GetCVar(CCVars.CMUChatRowTint))),
            AccentColor = accent,
            AccentSize = metrics.AccentSize,
            BorderColor = accent,
            BorderThickness = isAnnouncement ? new Thickness(2, 0, 0, 0) : new Thickness(0),
            // Keep prefixes close to the edge while leaving the row tint flush with the panel.
            ContentMarginLeftOverride = 4,
            // Leave room for the corner triangle so it never sits on top of the text.
            ContentMarginRightOverride = 4 + metrics.AccentSize,
            // Asymmetric on purpose: the 1.25 line height puts its leading under the last line, so
            // equal padding renders bottom-heavy. 5/3 is what measures 6/6 on screen.
            ContentMarginTopOverride = metrics.VerticalPadding + (isAnnouncement ? 5 : 0),
            ContentMarginBottomOverride = metrics.VerticalPadding + (isAnnouncement ? 3 : 0)
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = metrics.HorizontalGap,
            HorizontalExpand = true
        };
        AddChild(row);

        // Under CRT the side labels (channel prefix, repeat badge) take the chat face at the size the
        // message bodies sit at, so the prefix column and the text beside it are one typeface at one
        // size rather than two.
        //
        // Deliberately ignores `fontSize`, and that needs saying: RichTextLabel has no FontOverride,
        // so the body is whatever the stylesheet's CrtChatText says and cannot follow the caller's
        // size. It never could - before CRT the body simply used the theme default and `fontSize`
        // only ever reached these side labels. Honouring it here would scale the prefix away from a
        // body that cannot move.
        var sideFont = StyleNano.CrtUiEnabled
            ? StyleNano.GetChatFont(_resourceCache)
            : fontSize == null
                ? null
                : _resourceCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", fontSize.Value);

        var prefix = BuildPrefix(message);
        if (prefix != null)
        {
            // MinWidth keeps the common short tags in a tidy column, but no MaxWidth/ClipText: the
            // remaining long tags (ALERT, MENTOR) push the message across rather than being silently
            // chopped mid-word. Shortening a label is a decision to make in GetChannelLabel, where
            // it is legible - clipping is the same thing happening by accident.
            row.AddChild(new Label
            {
                Text = prefix,
                MinWidth = metrics.PrefixMinWidth,
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

        if (message.XenoWatchEntity.Valid)
        {
            var watchButton = CreateXenoWatchButton(message, metrics, textColor);
            row.AddChild(watchButton);
        }

        _messageLabel = new RichTextLabel
        {
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Top,
            LineHeightScale = StyleNano.CrtUiEnabled ? CrtLineHeightScale : metrics.LineHeightScale
        };

        // RichTextLabel resolves its font ONLY from the stylesheet - it has no FontOverride - so
        // without this class the message bodies fall back to the theme default and render
        // proportional while every label around them is the mono OSD face. That single miss is most
        // of what stopped the chat reading as a terminal, and it is invisible from the outside
        // because nothing errors and the text still appears.
        if (StyleNano.CrtUiEnabled)
        {
            _messageLabel.AddStyleClass(isAnnouncement
                ? StyleNano.StyleClassCrtChatAnnouncementText
                : StyleNano.StyleClassCrtChatText);
        }

        _messageLabel.SetMessage(ReplaceCommandLinkTags(formatted), AllowedMarkupTags, defaultColor: textColor);
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
        var followButton = CreateChatActionButton(
            Loc.GetString("cmu-chat-manager-follow-button"),
            Loc.GetString("cmu-chat-manager-follow-button-tooltip"),
            metrics,
            textColor);
        followButton.OnPressed += _ => _consoleHost.ExecuteCommand($"{CMUGhostFollowCommand.CommandName} {message.GhostFollowEntity}");
        return followButton;
    }

    private Button CreateXenoWatchButton(ChatMessage message, RowMetrics metrics, Color textColor)
    {
        var watchButton = CreateChatActionButton(
            Loc.GetString("cmu-chat-manager-xeno-watch-button"),
            Loc.GetString("cmu-chat-manager-xeno-watch-button-tooltip"),
            metrics,
            textColor);
        watchButton.OnPressed += _ => _consoleHost.ExecuteCommand($"{CMUXenoWatchCommand.CommandName} {message.XenoWatchEntity}");
        return watchButton;
    }

    private Button CreateChatActionButton(string text, string toolTip, RowMetrics metrics, Color textColor)
    {
        var buttonSize = new Vector2(metrics.FollowButtonSize, metrics.FollowButtonSize);
        var buttonColor = textColor.WithAlpha(1f);
        var button = new Button
        {
            Text = text,
            ToolTip = toolTip,
            MinSize = buttonSize,
            MaxSize = buttonSize,
            Margin = new Thickness(2, 5, 2, 0),
            ModulateSelfOverride = buttonColor,
            VerticalAlignment = VAlignment.Top,
            StyleClasses = { StyleNano.StyleClassChatGhostFollowButton }
        };

        button.Label.HorizontalExpand = true;
        button.Label.HorizontalAlignment = HAlignment.Center;
        button.Label.VerticalAlignment = VAlignment.Center;
        button.Label.Align = Label.AlignMode.Center;
        button.Label.FontColorOverride = buttonColor;
        return button;
    }

    public void SetRepeatCount(int count)
    {
        _repeatBadge.Visible = count > 1;
        _repeatBadge.Text = $"x{count}";
    }

    internal static FormattedMessage ReplaceCommandLinkTags(FormattedMessage message)
    {
        var output = new FormattedMessage(message.Count);
        foreach (var node in message)
        {
            if (node.Name == "cmdlink")
            {
                output.PushTag(new MarkupNode(
                    ChatCommandLinkTag.TagName,
                    node.Value,
                    node.Attributes,
                    node.Closing));
                continue;
            }

            output.PushTag(node);
        }

        return output;
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
        // Padding gives a row its own interior and the bottom margin separates it from the next
        // without drawing a rule; at the older, tighter values a full log read as one block of text.
        var metrics = fontSize == null
            ? new RowMetrics(4, 4, 3, 1.12f, 42, 25, 16, 10)
            : fontSize.Value switch
            {
                <= 9 => new RowMetrics(3, 3, 2, 1.08f, 34, 20, 14, 8),
                <= 11 => new RowMetrics(3, 3, 2, 1.10f, 38, 22, 15, 9),
                <= 13 => new RowMetrics(4, 4, 3, 1.12f, 40, 24, 16, 10),
                _ => new RowMetrics(4, 4, 3, 1.14f, 42, 25, 18, 11)
            };

        // No corner accent triangle under CRT, and that is mostly about space rather than taste.
        // Every row reserved 4 + AccentSize on its right edge to keep text clear of the wedge, which
        // with the pane margin and the scrollbar made the right-hand gutter about twice the width of
        // the left. The triangle was also saying what the prefix column already says twice over - in
        // words and in the same colour - so it was the obvious thing to spend.
        return StyleNano.CrtUiEnabled ? metrics with { AccentSize = 0 } : metrics;
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
            // ADM, not ADMIN: at three characters it fits the prefix column with the common tags
            // instead of filling it and leaving no gap before the text. This switch is only the
            // fallback - a message that carries Display.ChannelLabel uses that instead, and it is
            // set from the matching table in MsgChatMessage. Keep the two in step.
            ChatChannel.Admin => "ADM",
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

    /// <summary>
    ///     Saturation for a CRT row tint, or null when the player has them switched off.
    /// </summary>
    private static float? CrtTintSaturation(string setting)
    {
        return setting switch
        {
            CCVars.CMUChatRowTintFull => CrtTerminalPalette.ChatTintSaturationFull,
            CCVars.CMUChatRowTintMuted => CrtTerminalPalette.ChatTintSaturationMuted,
            _ => null
        };
    }

    /// <summary>
    ///     Which channels take a fill under CRT: the ones upstream actually tints. Local, whisper and
    ///     emotes stay on the ground - upstream's #101214 and #151515 against a #0b0c0e log are fills
    ///     on paper only, and filling them here would make the log a wall of bands.
    /// </summary>
    private static bool TakesCrtTint(ChatChannel channel)
    {
        if ((channel & ChatChannel.AdminRelated) != 0)
            return true;

        return channel is ChatChannel.Radio
            or ChatChannel.OOC
            or ChatChannel.LOOC
            or ChatChannel.Dead
            or ChatChannel.Server
            or ChatChannel.Notifications;
    }

    private static Color GetBackground(ChatMessage message, Color accent, float? tintSaturation)
    {
        // An explicit override is semantic - a xeno announcement's purple, examine echoes - so it
        // survives the CRT branch below.
        if (message.Display?.BackgroundColorOverride is { } backgroundOverride)
            return backgroundOverride;

        var channel = message.Channel;

        if (StyleNano.CrtUiEnabled)
        {
            // Announcements carry no prefix, so without a band nothing marks where one starts.
            if (IsUnlabeledRadioSystemMessage(message))
                return CrtTerminalPalette.Surface2;

            // Rebuilt from the ladder rather than using the hexes below, which are tuned for the
            // base theme's near-black log: the tint wears the prefix's own hue at Surface2's
            // luminance, so every tinted row is one step off the ground and the same step.
            if (tintSaturation is { } saturation && TakesCrtTint(channel))
                return CrtTerminalPalette.ChatRowTint(accent, saturation);

            return Color.Transparent;
        }

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
        int RepeatMinWidth,
        int FollowButtonSize,
        int AccentSize);
}
