using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.Controls.LineEdit;

namespace Content.Client._RMC14.Xenonids.JoinXeno;

[UsedImplicitly]
public sealed class JoinXenoBui : BoundUserInterface
{
    [ViewVariables]
    private LarvaPoolWindow? _window;

    private readonly List<EntryState> _entries = new();
    private string _searchText = string.Empty;

    public JoinXenoBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        EnsureWindow();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not JoinXenoBuiState joinXenoState)
            return;

        _window = EnsureWindow();
        _entries.Clear();
        _window.HiveContainer.DisposeAllChildren();

        foreach (var entry in joinXenoState.Entries)
        {
            var row = CreateRow(entry);
            _window.HiveContainer.AddChild(row);
            _entries.Add(new EntryState(row, entry.HiveName));
        }

        UpdateVisibleEntries();
    }

    private LarvaPoolWindow EnsureWindow()
    {
        if (_window is { Disposed: false })
            return _window;

        _window = this.CreateWindow<LarvaPoolWindow>();
        _window.SearchBar.OnTextChanged += OnSearchTextChanged;
        return _window;
    }

    private Control CreateRow(JoinXenoHiveEntry entry)
    {
        var panel = new PanelContainer
        {
            StyleClasses = { StyleNano.StyleClassCrtInsetPanel },
            HorizontalExpand = true,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };

        var textBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };

        textBox.AddChild(new Label
        {
            Text = entry.HiveName,
            ClipText = true,
            HorizontalExpand = true,
            StyleClasses = { StyleNano.StyleClassCrtHeading },
        });

        textBox.AddChild(new Label
        {
            Text = GetStatusText(entry),
            ClipText = true,
            HorizontalExpand = true,
            StyleClasses = { StyleNano.StyleClassCrtDimText },
        });

        if (entry.Status == LarvaPoolStatus.Ineligible)
        {
            textBox.AddChild(new Label
            {
                Text = Loc.GetString(
                    "rmc-xeno-larva-pool-ineligible-reason",
                    ("reason", GetIneligibilityReason(entry.IneligibilityReason))),
                ClipText = true,
                HorizontalExpand = true,
                StyleClasses = { StyleNano.StyleClassCrtDimText },
            });
        }

        row.AddChild(textBox);

        var toggle = new Button
        {
            Text = entry.PreferenceLoaded
                ? Loc.GetString(entry.OptedIn
                    ? "rmc-xeno-larva-pool-opt-out"
                    : "rmc-xeno-larva-pool-opt-in")
                : Loc.GetString("rmc-xeno-larva-pool-preference-loading"),
            Disabled = !entry.PreferenceLoaded,
            MinWidth = 110,
            VerticalAlignment = Control.VAlignment.Center,
        };
        toggle.OnPressed += _ =>
        {
            toggle.Disabled = true;
            SendMessage(new SetLarvaPoolOptInBuiMsg(entry.Hive, !entry.OptedIn));
        };
        row.AddChild(toggle);

        panel.AddChild(row);
        CrtLobbyTheme.Apply(panel);
        return panel;
    }

    private static string GetStatusText(JoinXenoHiveEntry entry)
    {
        return entry.Status switch
        {
            LarvaPoolStatus.Eligible => Loc.GetString("rmc-xeno-larva-pool-status-position", ("position", entry.Position)),
            LarvaPoolStatus.Waiting => Loc.GetString("rmc-xeno-larva-pool-status-waiting", ("position", entry.Position)),
            _ => Loc.GetString("rmc-xeno-larva-pool-status-ineligible", ("position", entry.Position)),
        };
    }

    private static string GetIneligibilityReason(LarvaPoolIneligibilityReason reason)
    {
        var localizationId = reason switch
        {
            LarvaPoolIneligibilityReason.PreferenceDataLoading => "rmc-xeno-larva-pool-reason-preference-loading",
            LarvaPoolIneligibilityReason.CharacterProfileUnavailable => "rmc-xeno-larva-pool-reason-character-profile",
            LarvaPoolIneligibilityReason.RoleBanned => "rmc-xeno-larva-pool-reason-role-banned",
            LarvaPoolIneligibilityReason.RoleRequirements => "rmc-xeno-larva-pool-reason-role-requirements",
            LarvaPoolIneligibilityReason.RevivableBody => "rmc-xeno-larva-pool-reason-revivable",
            LarvaPoolIneligibilityReason.StaffProtected => "rmc-xeno-larva-pool-reason-staff-protected",
            LarvaPoolIneligibilityReason.OptedOut => "rmc-xeno-larva-pool-reason-opted-out",
            _ => "rmc-xeno-larva-pool-reason-unknown",
        };

        return Loc.GetString(localizationId);
    }

    private void OnSearchTextChanged(LineEditEventArgs args)
    {
        _searchText = args.Text;
        UpdateVisibleEntries();
    }

    private void UpdateVisibleEntries()
    {
        if (_window is not { Disposed: false })
            return;

        _window.CountLabel.Text = Loc.GetString("rmc-xeno-larva-pool-count", ("count", _entries.Count));

        var anyVisible = false;
        foreach (var entry in _entries)
        {
            var visible = string.IsNullOrWhiteSpace(_searchText) ||
                          entry.SearchText.Contains(_searchText, StringComparison.OrdinalIgnoreCase);

            entry.Control.Visible = visible;
            anyVisible |= visible;
        }

        _window.ContentPanel.Visible = anyVisible;
        _window.NoHivesMessage.Text = _entries.Count == 0
            ? Loc.GetString("rmc-xeno-larva-pool-empty")
            : Loc.GetString("rmc-xeno-larva-pool-no-results");
        _window.NoHivesMessage.Visible = !anyVisible;
    }

    private readonly record struct EntryState(Control Control, string SearchText);
}
