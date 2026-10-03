using System.Linq;
using System.Text;
using Content.Shared.CMU14.Radio;
using Content.Shared.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The set's memories: what each one holds, which one the operator talks on, and which ones
///     are being relayed to the headsets around them. Picking a net, keying a frequency and
///     renaming all happen in one editor under the list, opened from the memory's EDIT button.
/// </summary>
public sealed class ANPRCNetsPage : BoxContainer
{
    private readonly ANPRCRadioActions _radio;

    private readonly RichTextLabel _intro;
    private readonly PanelContainer _quickCard;
    private readonly RichTextLabel _quickText;
    private readonly Button _quickButton;
    private readonly BoxContainer _list;

    private readonly Button _addButton;
    private readonly BoxContainer _addRow;
    private readonly LineEdit _addLabel;

    private readonly PanelContainer _editor;
    private readonly Label _editorTitle;
    private readonly BoxContainer _netList;
    private readonly LineEdit _frequencyEdit;
    private readonly RichTextLabel _frequencyHelp;
    private readonly LineEdit _renameEdit;
    private readonly ANPRCConfirmButton _deleteButton;
    private readonly Button _emptyButton;

    private int _editSlot = -1;
    private string _listKey = string.Empty;
    private string _netListKey = string.Empty;

    /// <summary>The banner above is already offering the quick setup, so this card would repeat it.</summary>
    public bool SetupInBanner;

    public ANPRCNetsPage(ANPRCRadioActions radio)
    {
        _radio = radio;

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;

        _intro = ANPRCUi.Wrapped(Loc.GetString("anprc-op-nets-intro"), ANPRCUi.TextDim);
        AddChild(_intro);

        // ----- quick setup ---------------------------------------------------------------------

        var quick = ANPRCUi.Column(4, 6);
        _quickText = ANPRCUi.Wrapped(string.Empty, ANPRCUi.Text);
        _quickButton = ANPRCUi.Button(string.Empty, Loc.GetString("anprc-op-quick-setup-tooltip"), () => _radio.QuickSetup());

        quick.AddChild(ANPRCUi.Label(Loc.GetString("anprc-op-quick-setup-title"), ANPRCUi.Good, ANPRCUi.Mono(11, true)));
        quick.AddChild(_quickText);
        quick.AddChild(_quickButton);

        _quickCard = ANPRCUi.Card(quick, ANPRCUi.Good);
        AddChild(_quickCard);

        // ----- the memories --------------------------------------------------------------------

        _list = ANPRCUi.Column(4);
        AddChild(_list);

        _addButton = ANPRCUi.Button(Loc.GetString("anprc-op-add-memory"), Loc.GetString("anprc-op-add-memory-tooltip"), () =>
        {
            _addRow!.Visible = true;
            _addButton!.Visible = false;
            _addLabel!.Text = string.Empty;
            _addLabel.GrabKeyboardFocus();
        });
        AddChild(_addButton);

        _addRow = ANPRCUi.Row(4);
        _addLabel = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("anprc-op-add-memory-placeholder"),
        };
        _addLabel.OnTextEntered += _ => CommitAdd();

        _addRow.AddChild(_addLabel);
        _addRow.AddChild(ANPRCUi.Button(Loc.GetString("anprc-op-add"), null, CommitAdd));
        _addRow.AddChild(ANPRCUi.Button(Loc.GetString("anprc-op-cancel"), null, () =>
        {
            _addRow.Visible = false;
            _addButton.Visible = true;
        }));
        _addRow.Visible = false;
        AddChild(_addRow);

        // ----- the editor ----------------------------------------------------------------------

        var editor = ANPRCUi.Column(5, 6);

        var editorHeader = ANPRCUi.Row(4);
        _editorTitle = ANPRCUi.Label(string.Empty, ANPRCUi.HeadingText, ANPRCUi.Mono(11, true));
        _editorTitle.HorizontalExpand = true;
        editorHeader.AddChild(_editorTitle);
        editorHeader.AddChild(ANPRCUi.Button(Loc.GetString("anprc-op-done"), null, CloseEditor));
        editor.AddChild(editorHeader);

        editor.AddChild(ANPRCUi.Label(Loc.GetString("anprc-op-editor-pick-net"), ANPRCUi.Text));
        _netList = ANPRCUi.Column(2);
        editor.AddChild(_netList);

        var frequencyRow = ANPRCUi.Row(4);
        _frequencyEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("anprc-op-editor-frequency-placeholder"),
        };
        _frequencyEdit.OnTextEntered += _ => CommitFrequency();
        frequencyRow.AddChild(_frequencyEdit);
        frequencyRow.AddChild(ANPRCUi.Button(Loc.GetString("anprc-op-editor-tune"), null, CommitFrequency));

        _frequencyHelp = ANPRCUi.Wrapped(Loc.GetString("anprc-op-editor-frequency-help"), ANPRCUi.TextDim);
        // direct frequencies are keyed on the faceplate. the guided editor tunes named nets only,
        // which covers every net the operator's side holds and every one the search has fixed
        editor.AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-editor-frequency-expert"), ANPRCUi.TextDim));

        editor.AddChild(ANPRCUi.Label(Loc.GetString("anprc-op-editor-rename"), ANPRCUi.Text));
        var renameRow = ANPRCUi.Row(4);
        _renameEdit = new LineEdit { HorizontalExpand = true };
        _renameEdit.OnTextEntered += _ => CommitRename();
        renameRow.AddChild(_renameEdit);
        renameRow.AddChild(ANPRCUi.Button(Loc.GetString("anprc-op-editor-rename-button"), null, CommitRename));
        editor.AddChild(renameRow);

        var dangerRow = ANPRCUi.Row(4);
        _emptyButton = ANPRCUi.Button(Loc.GetString("anprc-op-editor-empty"), Loc.GetString("anprc-op-editor-empty-tooltip"), () =>
        {
            if (_editSlot >= 0)
                _radio.ClearSlot(_editSlot);
        });
        _emptyButton.HorizontalExpand = true;

        _deleteButton = new ANPRCConfirmButton(
            Loc.GetString("anprc-op-editor-delete"),
            Loc.GetString("anprc-op-editor-delete-confirm"),
            Loc.GetString("anprc-op-editor-delete-tooltip"))
        {
            HorizontalExpand = true,
        };
        _deleteButton.OnConfirmed += () =>
        {
            if (_editSlot < 0)
                return;

            _radio.DeleteSlot(_editSlot);
            CloseEditor();
        };

        dangerRow.AddChild(_emptyButton);
        dangerRow.AddChild(_deleteButton);
        editor.AddChild(dangerRow);

        _editor = ANPRCUi.Card(editor, ANPRCUi.LcdEdge);
        _editor.Visible = false;
        AddChild(_editor);
    }

    private void CommitAdd()
    {
        var label = _addLabel.Text.Trim().ToUpperInvariant();

        if (label.Length > ANPRCRadioComponent.MaxLabelLength)
            label = label[..ANPRCRadioComponent.MaxLabelLength];

        _radio.AddSlot(label);

        _addRow.Visible = false;
        _addButton.Visible = true;
        _addLabel.Text = string.Empty;
    }

    private void CommitFrequency()
    {
        var text = _frequencyEdit.Text.Trim();

        if (_editSlot < 0 || text.Length == 0)
            return;

        _radio.ManualFrequency(_editSlot, text);
        _frequencyEdit.Text = string.Empty;
    }

    private void CommitRename()
    {
        var text = _renameEdit.Text.Trim().ToUpperInvariant();

        if (_editSlot < 0 || text.Length == 0)
            return;

        if (text.Length > ANPRCRadioComponent.MaxLabelLength)
            text = text[..ANPRCRadioComponent.MaxLabelLength];

        _radio.RenameSlot(_editSlot, text);
    }

    public void OpenEditor(int slot, ANPRCPanelData data)
    {
        _editSlot = slot;
        _renameEdit.Text = data.SlotLabel(slot);
        _frequencyEdit.Text = string.Empty;
        _deleteButton.Disarm();
        _netListKey = string.Empty;

        _editor.Visible = true;
        _listKey = string.Empty;

        Update(data);
    }

    private void CloseEditor()
    {
        _editSlot = -1;
        _editor.Visible = false;
        _listKey = string.Empty;
    }

    public void Update(ANPRCPanelData data)
    {
        var state = data.State;

        // ----- quick setup -----------------------------------------------------------------------

        _quickCard.Visible = data.MissingStandardNets.Count > 0 && !SetupInBanner;

        if (_quickCard.Visible)
        {
            var names = string.Join(", ", data.MissingStandardNets.Select(net =>
                Loc.GetString("anprc-op-std-net-entry", ("label", net.Label), ("net", data.ChannelName(net.Channel)))));

            ANPRCUi.SetWrapped(_quickText,
                Loc.GetString("anprc-op-quick-setup-text", ("nets", names)),
                ANPRCUi.Text);

            _quickButton.Text = Loc.GetString(state.Enabled
                ? "anprc-op-quick-setup-button"
                : "anprc-op-quick-setup-button-off");
        }

        // ----- the memories ----------------------------------------------------------------------

        if (_editSlot >= 0 && !state.SlotLabels.ContainsKey(_editSlot))
            CloseEditor();

        var key = ListKey(data);

        if (key != _listKey)
        {
            _listKey = key;
            RebuildList(data);
        }

        var full = state.SlotLabels.Count >= ANPRCRadioComponent.MaxSlots;
        _addButton.Disabled = full;
        _addButton.Text = full
            ? Loc.GetString("anprc-op-add-memory-full")
            : Loc.GetString("anprc-op-add-memory-free",
                ("free", ANPRCRadioComponent.MaxSlots - state.SlotLabels.Count),
                ("max", ANPRCRadioComponent.MaxSlots));

        if (full && _addRow.Visible)
        {
            _addRow.Visible = false;
            _addButton.Visible = true;
        }

        // ----- the editor ------------------------------------------------------------------------

        if (_editSlot < 0)
            return;

        _editorTitle.Text = Loc.GetString("anprc-op-editor-title", ("label", data.SlotLabel(_editSlot)));
        _emptyButton.Disabled = data.SlotEmpty(_editSlot);

        var netKey = NetListKey(data);

        if (netKey != _netListKey)
        {
            _netListKey = netKey;
            RebuildNetList(data);
        }
    }

    // the list is only rebuilt when something on it changed. a state push lands every second
    // while the link drifts, and rebuilding under the cursor makes buttons flicker
    private static string ListKey(ANPRCPanelData data)
    {
        var state = data.State;
        var builder = new StringBuilder();

        builder.Append(state.ActiveSlot).Append('|').Append(state.Enabled)
            .Append('|').Append(data.Relay.Relaying).Append('|').Append(state.ScanEnabled)
            .Append('|').Append(state.MonitorEnabled);

        foreach (var slot in data.Slots)
        {
            builder.Append('|').Append(slot).Append(':').Append(data.SlotLabel(slot));

            if (state.Presets.TryGetValue(slot, out var channel))
                builder.Append(':').Append(channel.Id).Append(':').Append(data.IsRelayed(channel));

            if (state.FrequencyOverrides.TryGetValue(slot, out var direct))
                builder.Append(':').Append(direct.Kilohertz);
        }

        foreach (var net in data.MissingStandardNets)
        {
            builder.Append("|m").Append(net.Channel.Id);
        }

        return builder.ToString();
    }

    private void RebuildList(ANPRCPanelData data)
    {
        _list.RemoveAllChildren();

        var state = data.State;

        if (state.SlotLabels.Count == 0)
        {
            _list.AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-nets-empty"), ANPRCUi.Warn));
            return;
        }

        foreach (var slot in data.Slots)
        {
            _list.AddChild(BuildCard(data, slot));
        }
    }

    private Control BuildCard(ANPRCPanelData data, int slot)
    {
        var state = data.State;
        var active = slot == state.ActiveSlot;
        var empty = data.SlotEmpty(slot);

        var row = ANPRCUi.Row(6);
        row.Margin = new Thickness(5, 4);

        // the talk button leads the row because it is what an operator presses most
        var use = new Button
        {
            Text = Loc.GetString(active ? "anprc-op-net-active" : "anprc-op-net-use"),
            ToolTip = Loc.GetString(active ? "anprc-op-net-active-tooltip" : "anprc-op-net-use-tooltip"),
            MinWidth = 64,
            Pressed = active,
            Disabled = empty,
            VerticalAlignment = VAlignment.Center,
        };
        use.OnPressed += _ => _radio.SelectSlot(slot);
        row.AddChild(use);

        var text = ANPRCUi.Column(1);
        text.HorizontalExpand = true;

        var title = ANPRCUi.Label(
            data.SlotLabel(slot) + "   " + SlotReading(data, slot, out var channelName),
            active ? ANPRCUi.LcdBright : empty ? ANPRCUi.TextDim : ANPRCUi.Text,
            ANPRCUi.Mono(11, true));
        text.AddChild(title);

        if (channelName.Length > 0)
            text.AddChild(ANPRCUi.Label(channelName, ANPRCUi.TextDim));

        var (status, colour) = SlotStatus(data, slot, active);
        text.AddChild(ANPRCUi.Wrapped(status, colour));

        row.AddChild(text);

        var edit = new Button
        {
            Text = Loc.GetString("anprc-op-net-edit"),
            ToolTip = Loc.GetString("anprc-op-net-edit-tooltip"),
            VerticalAlignment = VAlignment.Center,
        };
        edit.OnPressed += _ => OpenEditor(slot, data);
        row.AddChild(edit);

        return ANPRCUi.Framed(row,
            active ? ANPRCUi.PanelRaised : ANPRCUi.Panel,
            active ? ANPRCUi.LcdBright : ANPRCUi.PanelEdge,
            active ? 2f : 1f);
    }

    private static string SlotReading(ANPRCPanelData data, int slot, out string channelName)
    {
        var state = data.State;
        channelName = string.Empty;

        if (state.FrequencyOverrides.TryGetValue(slot, out var direct))
        {
            var unknown = state.SweepContacts.Any(contact =>
                contact.Resolved && !contact.Known && contact.Frequency == direct);

            channelName = Loc.GetString(unknown ? "anprc-op-net-unknown" : "anprc-op-net-direct");
            return ANPRCPanelData.FormatFrequency(direct);
        }

        if (state.Presets.TryGetValue(slot, out var channel) && data.TryChannel(channel, out var proto))
        {
            channelName = proto!.LocalizedName;
            return ANPRCPanelData.FormatFrequency(data.PlanFrequency(proto));
        }

        return "---.---";
    }

    /// <summary>What this memory is doing for the operator and for everyone else, in words.</summary>
    private static (string, Color) SlotStatus(ANPRCPanelData data, int slot, bool active)
    {
        var state = data.State;

        if (data.SlotEmpty(slot))
            return (Loc.GetString("anprc-op-net-status-empty"), ANPRCUi.Warn);

        var parts = new List<string>();

        if (active)
            parts.Add(Loc.GetString(state.MonitorEnabled ? "anprc-op-net-status-listening" : "anprc-op-net-status-talking"));
        else if (state.MonitorEnabled || state.ScanEnabled)
            parts.Add(Loc.GetString("anprc-op-net-status-hearing"));

        var colour = active ? ANPRCUi.Good : ANPRCUi.TextDim;

        if (state.Presets.TryGetValue(slot, out var channel))
        {
            if (data.IsRelayed(channel))
            {
                parts.Add(Loc.GetString("anprc-op-net-status-relayed"));
            }
            else if (data.TryChannel(channel, out var proto) && proto!.AnchorGated)
            {
                var own = string.IsNullOrEmpty(state.OperatorFaction) ||
                          string.Equals(proto.Faction, state.OperatorFaction, StringComparison.OrdinalIgnoreCase);

                parts.Add(Loc.GetString(own ? "anprc-op-net-status-not-relayed" : "anprc-op-net-status-foreign"));

                if (own && active)
                    colour = ANPRCUi.Warn;
            }
        }
        else
        {
            parts.Add(Loc.GetString("anprc-op-net-status-direct"));
        }

        return (string.Join(" · ", parts), colour);
    }

    private static string NetListKey(ANPRCPanelData data)
    {
        var builder = new StringBuilder();

        foreach (var (id, frequency) in data.State.ChannelFrequencies.OrderBy(pair => pair.Key))
        {
            builder.Append(id).Append(frequency.Kilohertz).Append('|');
        }

        foreach (var net in data.Relay.StandardNets)
        {
            builder.Append(net.Channel.Id).Append('|');
        }

        if (data.State.Presets.TryGetValue(data.State.ActiveSlot, out var active))
            builder.Append('*').Append(active.Id);

        return builder.ToString();
    }

    /// <summary>
    ///     The nets this set may work: its own side's, plus any foreign net the search receiver has
    ///     fixed. Standard nets are listed first and marked, so the recommended pick is obvious.
    /// </summary>
    private void RebuildNetList(ANPRCPanelData data)
    {
        _netList.RemoveAllChildren();

        var state = data.State;
        var faction = state.OperatorFaction;
        var standard = data.Relay.StandardNets;
        var nets = new List<(RadioChannelPrototype Proto, bool Intercept, ANPRCStandardNet? Standard)>();

        foreach (var proto in data.Channels())
        {
            if (proto.Frequency == RadioFrequency.Off)
                continue;

            var own = string.IsNullOrEmpty(faction) ||
                      string.Equals(proto.Faction, faction, StringComparison.OrdinalIgnoreCase);

            // a foreign net lists once the server has put its frequency in the state, which it
            // only does after the search receiver has fixed it. unfactioned nets are tuned by number
            var intercept = !own && !string.IsNullOrEmpty(proto.Faction) &&
                            state.ChannelFrequencies.ContainsKey(proto.ID);

            if (!own && !intercept)
                continue;

            nets.Add((proto, intercept, standard.FirstOrDefault(net => net.Channel == proto.ID)));
        }

        if (nets.Count == 0)
        {
            _netList.AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-editor-no-nets"), ANPRCUi.TextDim));
            return;
        }

        var ordered = nets
            .OrderBy(net => net.Standard == null)
            .ThenBy(net => net.Intercept)
            .ThenBy(net => net.Proto.LocalizedName, StringComparer.OrdinalIgnoreCase);

        foreach (var (proto, intercept, standardNet) in ordered)
        {
            var tag = standardNet != null
                ? Loc.GetString(standardNet.Squad ? "anprc-op-editor-tag-squad" : "anprc-op-editor-tag-standard")
                : intercept
                    ? Loc.GetString("anprc-op-editor-tag-intercept")
                    : string.Empty;

            var inMemory = state.Presets.Values.Contains(proto.ID);

            if (inMemory)
                tag = (tag.Length > 0 ? tag + " " : string.Empty) + Loc.GetString("anprc-op-editor-tag-in-memory");

            var button = new Button
            {
                Text = Loc.GetString("anprc-op-editor-net-entry",
                    ("frequency", ANPRCPanelData.FormatFrequency(data.PlanFrequency(proto))),
                    ("net", proto.LocalizedName),
                    ("tag", tag)),
                HorizontalExpand = true,
                ToolTip = intercept ? Loc.GetString("anprc-op-editor-intercept-tooltip") : null,
            };

            if (intercept)
                button.ModulateSelfOverride = ANPRCUi.Warn;

            var channel = new ProtoId<RadioChannelPrototype>(proto.ID);
            button.OnPressed += _ =>
            {
                if (_editSlot >= 0)
                    _radio.SetSlotChannel(_editSlot, channel);
            };

            _netList.AddChild(button);
        }
    }
}
