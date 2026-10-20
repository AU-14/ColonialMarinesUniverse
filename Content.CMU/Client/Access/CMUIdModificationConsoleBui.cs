using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared._RMC14.Marines.Access;
using Content.Shared._RMC14.UserInterface;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Access;

/// <summary>
///     The identification computer, drawn in the CRT theme. Sends the same messages as RMC's console UI, so the
///     server side is unchanged: sign in with an authorized card, insert the card to change, then edit its access,
///     job and squad from the tabs.
/// </summary>
public sealed class CMUIdModificationConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey), IRefreshableBui
{
    private CMUIdModificationConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUIdModificationConsoleWindow>();
        _window.OnSignIn += () => SendPredictedMessage(new IdModificationConsoleSignInBuiMsg());
        _window.OnTargetCard += () => SendPredictedMessage(new IdModificationConsoleSignInTargetBuiMsg());
        _window.OnAccess += (access, add) => SendPredictedMessage(new IdModificationConsoleAccessChangeBuiMsg(access, add));
        _window.OnMultipleAccess += (type, group) =>
            SendPredictedMessage(new IdModificationConsoleMultipleAccessChangeBuiMsg(type, group));
        _window.OnIff += revoke => SendPredictedMessage(new IdModificationConsoleIFFChangeBuiMsg(revoke));
        _window.OnJob += job => SendPredictedMessage(new IdModificationConsoleJobChangeBuiMsg(job));
        _window.OnTerminate += () => SendPredictedMessage(new IdModificationConsoleTerminateConfirmBuiMsg());
        _window.OnSquad += squad => SendPredictedMessage(new IdModificationConsoleAssignSquadMsg(squad));
        Refresh();
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true } ||
            !EntMan.TryGetComponent(Owner, out IdModificationConsoleComponent? console))
        {
            return;
        }

        var containers = EntMan.System<ContainerSystem>();
        var privileged = containers.TryGetContainer(Owner, console.PrivilegedIdSlot, out var p) && p is ContainerSlot ps
            ? ps.ContainedEntity
            : null;
        var target = containers.TryGetContainer(Owner, console.TargetIdSlot, out var t) && t is ContainerSlot ts
            ? ts.ContainedEntity
            : null;

        EntMan.TryGetComponent(privileged, out IdCardComponent? privilegedCard);
        EntMan.TryGetComponent(target, out IdCardComponent? targetCard);
        EntMan.TryGetComponent(target, out AccessComponent? targetAccess);
        _window.Update(console, privileged != null, privilegedCard, target != null, targetCard, targetAccess);
    }
}

public sealed class CMUIdModificationConsoleWindow : DefaultWindow
{
    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    private enum Tab
    {
        Access,
        Jobs,
        Squads,
    }

    public event Action? OnSignIn;
    public event Action? OnTargetCard;
    public event Action<ProtoId<AccessLevelPrototype>, bool>? OnAccess;
    public event Action<string, string>? OnMultipleAccess;
    public event Action<bool>? OnIff;
    public event Action<ProtoId<AccessGroupPrototype>>? OnJob;
    public event Action? OnTerminate;
    public event Action<NetEntity?>? OnSquad;

    private readonly IPrototypeManager _prototypes;
    private readonly Font _small;
    private readonly Font _body;
    private readonly Font _bold;
    private readonly Font _title;

    private readonly Label _subheading;
    private readonly Label _statusTag;
    private readonly PanelContainer _statusPanel;
    private readonly Label _authName;
    private readonly Button _authButton;
    private readonly Label _targetName;
    private readonly Label _targetJob;
    private readonly Button _targetButton;

    private readonly BoxContainer _tabs;
    private readonly Dictionary<Tab, Button> _tabButtons = new();
    private readonly BoxContainer _actions;
    private readonly Label _groupsCaption;
    private readonly BoxContainer _groups;
    private readonly Label _entriesCaption;
    private readonly BoxContainer _entryActions;
    private readonly GridContainer _entries;
    private readonly BoxContainer _workspace;
    private readonly Label _placeholder;
    private readonly Label _footer;

    private Tab _tab = Tab.Access;
    private string _accessGroup = string.Empty;
    private string _jobGroup = string.Empty;

    private IdModificationConsoleComponent? _console;
    private AccessComponent? _targetAccess;

    public CMUIdModificationConsoleWindow()
    {
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        var cache = IoCManager.Resolve<IResourceCache>();
        _small = cache.GetFont(MonoRegular, 10);
        _body = cache.GetFont(MonoRegular, 12);
        _bold = cache.GetFont(MonoBold, 12);
        _title = cache.GetFont(MonoBold, 15);

        Title = Loc.GetString("cmu-id-console-title");
        MinSize = new Vector2(720, 560);
        SetSize = new Vector2(780, 640);

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

        // Header strip: what this console is, and whether someone's signed in.
        var header = Strip(CrtTerminalPalette.Surface3);
        var headerRow = Row();
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var heading = Text(Loc.GetString("cmu-id-console-heading"), _title, CrtTerminalPalette.TextBright);
        heading.ClipText = true;
        titles.AddChild(heading);
        _subheading = Text(string.Empty, _small, CrtTerminalPalette.TextDim);
        _subheading.ClipText = true;
        titles.AddChild(_subheading);
        headerRow.AddChild(titles);
        _statusPanel = new PanelContainer { VerticalAlignment = VAlignment.Center };
        _statusTag = Text(string.Empty, _bold, CrtTerminalPalette.Void);
        _statusTag.Margin = new Thickness(8, 3);
        _statusPanel.AddChild(_statusTag);
        headerRow.AddChild(_statusPanel);
        header.AddChild(headerRow);
        root.AddChild(header);

        // The two card slots.
        var slots = new GridContainer { Columns = 2, HSeparationOverride = 8, HorizontalExpand = true };
        slots.AddChild(Slot("cmu-id-console-slot-auth", out _authName, out _, out _authButton));
        slots.AddChild(Slot("cmu-id-console-slot-target", out _targetName, out _targetJob, out _targetButton));
        _authButton.OnPressed += _ => OnSignIn?.Invoke();
        _targetButton.OnPressed += _ => OnTargetCard?.Invoke();
        root.AddChild(slots);

        // Tabs on the left, the tab's card-wide actions on the right.
        var tabRow = Row();
        _tabs = Row();
        _tabs.SeparationOverride = 4;
        _tabs.HorizontalExpand = true;
        foreach (var tab in Enum.GetValues<Tab>())
        {
            var button = new Button
            {
                Text = Loc.GetString($"cmu-id-console-tab-{tab.ToString().ToLowerInvariant()}"),
                ToggleMode = true,
                MinWidth = 96,
            };
            CmuButtonStyles.Apply(button, CmuButtonStyles.Variant.Neutral);
            button.Label.FontOverride = _bold;
            var captured = tab;
            button.OnPressed += _ =>
            {
                _tab = captured;
                Rebuild();
            };
            _tabs.AddChild(button);
            _tabButtons[tab] = button;
        }

        tabRow.AddChild(_tabs);
        _actions = Row();
        _actions.SeparationOverride = 4;
        tabRow.AddChild(_actions);
        root.AddChild(tabRow);

        // Workspace: groups on the left, what's in the picked group on the right.
        var body = new Control { VerticalExpand = true, HorizontalExpand = true };
        _workspace = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var left = Panel(CrtTerminalPalette.Surface1);
        left.MinWidth = 230;
        var leftColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        _groupsCaption = Text(string.Empty, _small, CrtTerminalPalette.TextDim);
        leftColumn.AddChild(_groupsCaption);
        var leftScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _groups = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, HorizontalExpand = true };
        leftScroll.AddChild(_groups);
        leftColumn.AddChild(leftScroll);
        left.AddChild(leftColumn);
        _workspace.AddChild(left);

        var right = Panel(CrtTerminalPalette.Surface1);
        right.HorizontalExpand = true;
        var rightColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        var rightHeader = Row();
        _entriesCaption = Text(string.Empty, _small, CrtTerminalPalette.TextDim);
        _entriesCaption.HorizontalExpand = true;
        _entriesCaption.VerticalAlignment = VAlignment.Center;
        _entriesCaption.ClipText = true;
        rightHeader.AddChild(_entriesCaption);
        _entryActions = Row();
        _entryActions.SeparationOverride = 4;
        rightHeader.AddChild(_entryActions);
        rightColumn.AddChild(rightHeader);
        var rightScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _entries = new GridContainer { Columns = 2, HSeparationOverride = 4, VSeparationOverride = 4, HorizontalExpand = true };
        rightScroll.AddChild(_entries);
        rightColumn.AddChild(rightScroll);
        right.AddChild(rightColumn);
        _workspace.AddChild(right);
        body.AddChild(_workspace);

        _placeholder = Text(string.Empty, _body, CrtTerminalPalette.TextDim);
        _placeholder.HorizontalAlignment = HAlignment.Center;
        _placeholder.VerticalAlignment = VAlignment.Center;
        body.AddChild(_placeholder);
        root.AddChild(body);

        var footer = Strip(CrtTerminalPalette.Surface1);
        _footer = Text(string.Empty, _small, CrtTerminalPalette.TextDim);
        _footer.ClipText = true;
        footer.AddChild(_footer);
        root.AddChild(footer);

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

    public void Update(
        IdModificationConsoleComponent console,
        bool hasPrivileged,
        IdCardComponent? privilegedCard,
        bool hasTarget,
        IdCardComponent? targetCard,
        AccessComponent? targetAccess)
    {
        _console = console;
        _targetAccess = targetAccess;

        _subheading.Text = Loc.GetString("cmu-id-console-subheading", ("faction", console.SquadGroup.ToUpperInvariant()));

        var (tag, color) = console.Authenticated
            ? ("cmu-id-console-status-authorized", CrtTerminalPalette.Accent)
            : hasPrivileged
                ? ("cmu-id-console-status-denied", CrtTerminalPalette.Alert)
                : ("cmu-id-console-status-locked", CrtTerminalPalette.Caution);
        _statusTag.Text = Loc.GetString(tag);
        _statusPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = color };

        _authName.Text = hasPrivileged
            ? (privilegedCard?.FullName ?? Loc.GetString("cmu-id-console-unknown")).ToUpperInvariant()
            : Loc.GetString("cmu-id-console-no-card");
        _authName.FontColorOverride = hasPrivileged ? CrtTerminalPalette.TextBright : CrtTerminalPalette.TextDim;
        _authButton.Text = Loc.GetString(console.Authenticated
            ? "cmu-id-console-log-out"
            : hasPrivileged
                ? "cmu-id-console-eject"
                : "cmu-id-console-sign-in");

        _targetName.Text = hasTarget
            ? (targetCard?.FullName ?? Loc.GetString("cmu-id-console-unknown")).ToUpperInvariant()
            : Loc.GetString("cmu-id-console-no-card");
        _targetName.FontColorOverride = hasTarget ? CrtTerminalPalette.TextBright : CrtTerminalPalette.TextDim;
        _targetJob.Text = hasTarget ? (targetCard?.LocalizedJobTitle ?? string.Empty).ToUpperInvariant() : string.Empty;
        _targetButton.Text = Loc.GetString(hasTarget ? "cmu-id-console-eject" : "cmu-id-console-insert");

        var ready = console.Authenticated && targetCard != null && targetAccess != null;
        _tabs.Visible = ready;
        _actions.Visible = ready;
        _workspace.Visible = ready;
        _placeholder.Visible = !ready;
        _placeholder.Text = Loc.GetString(!console.Authenticated
            ? "cmu-id-console-placeholder-sign-in"
            : "cmu-id-console-placeholder-insert");
        _footer.Text = Loc.GetString(ready ? $"cmu-id-console-footer-{_tab.ToString().ToLowerInvariant()}" : "cmu-id-console-footer-idle");

        if (ready)
            Rebuild();
    }

    /// <summary>Redraws the open tab from the last state. Lists are short, so it's cheaper to rebuild than diff.</summary>
    private void Rebuild()
    {
        if (_console is not { } console || _targetAccess is not { } access)
            return;

        // The button style replaces the pressed look, so the open tab is shown by colour instead.
        foreach (var (tab, button) in _tabButtons)
        {
            button.Pressed = tab == _tab;
            CmuButtonStyles.Apply(button, tab == _tab ? CmuButtonStyles.Variant.Affirm : CmuButtonStyles.Variant.Neutral);
            button.Label.FontOverride = _bold;
        }

        _actions.RemoveAllChildren();
        _groups.RemoveAllChildren();
        _entryActions.RemoveAllChildren();
        _entries.RemoveAllChildren();
        _footer.Text = Loc.GetString($"cmu-id-console-footer-{_tab.ToString().ToLowerInvariant()}");

        switch (_tab)
        {
            case Tab.Access:
                BuildAccess(console, access);
                break;
            case Tab.Jobs:
                BuildJobs(console, access);
                break;
            case Tab.Squads:
                BuildSquads(console);
                break;
        }
    }

    private void BuildAccess(IdModificationConsoleComponent console, AccessComponent access)
    {
        var iff = Action(console.HasIFF ? "cmu-id-console-iff-revoke" : "cmu-id-console-iff-grant",
            console.HasIFF ? CmuButtonStyles.Variant.Caution : CmuButtonStyles.Variant.Affirm);
        iff.OnPressed += _ => OnIff?.Invoke(console.HasIFF);
        _actions.AddChild(iff);
        var grantAll = Action("cmu-id-console-grant-all", CmuButtonStyles.Variant.Affirm);
        grantAll.OnPressed += _ => OnMultipleAccess?.Invoke("GrantAllGroup", string.Empty);
        _actions.AddChild(grantAll);
        var revokeAll = Action("cmu-id-console-revoke-all", CmuButtonStyles.Variant.Danger);
        revokeAll.OnPressed += _ => OnMultipleAccess?.Invoke("RevokeAllGroup", string.Empty);
        _actions.AddChild(revokeAll);

        _groupsCaption.Text = Loc.GetString("cmu-id-console-caption-groups");

        var levels = console.AccessList
            .Select(a => _prototypes.TryIndex(a, out var proto) ? proto : null)
            .OfType<AccessLevelPrototype>()
            .ToList();

        var groups = console.AccessGroups
            .Select(g => _prototypes.TryIndex(g, out var proto) ? proto.AccessGroup : null)
            .OfType<string>()
            .Distinct()
            .OrderBy(g => g)
            .ToList();

        if (!groups.Contains(_accessGroup))
            _accessGroup = groups.FirstOrDefault() ?? string.Empty;

        foreach (var group in groups)
        {
            var inGroup = levels.Where(l => l.AccessGroup == group).ToList();
            var held = inGroup.Count(l => access.Tags.Contains(l.ID));
            var fill = inGroup.Count == 0 || held == 0
                ? CrtTerminalPalette.TextDim
                : held >= inGroup.Count
                    ? CrtTerminalPalette.Accent
                    : CrtTerminalPalette.Caution;
            var captured = group;
            _groups.AddChild(GroupButton(group.ToUpperInvariant(), $"{held}/{inGroup.Count}", fill, group == _accessGroup, () =>
            {
                _accessGroup = captured;
                Rebuild();
            }));
        }

        _entriesCaption.Text = Loc.GetString("cmu-id-console-caption-access", ("group", _accessGroup.ToUpperInvariant()));
        if (_accessGroup == string.Empty)
            return;

        var grantGroup = Action("cmu-id-console-grant-group", CmuButtonStyles.Variant.Affirm);
        grantGroup.OnPressed += _ => OnMultipleAccess?.Invoke("GrantAll", _accessGroup);
        _entryActions.AddChild(grantGroup);
        var revokeGroup = Action("cmu-id-console-revoke-group", CmuButtonStyles.Variant.Danger);
        revokeGroup.OnPressed += _ => OnMultipleAccess?.Invoke("RevokeAll", _accessGroup);
        _entryActions.AddChild(revokeGroup);

        foreach (var level in levels.Where(l => l.AccessGroup == _accessGroup).OrderBy(l => LevelName(l)))
        {
            var has = access.Tags.Contains(level.ID);
            var id = new ProtoId<AccessLevelPrototype>(level.ID);
            _entries.AddChild(EntryButton(LevelName(level), has, () => OnAccess?.Invoke(id, !has)));
        }
    }

    private void BuildJobs(IdModificationConsoleComponent console, AccessComponent access)
    {
        var discharge = new ConfirmButton
        {
            Text = Loc.GetString("cmu-id-console-discharge"),
            ConfirmationText = Loc.GetString("cmu-id-console-discharge-confirm"),
            MinWidth = 180,
        };
        CmuButtonStyles.Apply(discharge, CmuButtonStyles.Variant.Danger);
        discharge.Label.FontOverride = _small;
        discharge.OnPressed += _ => OnTerminate?.Invoke();
        _actions.AddChild(discharge);

        _groupsCaption.Text = Loc.GetString("cmu-id-console-caption-departments");

        var jobs = console.JobList
            .Select(j => _prototypes.TryIndex(j, out var proto) ? proto : null)
            .OfType<AccessGroupPrototype>()
            .ToList();

        var groups = console.JobGroups
            .Select(g => _prototypes.TryIndex(g, out var proto) ? proto.AccessGroup : null)
            .OfType<string>()
            .Distinct()
            .OrderBy(g => g)
            .ToList();

        if (!groups.Contains(_jobGroup))
        {
            // Open on the department of the card's current job, if it matches one exactly.
            _jobGroup = jobs.FirstOrDefault(j => IsCurrentJob(j, access))?.AccessGroup is { } current && groups.Contains(current)
                ? current
                : groups.FirstOrDefault() ?? string.Empty;
        }

        foreach (var group in groups)
        {
            var count = jobs.Count(j => j.AccessGroup == group);
            var current = jobs.Any(j => j.AccessGroup == group && IsCurrentJob(j, access));
            var captured = group;
            _groups.AddChild(GroupButton(group.ToUpperInvariant(), count.ToString(),
                current ? CrtTerminalPalette.Accent : CrtTerminalPalette.TextDim, group == _jobGroup, () =>
                {
                    _jobGroup = captured;
                    Rebuild();
                }));
        }

        _entriesCaption.Text = Loc.GetString("cmu-id-console-caption-jobs", ("group", _jobGroup.ToUpperInvariant()));
        foreach (var job in jobs.Where(j => j.AccessGroup == _jobGroup).OrderBy(j => j.GetAccessGroupName()))
        {
            var id = new ProtoId<AccessGroupPrototype>(job.ID);
            _entries.AddChild(EntryButton(job.GetAccessGroupName(), IsCurrentJob(job, access), () => OnJob?.Invoke(id)));
        }
    }

    private void BuildSquads(IdModificationConsoleComponent console)
    {
        var clear = Action("cmu-id-console-unassign", CmuButtonStyles.Variant.Caution);
        clear.OnPressed += _ => OnSquad?.Invoke(null);
        _actions.AddChild(clear);

        _groupsCaption.Text = Loc.GetString("cmu-id-console-caption-squads");
        _entriesCaption.Text = Loc.GetString("cmu-id-console-caption-squads-hint");

        if (console.Squads is not { } squads)
            return;

        foreach (var squad in squads)
        {
            var captured = squad.Id;
            _groups.AddChild(GroupButton(squad.Name.ToUpperInvariant(), string.Empty, squad.Color, false,
                () => OnSquad?.Invoke(captured)));
        }
    }

    private static bool IsCurrentJob(AccessGroupPrototype job, AccessComponent access)
    {
        return job.Tags.Count > 0 && access.Tags.SetEquals(job.Tags);
    }

    private static string LevelName(AccessLevelPrototype level)
    {
        return level.Name is { } name ? Loc.GetString(name) : level.ID;
    }

    private Control Slot(string caption, out Label name, out Label detail, out Button button)
    {
        var strip = Strip(CrtTerminalPalette.Surface2);
        strip.HorizontalExpand = true;
        var row = Row();
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        column.AddChild(Text(Loc.GetString(caption), _small, CrtTerminalPalette.TextDim));
        name = Text(string.Empty, _bold, CrtTerminalPalette.TextBright);
        name.ClipText = true;
        column.AddChild(name);
        detail = Text(string.Empty, _small, CrtTerminalPalette.Text);
        detail.ClipText = true;
        column.AddChild(detail);
        row.AddChild(column);
        button = new Button { MinWidth = 100, VerticalAlignment = VAlignment.Center };
        CmuButtonStyles.Apply(button, CmuButtonStyles.Variant.Neutral);
        button.Label.FontOverride = _small;
        row.AddChild(button);
        strip.AddChild(row);
        return strip;
    }

    private Button Action(string text, CmuButtonStyles.Variant variant)
    {
        var button = new Button { Text = Loc.GetString(text) };
        CmuButtonStyles.Apply(button, variant);
        button.Label.FontOverride = _small;
        return button;
    }

    /// <summary>A row in the left column: a marker in <paramref name="color"/>, a name and a count.</summary>
    private Control GroupButton(string name, string count, Color color, bool selected, Action onPressed)
    {
        var button = new ContainerButton { HorizontalExpand = true, MinHeight = 28 };
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = selected ? CrtTerminalPalette.Surface4 : CrtTerminalPalette.Surface2 },
        };
        var row = Row();
        row.AddChild(new PanelContainer
        {
            MinWidth = 4,
            PanelOverride = new StyleBoxFlat { BackgroundColor = selected ? CrtTerminalPalette.Accent : color.WithAlpha(0.6f) },
        });
        var label = Text(name, _bold, selected ? CrtTerminalPalette.TextBright : CrtTerminalPalette.Text);
        label.HorizontalExpand = true;
        label.ClipText = true;
        label.VerticalAlignment = VAlignment.Center;
        row.AddChild(label);
        var countLabel = Text(count, _small, color);
        countLabel.VerticalAlignment = VAlignment.Center;
        countLabel.Margin = new Thickness(0, 0, 8, 0);
        row.AddChild(countLabel);
        panel.AddChild(row);
        button.AddChild(panel);
        button.OnMouseEntered += _ =>
        {
            if (!selected)
                panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface3 };
        };
        button.OnMouseExited += _ =>
        {
            if (!selected)
                panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 };
        };
        button.OnPressed += _ => onPressed();
        return button;
    }

    /// <summary>One access or job in the right column, lit while the card has it.</summary>
    private Control EntryButton(string name, bool active, Action onPressed)
    {
        var button = new ContainerButton { HorizontalExpand = true, MinHeight = 30 };
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = active ? CrtTerminalPalette.Surface4 : CrtTerminalPalette.Surface2 },
        };
        var row = Row();
        row.AddChild(new PanelContainer
        {
            MinWidth = 4,
            PanelOverride = new StyleBoxFlat { BackgroundColor = active ? CrtTerminalPalette.Accent : CrtTerminalPalette.Line },
        });
        var label = Text(name, _body, active ? CrtTerminalPalette.TextBright : CrtTerminalPalette.TextDim);
        label.HorizontalExpand = true;
        label.ClipText = true;
        label.VerticalAlignment = VAlignment.Center;
        row.AddChild(label);
        var state = Text(Loc.GetString(active ? "cmu-id-console-entry-on" : "cmu-id-console-entry-off"), _small,
            active ? CrtTerminalPalette.Accent : CrtTerminalPalette.TextDim);
        state.VerticalAlignment = VAlignment.Center;
        state.Margin = new Thickness(0, 0, 8, 0);
        row.AddChild(state);
        panel.AddChild(row);
        button.AddChild(panel);
        button.OnMouseEntered += _ =>
            panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface3 };
        button.OnMouseExited += _ => panel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = active ? CrtTerminalPalette.Surface4 : CrtTerminalPalette.Surface2,
        };
        button.OnPressed += _ => onPressed();
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

    private static PanelContainer Panel(Color color)
    {
        return new PanelContainer
        {
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 8,
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
