using System.Numerics;
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
    private JoinXenoQueueWindow? _window;

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

    private JoinXenoQueueWindow EnsureWindow()
    {
        if (_window is { Disposed: false })
            return _window;

        _window = this.CreateWindow<JoinXenoQueueWindow>();
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

        row.AddChild(textBox);

        var button = new Button
        {
            Text = GetButtonText(entry),
            TextAlign = Label.AlignMode.Center,
            ClipText = false,
            MinSize = new Vector2(132, 34),
            VerticalAlignment = Control.VAlignment.Center,
        };

        if (entry.Status == JoinXenoQueueStatus.NotQueued)
            button.AddStyleClass(StyleNano.StyleClassCrtAttentionButton);

        button.OnPressed += _ => SendPredictedMessage(new JoinXenoHiveChoiceBuiMsg(entry.Hive));
        row.AddChild(button);

        panel.AddChild(row);
        CrtLobbyTheme.Apply(panel);
        return panel;
    }

    private static string GetButtonText(JoinXenoHiveEntry entry)
    {
        return entry.Status == JoinXenoQueueStatus.NotQueued
            ? Loc.GetString("rmc-xeno-larva-queue-join")
            : Loc.GetString("rmc-xeno-larva-queue-leave");
    }

    private static string GetStatusText(JoinXenoHiveEntry entry)
    {
        return entry.Status switch
        {
            JoinXenoQueueStatus.Queued => Loc.GetString("rmc-xeno-larva-queue-status-position", ("position", entry.Position)),
            JoinXenoQueueStatus.Waiting => Loc.GetString("rmc-xeno-larva-queue-status-waiting"),
            _ => Loc.GetString("rmc-xeno-larva-queue-status-available"),
        };
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

        _window.CountLabel.Text = Loc.GetString("rmc-xeno-larva-queue-count", ("count", _entries.Count));

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
            ? Loc.GetString("rmc-xeno-larva-queue-empty")
            : Loc.GetString("rmc-xeno-larva-queue-no-results");
        _window.NoHivesMessage.Visible = !anyVisible;
    }

    private readonly record struct EntryState(Control Control, string SearchText);
}
