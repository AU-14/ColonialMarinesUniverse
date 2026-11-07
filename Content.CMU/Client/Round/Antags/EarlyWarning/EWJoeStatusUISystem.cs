using System.Numerics;
using System.Text;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// The Early Warning Joe's own link to the signal: its level, the directives (objectives) for the next one,
/// its thralls and whether the river holds a copy of it, built from the owner-only
/// <see cref="EWJoeStatusComponent"/>. Opens only from the client-only "Signal Status" action. Text pass B:
/// the ladder no longer appears in the character window (the upstream list skips it on the server, and
/// nothing is injected here), and the window reads as a machine diagnostic rather than a stock list.
/// </summary>
public sealed class EWJoeStatusUISystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;

    private static readonly Color Ember = Color.FromHex("#ff5a1f");
    private static readonly Color Flare = Color.FromHex("#ffb066");
    private static readonly Color Dim = Color.FromHex("#a07a64");
    private static readonly Color Done = Color.FromHex("#7fd18b");

    private const int BarWidth = 10;

    private DefaultWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeOpenStatusActionEvent>(OnOpenStatus);
        SubscribeLocalEvent<EWJoeStatusComponent, AfterAutoHandleStateEvent>(OnStatusState);
        SubscribeLocalEvent<EWJoeStatusComponent, ComponentShutdown>(OnStatusShutdown);
        // the owner-only status is not removed when the player leaves the body (it just stops updating), so
        // ghosting or a body swap closes the window here
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnDetached(LocalPlayerDetachedEvent args)
    {
        _window?.Close();
        _window = null;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
        _window = null;
    }

    private void OnOpenStatus(Entity<EWJoeComponent> ent, ref EWJoeOpenStatusActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Owner != _player.LocalEntity)
            return;

        if (_window is { IsOpen: true })
        {
            _window.Close();
            return;
        }

        _window = new DefaultWindow
        {
            Title = Loc.GetString("cmu-ew-joe-status-title"),
            MinSize = new Vector2(460, 260),
        };
        _window.OnClose += () => _window = null;
        Refresh(ent.Owner);
        _window.OpenCentered();
    }

    private void OnStatusState(Entity<EWJoeStatusComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner == _player.LocalEntity)
            Refresh(ent.Owner);
    }

    private void OnStatusShutdown(Entity<EWJoeStatusComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        _window?.Close();
        _window = null;
    }

    private void Refresh(EntityUid uid)
    {
        if (_window == null)
            return;

        _window.Contents.RemoveAllChildren();
        if (TryComp(uid, out EWJoeStatusComponent? status))
            _window.Contents.AddChild(BuildPanel(status));
    }

    private Control BuildPanel(EWJoeStatusComponent status)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10),
        };

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#0d0605"),
                BorderColor = Ember,
                BorderThickness = new Thickness(2),
            },
        };
        panel.AddChild(box);

        box.AddChild(Line(Loc.GetString("cmu-ew-joe-status-link"), Ember));
        box.AddChild(Line(Loc.GetString("cmu-ew-joe-status-level",
            ("level", status.Level),
            ("name", string.IsNullOrEmpty(status.LevelName) ? string.Empty : Loc.GetString(status.LevelName))), Flare));

        box.AddChild(Line(Loc.GetString(status.RespawnReady ? "cmu-ew-joe-status-respawn" : "cmu-ew-joe-status-no-respawn"),
            status.RespawnReady ? Done : Dim));

        box.AddChild(Line(status.Thralls.Count > 0
            ? Loc.GetString("cmu-ew-joe-status-thralls", ("names", string.Join(", ", status.Thralls)))
            : Loc.GetString("cmu-ew-joe-status-no-thralls"), status.Thralls.Count > 0 ? Flare : Dim));

        box.AddChild(Line(new string('=', 44), Ember));

        if (status.FinalLevel)
        {
            box.AddChild(Line(Loc.GetString("cmu-ew-joe-status-final"), Flare));
            return panel;
        }

        if (status.Objectives.Count == 0)
        {
            box.AddChild(Line(Loc.GetString("cmu-ew-joe-status-none"), Dim));
            return panel;
        }

        var required = Math.Max(1, status.CreditRequired);
        box.AddChild(Line(Loc.GetString("cmu-ew-joe-status-credit",
            ("bar", Bar(Math.Min(1f, status.CreditEarned / (float) required))),
            ("earned", status.CreditEarned),
            ("required", status.CreditRequired)), Flare));

        if (status.RequireAssimilation)
            box.AddChild(Line(Loc.GetString("cmu-ew-joe-status-console"), Flare));

        if (status.DoubleCredit)
            box.AddChild(Line(Loc.GetString("cmu-ew-joe-status-double"), Flare));

        for (var i = 0; i < status.Objectives.Count; i++)
        {
            var entry = status.Objectives[i];
            var state = entry.Completed
                ? Loc.GetString("cmu-ew-joe-status-done")
                : $"[{Bar(entry.Progress)}] {(int) MathF.Round(entry.Progress * 100)}%";

            box.AddChild(Line(Loc.GetString("cmu-ew-joe-status-directive",
                ("number", (i + 1).ToString("00")),
                ("title", entry.Title)), entry.Completed ? Done : Flare, top: 6));
            box.AddChild(Line(state, entry.Completed ? Done : Ember));
            box.AddChild(Line(entry.Description, Dim));
        }

        return panel;
    }

    /// <summary>A text progress bar, <see cref="BarWidth"/> cells wide.</summary>
    public static string Bar(float progress)
    {
        var filled = (int) MathF.Round(Math.Clamp(progress, 0f, 1f) * BarWidth);
        var bar = new StringBuilder(BarWidth);
        bar.Append('#', filled);
        bar.Append('-', BarWidth - filled);
        return bar.ToString();
    }

    private static Control Line(string text, Color color, float top = 1)
    {
        // the mono tag sets its own font, so weight comes from colour and capitals, not [bold]
        var markup = $"[mono][color={color.ToHex()}]{FormattedMessage.EscapeText(text)}[/color][/mono]";

        var label = new RichTextLabel { Margin = new Thickness(0, top, 0, 1) };
        label.SetMessage(FormattedMessage.FromMarkupPermissive(markup));
        return label;
    }
}
