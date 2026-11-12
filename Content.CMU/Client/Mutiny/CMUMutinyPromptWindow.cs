using System.Numerics;
using Content.Shared.CMU14.Mutiny;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Mutiny;

// invite, begin-confirm and choose-a-side all share this: a tinted header with the side badge, the
// question, then either buttons or side cards, and a draining timer when the server will answer for you
public sealed partial class CMUMutinyPromptWindow : DefaultWindow
{
    private static readonly ResPath HudRsi = new("/Textures/CMU14/Interface/mutiny_hud.rsi");

    private static readonly Color Backdrop = Color.FromHex("#14161B");
    private static readonly Color Card = Color.FromHex("#1D2027");
    private static readonly Color CardHover = Color.FromHex("#262A33");
    private static readonly Color Border = Color.FromHex("#2C303A");
    private static readonly Color Text = Color.FromHex("#E4E1DC");
    private static readonly Color Muted = Color.FromHex("#9097A3");

    private static readonly Color MutinyRed = Color.FromHex("#E0524A");
    private static readonly Color MutinyGold = Color.FromHex("#E2B64C");
    private static readonly Color LoyalBlue = Color.FromHex("#5F8CC4");
    private static readonly Color NeutralGrey = Color.FromHex("#A0A0A0");

    [Dependency] private IGameTiming _timing = default!;
    private readonly SpriteSystem _sprites;

    private const float NarrowWidth = 500;
    private const float WideWidth = 640;
    private const float BodyMargin = 16;

    private readonly CMUMutinyPromptEuiState _state;
    private readonly float _textWidth;
    private readonly ProgressBar? _timerBar;
    private readonly Label? _timerLabel;
    private readonly string _fallbackName = string.Empty;

    public event Action<CMUMutinyChoice>? OnChoice;

    public CMUMutinyPromptWindow(CMUMutinyPromptEuiState state)
    {
        IoCManager.InjectDependencies(this);
        _sprites = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        _state = state;

        Title = Loc.GetString("cmu-mutiny-window-title");
        // rich text only wraps against a max width, without one the window grows to fit a whole paragraph
        var width = state.Kind == CMUMutinyPromptKind.ChooseSide ? WideWidth : NarrowWidth;
        _textWidth = width - BodyMargin * 2;
        MinWidth = width;
        MaxWidth = width;
        Resizable = false;

        var backdrop = new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = Backdrop } };
        var root = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        backdrop.AddChild(root);
        Contents.AddChild(backdrop);

        var (accent, badge, heading, from) = state.Kind switch
        {
            CMUMutinyPromptKind.Begin => (MutinyGold, "mutineer_leader", "cmu-mutiny-begin-heading", (string?) null),
            CMUMutinyPromptKind.ChooseSide => (MutinyRed, "mutineer", "cmu-mutiny-side-heading", "cmu-mutiny-side-from"),
            _ => (MutinyRed, "mutineer", "cmu-mutiny-invite-heading", "cmu-mutiny-invite-from"),
        };

        string? fromText = null;
        if (from != null)
        {
            fromText = state.LeaderJob is { } job
                ? Loc.GetString(from + "-job", ("leader", state.LeaderName), ("job", job))
                : Loc.GetString(from, ("leader", state.LeaderName));
        }

        root.AddChild(Header(accent, badge, Loc.GetString(heading), fromText));

        var body = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(BodyMargin, 12, BodyMargin, 14),
            SeparationOverride = 8,
        };
        root.AddChild(body);

        switch (state.Kind)
        {
            case CMUMutinyPromptKind.Invite:
                body.AddChild(Paragraph(Loc.GetString("cmu-mutiny-invite-body"), Text));
                body.AddChild(Note(Loc.GetString(state.Begun ? "cmu-mutiny-invite-open-note" : "cmu-mutiny-invite-sworn-note"), accent));
                body.AddChild(Paragraph(Loc.GetString("cmu-mutiny-rules"), Muted));
                body.AddChild(Buttons(
                    (Loc.GetString(state.Begun ? "cmu-mutiny-invite-accept-open" : "cmu-mutiny-invite-accept"), CMUMutinyChoice.Accept, accent),
                    (Loc.GetString("cmu-mutiny-invite-decline"), CMUMutinyChoice.Decline, null)));
                _fallbackName = Loc.GetString("cmu-mutiny-invite-decline");
                break;

            case CMUMutinyPromptKind.Begin:
                body.AddChild(Paragraph(Loc.GetString("cmu-mutiny-begin-body"), Text));
                body.AddChild(Roster(state.Roster));
                body.AddChild(Note(Loc.GetString("cmu-mutiny-begin-warning"), MutinyRed));
                body.AddChild(Buttons(
                    (Loc.GetString("cmu-mutiny-begin-confirm"), CMUMutinyChoice.Accept, MutinyRed),
                    (Loc.GetString("cmu-mutiny-begin-cancel"), CMUMutinyChoice.Decline, null)));
                break;

            case CMUMutinyPromptKind.ChooseSide:
                body.AddChild(Paragraph(Loc.GetString("cmu-mutiny-side-body"), Text));
                var cards = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
                var synth = state.Disabled.Contains(CMUMutinyChoice.Mutineer);
                cards.AddChild(SideCard(CMUMutinyChoice.Mutineer, "mutineer", MutinyRed,
                    Loc.GetString("cmu-mutiny-side-mutineer"),
                    Loc.GetString(synth ? "cmu-mutiny-side-mutineer-synth" : "cmu-mutiny-side-mutineer-desc"),
                    !synth));
                cards.AddChild(SideCard(CMUMutinyChoice.Loyalist, "loyalist", LoyalBlue,
                    Loc.GetString("cmu-mutiny-side-loyalist-name"),
                    Loc.GetString("cmu-mutiny-side-loyalist-desc"),
                    true));
                cards.AddChild(SideCard(CMUMutinyChoice.NonCombatant, "noncombatant", NeutralGrey,
                    Loc.GetString("cmu-mutiny-side-noncombatant-name"),
                    Loc.GetString("cmu-mutiny-side-noncombatant-desc"),
                    true));
                body.AddChild(cards);
                body.AddChild(Paragraph(Loc.GetString("cmu-mutiny-rules"), Muted));
                _fallbackName = Loc.GetString("cmu-mutiny-side-noncombatant-name");
                break;
        }

        if (state.Deadline != null && state.Duration > TimeSpan.Zero)
        {
            _timerBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = (float) state.Duration.TotalSeconds,
                MinHeight = 4,
                BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Border },
                ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = accent },
            };
            _timerLabel = new Label { FontColorOverride = Muted, HorizontalAlignment = HAlignment.Right };
            body.AddChild(_timerLabel);
            root.AddChild(_timerBar);
            UpdateTimer();
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (_timerBar == null || _timerLabel == null || _state.Deadline is not { } deadline)
            return;

        var left = deadline - _timing.CurTime;
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;

        _timerBar.Value = (float) left.TotalSeconds;
        _timerLabel.Text = Loc.GetString("cmu-mutiny-timer", ("seconds", (int) Math.Ceiling(left.TotalSeconds)), ("fallback", _fallbackName));
    }

    private Control Header(Color accent, string badge, string heading, string? from)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(Backdrop, accent, 0.16f),
                BorderColor = accent,
                BorderThickness = new Thickness(0, 0, 0, 2),
            },
        };

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(14, 12),
            SeparationOverride = 12,
        };
        panel.AddChild(row);

        row.AddChild(Badge(badge, 5));

        var text = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalAlignment = VAlignment.Center };
        text.AddChild(new Label
        {
            Text = heading.ToUpperInvariant(),
            StyleClasses = { "LabelHeadingBigger" },
            FontColorOverride = Color.InterpolateBetween(Text, accent, 0.35f),
        });

        // header margins plus the 40px badge and its gap
        if (from != null)
            text.AddChild(Paragraph(from, Text, _textWidth + BodyMargin * 2 - 28 - 40 - 12));

        row.AddChild(text);
        return panel;
    }

    // the badge art sits in the middle 8x8 of a 16x16 hud state, so crop to it before scaling up
    private Control Badge(string state, int scale)
    {
        // RSI frames are already atlas slices and an atlas of an atlas won't draw, so crop the source
        var texture = _sprites.Frame0(new SpriteSpecifier.Rsi(HudRsi, state));
        var source = texture;
        var origin = Vector2.Zero;
        if (texture is AtlasTexture atlas)
        {
            source = atlas.SourceTexture;
            origin = atlas.SubRegion.TopLeft;
        }

        return new TextureRect
        {
            Texture = new AtlasTexture(source, UIBox2.FromDimensions(origin + new Vector2(4, 4), new Vector2(8, 8))),
            TextureScale = new Vector2(scale, scale),
            Stretch = TextureRect.StretchMode.KeepCentered,
            MinSize = new Vector2(8 * scale, 8 * scale),
            VerticalAlignment = VAlignment.Center,
        };
    }

    private RichTextLabel Paragraph(string text, Color color, float? width = null)
    {
        var msg = new FormattedMessage();
        msg.PushColor(color);
        msg.AddText(text);
        msg.Pop();

        var label = new RichTextLabel { MaxWidth = width ?? _textWidth };
        label.SetMessage(msg);
        return label;
    }

    private Control Note(string text, Color accent)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(Backdrop, accent, 0.08f),
                BorderColor = accent,
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(10, 6),
            },
        };
        panel.AddChild(Paragraph(text, Text, _textWidth - 22));
        return panel;
    }

    private Control Roster(List<CMUMutinyRosterEntry> roster)
    {
        var box = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };
        box.AddChild(new Label
        {
            Text = Loc.GetString("cmu-mutiny-begin-roster", ("count", roster.Count)),
            FontColorOverride = MutinyGold,
        });

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Card,
                BorderColor = Border,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6),
            },
        };
        box.AddChild(panel);

        if (roster.Count == 0)
        {
            panel.AddChild(Paragraph(Loc.GetString("cmu-mutiny-begin-roster-empty"), Muted, _textWidth - 18));
            return box;
        }

        // a scroll box has no height of its own, so size it to the rows up to a cap
        var scroll = new ScrollContainer { HScrollEnabled = false, MinHeight = Math.Min(roster.Count * 24, 170), MaxHeight = 170 };
        var list = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2 };
        scroll.AddChild(list);
        panel.AddChild(scroll);

        foreach (var entry in roster)
        {
            var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8, Margin = new Thickness(0, 0, 12, 0) };
            var badge = Badge("mutineer", 2);
            badge.Modulate = entry.Alive ? Color.White : Color.White.WithAlpha(0.35f);
            row.AddChild(badge);
            row.AddChild(new Label
            {
                Text = entry.Name,
                FontColorOverride = entry.Alive ? Text : Muted,
                HorizontalExpand = true,
                ClipText = true,
            });

            var job = entry.Job ?? string.Empty;
            if (!entry.Alive)
                job = string.IsNullOrEmpty(job) ? Loc.GetString("cmu-mutiny-begin-dead") : $"{job} · {Loc.GetString("cmu-mutiny-begin-dead")}";

            row.AddChild(new Label { Text = job, FontColorOverride = entry.Alive ? Muted : MutinyRed });
            list.AddChild(row);
        }

        return box;
    }

    private Control Buttons(params (string Text, CMUMutinyChoice Choice, Color? Accent)[] buttons)
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            SeparationOverride = 8,
            Margin = new Thickness(0, 4, 0, 0),
        };

        foreach (var (text, choice, accent) in buttons)
        {
            var button = new Button { Text = text, MinSize = new Vector2(132, 34) };
            StyleButton(button, accent);
            button.OnPressed += _ => OnChoice?.Invoke(choice);
            row.AddChild(button);
        }

        return row;
    }

    private static void StyleButton(ContainerButton button, Color? accent)
    {
        var bg = accent is { } a ? Color.InterpolateBetween(Backdrop, a, 0.55f) : Card;
        var hover = accent is { } h ? Color.InterpolateBetween(Backdrop, h, 0.75f) : CardHover;
        var border = accent ?? Border;

        StyleBoxFlat Box(Color color) => new()
        {
            BackgroundColor = color,
            BorderColor = border,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 14,
            ContentMarginRightOverride = 14,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };

        button.StyleBoxOverride = Box(bg);
        button.OnMouseEntered += _ => button.StyleBoxOverride = Box(hover);
        button.OnMouseExited += _ => button.StyleBoxOverride = Box(bg);
    }

    private Control SideCard(CMUMutinyChoice choice, string badge, Color accent, string name, string desc, bool enabled)
    {
        var card = new ContainerButton
        {
            HorizontalExpand = true,
            Disabled = !enabled,
            MinHeight = 170,
        };

        StyleBoxFlat Box(Color bg, Color border) => new()
        {
            BackgroundColor = bg,
            BorderColor = border,
            BorderThickness = new Thickness(1, 1, 1, 3),
            Padding = new Thickness(10, 12),
        };

        var idle = Box(Card, enabled ? Color.InterpolateBetween(Border, accent, 0.45f) : Border);
        var hot = Box(Color.InterpolateBetween(Card, accent, 0.18f), accent);
        card.StyleBoxOverride = idle;

        if (enabled)
        {
            card.OnMouseEntered += _ => card.StyleBoxOverride = hot;
            card.OnMouseExited += _ => card.StyleBoxOverride = idle;
            card.OnPressed += _ => OnChoice?.Invoke(choice);
        }

        var content = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Modulate = enabled ? Color.White : Color.White.WithAlpha(0.4f),
        };
        content.AddChild(Badge(badge, 6));

        content.AddChild(new Label
        {
            Text = name.ToUpperInvariant(),
            FontColorOverride = accent,
            HorizontalAlignment = HAlignment.Center,
        });

        var msg = new FormattedMessage();
        msg.PushColor(enabled ? Text : Muted);
        msg.AddText(desc);
        msg.Pop();
        // three cards share the row, minus their padding and the gaps between them
        var description = new RichTextLabel { HorizontalAlignment = HAlignment.Center, MaxWidth = (_textWidth - 16) / 3 - 22 };
        description.SetMessage(msg);
        content.AddChild(description);

        card.AddChild(content);
        return card;
    }
}
