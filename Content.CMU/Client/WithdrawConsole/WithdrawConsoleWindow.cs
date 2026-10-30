using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.AllianceConsole;
using Content.Shared.CMU14.WithdrawConsole;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.WithdrawConsole;

/// <summary>
///     The withdraw console's screen, drawn in the CRT theme: the two-ID authorization, the withdrawal countdown, and
///     the withdraw, cancel and stalemate controls.
/// </summary>
public sealed class WithdrawConsoleWindow : DefaultWindow
{
    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    private const int RequiredIds = 2;

    public readonly ConfirmButton ToggleWithdrawButton;
    public readonly ConfirmButton CancelButton;
    public readonly ConfirmButton StalemateButton;

    private readonly IPrototypeManager _prototypes;

    private readonly Label _heading;
    private readonly Label _statusTag;
    private readonly PanelContainer _statusPanel;
    private readonly Label _status;
    private readonly Label _swipes;
    private readonly PanelContainer[] _slots = new PanelContainer[RequiredIds];
    private readonly Label _timer;
    private readonly Label _timerCaption;
    private readonly Label _stalemate;

    public WithdrawConsoleWindow()
    {
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        var cache = IoCManager.Resolve<IResourceCache>();
        var small = cache.GetFont(MonoRegular, 10);
        var body = cache.GetFont(MonoRegular, 12);
        var bold = cache.GetFont(MonoBold, 12);
        var title = cache.GetFont(MonoBold, 15);
        var huge = cache.GetFont(MonoBold, 28);

        Title = Loc.GetString("withdraw-console-ui-title");
        MinSize = new Vector2(460, 420);
        SetSize = new Vector2(480, 440);

        var screen = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface0 },
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(10),
        };
        screen.AddChild(root);

        // Header strip: which side's console this is, and where it stands.
        var header = Strip(CrtTerminalPalette.Surface3);
        var headerRow = Row();
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        _heading = Text(Loc.GetString("withdraw-console-ui-crt-heading"), title, CrtTerminalPalette.TextBright);
        _heading.ClipText = true;
        titles.AddChild(_heading);
        var subheading = Text(Loc.GetString("withdraw-console-ui-crt-subheading"), small, CrtTerminalPalette.TextDim);
        subheading.ClipText = true;
        titles.AddChild(subheading);
        headerRow.AddChild(titles);
        _statusPanel = new PanelContainer { VerticalAlignment = VAlignment.Center };
        _statusTag = Text(string.Empty, bold, CrtTerminalPalette.Void);
        _statusTag.Margin = new Thickness(8, 3);
        _statusPanel.AddChild(_statusTag);
        headerRow.AddChild(_statusPanel);
        header.AddChild(headerRow);
        root.AddChild(header);

        _status = Text(string.Empty, body, CrtTerminalPalette.Text);
        _status.ClipText = true;
        _status.Margin = new Thickness(2, 0);
        root.AddChild(_status);

        // Authorization: one slot per ID that has to be swiped.
        var auth = Strip(CrtTerminalPalette.Surface2);
        var authColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        authColumn.AddChild(Text(Loc.GetString("withdraw-console-ui-crt-auth"), small, CrtTerminalPalette.TextDim));
        var slotRow = Row();
        for (var i = 0; i < RequiredIds; i++)
        {
            _slots[i] = new PanelContainer { MinSize = new Vector2(36, 12), VerticalAlignment = VAlignment.Center };
            slotRow.AddChild(_slots[i]);
        }

        _swipes = Text(string.Empty, bold, CrtTerminalPalette.TextBright);
        _swipes.ClipText = true;
        _swipes.HorizontalExpand = true;
        _swipes.Margin = new Thickness(4, 0, 0, 0);
        slotRow.AddChild(_swipes);
        authColumn.AddChild(slotRow);
        auth.AddChild(authColumn);
        root.AddChild(auth);

        // Countdown.
        var clock = Strip(CrtTerminalPalette.Surface1);
        clock.VerticalExpand = true;
        var clockColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
        };
        _timerCaption = Text(Loc.GetString("withdraw-console-ui-crt-timer"), small, CrtTerminalPalette.TextDim);
        _timerCaption.HorizontalAlignment = HAlignment.Center;
        clockColumn.AddChild(_timerCaption);
        _timer = Text("--:--", huge, CrtTerminalPalette.TextDim);
        _timer.HorizontalAlignment = HAlignment.Center;
        clockColumn.AddChild(_timer);
        _stalemate = Text(string.Empty, small, CrtTerminalPalette.Caution);
        _stalemate.HorizontalAlignment = HAlignment.Center;
        clockColumn.AddChild(_stalemate);
        clock.AddChild(clockColumn);
        root.AddChild(clock);

        // Controls.
        ToggleWithdrawButton = Action("withdraw-console-ui-initiate", CmuButtonStyles.Variant.Danger, bold);
        CancelButton = Action("withdraw-console-ui-cancel", CmuButtonStyles.Variant.Caution, bold);
        StalemateButton = Action("withdraw-console-ui-stalemate-toggle", CmuButtonStyles.Variant.Neutral, bold);
        CancelButton.Visible = false;

        var controls = Row();
        controls.AddChild(ToggleWithdrawButton);
        controls.AddChild(CancelButton);
        controls.AddChild(StalemateButton);
        root.AddChild(controls);

        // Reading surface, so no roll bar or grain.
        var tube = new Control { HorizontalExpand = true, VerticalExpand = true };
        tube.AddChild(screen);
        tube.AddChild(new CrtScreenControl
        {
            Source = screen,
            Phosphor = CrtTerminalPalette.Accent,
            Roll = false,
            Grain = false,
        });
        Contents.AddChild(tube);
    }

    public void UpdateState(WithdrawConsoleBuiState state)
    {
        var side = AllianceConsoleComponent.ResolveFaction(_prototypes, state.Faction);
        _heading.Text = Loc.GetString("withdraw-console-ui-crt-heading-faction",
            ("faction", (side?.Name ?? state.Faction).ToUpperInvariant()));

        var swiped = state.IsUnlocked ? RequiredIds : Math.Clamp(state.SwipedIdCount, 0, RequiredIds);
        for (var i = 0; i < RequiredIds; i++)
        {
            _slots[i].PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = i < swiped ? CrtTerminalPalette.Accent : CrtTerminalPalette.Surface4,
            };
        }

        _swipes.Text = state.IsUnlocked
            ? Loc.GetString("withdraw-console-ui-unlocked")
            : Loc.GetString("withdraw-console-ui-swipe-count", ("count", state.SwipedIdCount));
        _swipes.FontColorOverride = state.IsUnlocked ? CrtTerminalPalette.Accent : CrtTerminalPalette.TextBright;

        _stalemate.Text = state.StalemateToggled ? Loc.GetString("withdraw-console-ui-crt-stalemate-proposed") : string.Empty;

        if (state.RoundEndTriggered)
        {
            SetStatus("withdraw-console-ui-crt-status-concluded", CrtTerminalPalette.TextDim, "withdraw-console-ui-round-ended");
            SetTimer(null);
            ToggleWithdrawButton.Disabled = true;
            CancelButton.Disabled = true;
            CancelButton.Visible = false;
            ToggleWithdrawButton.Visible = true;
            StalemateButton.Disabled = true;
            return;
        }

        if (state.WithdrawActive)
            SetStatus("withdraw-console-ui-crt-status-active", CrtTerminalPalette.Alert, "withdraw-console-ui-status-active");
        else if (state.IsUnlocked)
            SetStatus("withdraw-console-ui-crt-status-ready", CrtTerminalPalette.Caution, "withdraw-console-ui-status-ready");
        else
            SetStatus("withdraw-console-ui-crt-status-locked", CrtTerminalPalette.Accent, "withdraw-console-ui-status-locked");

        SetTimer(state.WithdrawActive ? state.SecondsRemaining : null);

        ToggleWithdrawButton.Visible = !state.WithdrawActive;
        ToggleWithdrawButton.Disabled = !state.IsUnlocked || state.WithdrawActive;
        CancelButton.Visible = state.WithdrawActive;
        CancelButton.Disabled = !state.WithdrawActive || !state.CanCancel;

        StalemateButton.Disabled = !state.IsUnlocked;
        StalemateButton.Text = Loc.GetString(state.StalemateToggled
            ? "withdraw-console-ui-stalemate-cancel"
            : "withdraw-console-ui-stalemate-toggle").ToUpperInvariant();
    }

    private void SetStatus(string tag, Color color, string status)
    {
        _statusTag.Text = Loc.GetString(tag);
        _statusPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = color };
        _status.Text = Loc.GetString(status);
    }

    private void SetTimer(double? seconds)
    {
        if (seconds is not { } value)
        {
            _timer.Text = "--:--";
            _timer.FontColorOverride = CrtTerminalPalette.TextDim;
            return;
        }

        var remaining = TimeSpan.FromSeconds(Math.Max(0, value));
        _timer.Text = $"{(int) remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        _timer.FontColorOverride = CrtTerminalPalette.Alert;
    }

    private static ConfirmButton Action(string text, CmuButtonStyles.Variant variant, Font font)
    {
        var button = new ConfirmButton
        {
            Text = Loc.GetString(text).ToUpperInvariant(),
            ConfirmationText = Loc.GetString("withdraw-console-ui-confirm"),
            Disabled = true,
            HorizontalExpand = true,
            MinHeight = 34,
        };
        CmuButtonStyles.Apply(button, variant);
        button.Label.FontOverride = font;
        return button;
    }

    private static PanelContainer Strip(Color color)
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            },
        };
    }

    private static BoxContainer Row()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
    }

    private static Label Text(string text, Font font, Color color)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color };
    }
}
