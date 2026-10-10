using System.Globalization;
using System.Linq;
using Content.Client.Eui;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Eui;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Expeditions;

public sealed class CMUSquadPanelEui : BaseEui
{
    private CMUSquadPanelWindow? _window;
    private CMUSquadPanelState _state = new();
    private NetEntity? _member;
    private bool _optionsLoaded;
    private bool _closed;
    private string[] _variants = [], _outfits = [], _doctrines = [], _factions = [];
    private readonly string[] _facings = ["auto", "north", "east", "south", "west"];

    public override void Opened()
    {
        _window = new CMUSquadPanelWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.Refresh.OnPressed += _ => Send(CMUSquadPanelAction.Refresh);
        _window.Squad.OnItemSelected += args =>
        {
            if (args.Id < 0 || args.Id >= _state.Squads.Count)
                return;
            _window.Squad.SelectId(args.Id);
            SendMessage(new CMUSquadPanelMessage { Action = CMUSquadPanelAction.Select, Root = _state.Squads[args.Id].Root });
        };
        _window.Member.OnItemSelected += args =>
        {
            if (args.Id >= 0 && args.Id < _state.Members.Count)
            {
                _member = _state.Members[args.Id].Entity;
                ShowMember();
            }
        };
        Bind(_window.Spawn, CMUSquadPanelAction.Spawn);
        Bind(_window.Move, CMUSquadPanelAction.Move);
        Bind(_window.Guard, CMUSquadPanelAction.Guard);
        Bind(_window.Hold, CMUSquadPanelAction.Hold);
        Bind(_window.Regroup, CMUSquadPanelAction.Regroup);
        Bind(_window.Resupply, CMUSquadPanelAction.Resupply);
        Bind(_window.PatrolAdd, CMUSquadPanelAction.PatrolAdd);
        Bind(_window.PatrolStart, CMUSquadPanelAction.PatrolStart);
        Bind(_window.PatrolStop, CMUSquadPanelAction.PatrolStop);
        Bind(_window.ApplyDoctrine, CMUSquadPanelAction.Doctrine);
        Bind(_window.SetFriendly, CMUSquadPanelAction.Friendly);
        Bind(_window.SetTarget, CMUSquadPanelAction.Target);
        _window.AddFriendly.OnPressed += _ => Append(_window.Friendlies);
        _window.AddTarget.OnPressed += _ => Append(_window.Targets);
        Fill(_window.Facing, _facings, "auto");
        _window.OpenCentered();
        RefreshLater();
    }

    private void Bind(Button button, CMUSquadPanelAction action) => button.OnPressed += _ => Send(action);

    private void Append(LineEdit edit)
    {
        if (_window == null || _factions.Length == 0)
            return;
        var values = edit.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => value != "default")
            .Append(_factions[_window.Faction.SelectedId]).Distinct();
        edit.Text = string.Join(",", values);
    }

    private static void Fill(OptionButton button, string[] values, string preferred)
    {
        button.Clear();
        for (var i = 0; i < values.Length; i++)
            button.AddItem(values[i], i);
        if (values.Length > 0)
            button.SelectId(Math.Max(0, Array.IndexOf(values, preferred)));
        button.OnItemSelected += args => button.SelectId(args.Id);
    }

    private void Send(CMUSquadPanelAction action)
    {
        if (_window == null || !_optionsLoaded)
            return;
        if (action == CMUSquadPanelAction.Refresh)
        {
            SendMessage(new CMUSquadPanelMessage { Action = action });
            return;
        }
        var w = _window;
        var count = 1;
        var map = 0;
        var x = 0f;
        var y = 0f;
        var usesPoint = action is CMUSquadPanelAction.Spawn or CMUSquadPanelAction.Move or CMUSquadPanelAction.Guard or CMUSquadPanelAction.PatrolAdd;
        if (action == CMUSquadPanelAction.Spawn && !int.TryParse(w.Count.Text, out count) ||
            usesPoint && !w.Here.Pressed && (!int.TryParse(w.Map.Text, out map) ||
                !float.TryParse(w.X.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(w.Y.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out y)))
        {
            w.Status.Text = Loc.GetString("cmu-squads-invalid");
            return;
        }
        SendMessage(new CMUSquadPanelMessage
        {
            Action = action, Root = _state.Selected, Count = count, Variant = _variants[w.Variant.SelectedId],
            Outfit = _outfits[w.Outfit.SelectedId], Doctrine = _doctrines[w.Doctrine.SelectedId],
            Here = w.Here.Pressed, Map = map, X = x, Y = y, Facing = _facings[w.Facing.SelectedId],
            Value = action == CMUSquadPanelAction.Doctrine ? _doctrines[w.Doctrine.SelectedId] :
                action == CMUSquadPanelAction.Friendly ? w.Friendlies.Text : w.Targets.Text,
        });
    }

    public override void HandleState(EuiStateBase state)
    {
        if (_window == null || state is not CMUSquadPanelState panel)
            return;
        var previous = _state;
        _state = panel;
        if (!_optionsLoaded)
        {
            _variants = panel.Variants.ToArray(); _outfits = panel.Outfits.ToArray();
            _doctrines = panel.Doctrines.ToArray(); _factions = panel.Factions.ToArray();
            if (_variants.Length == 0 || _outfits.Length == 0 || _doctrines.Length == 0)
                return;
            Fill(_window.Variant, _variants, "mixed"); Fill(_window.Outfit, _outfits, "scavenger");
            Fill(_window.Doctrine, _doctrines, "balanced"); Fill(_window.Faction, _factions, "GOVFOR");
            _optionsLoaded = true;
        }
        if (panel.Status.Length > 0)
            _window.Status.Text = panel.Status;
        if (!previous.Squads.SequenceEqual(panel.Squads))
        {
            _window.Squad.Clear();
            for (var i = 0; i < panel.Squads.Count; i++)
                _window.Squad.AddItem(panel.Squads[i].Label, i);
        }
        var chosen = panel.Squads.FindIndex(squad => squad.Root == panel.Selected);
        if (chosen >= 0 && _window.Squad.SelectedId != chosen)
            _window.Squad.SelectId(chosen);
        if (!previous.Members.Select(member => (member.Entity, member.Name)).SequenceEqual(panel.Members.Select(member => (member.Entity, member.Name))))
        {
            _window.Member.Clear();
            for (var i = 0; i < panel.Members.Count; i++)
                _window.Member.AddItem($"{panel.Members[i].Name} ({panel.Members[i].Entity})", i);
        }
        if (previous.Selected != panel.Selected)
        {
            _window.Friendlies.Text = panel.Friendlies;
            _window.Targets.Text = panel.Targets;
        }
        ShowMember();
    }

    private void ShowMember()
    {
        if (_window == null)
            return;
        var index = _state.Members.FindIndex(member => member.Entity == _member);
        if (index < 0 && _state.Members.Count > 0)
            index = 0;
        if (index < 0)
        {
            _window.Detail.Text = Loc.GetString("cmu-squads-select");
            _window.Diagram.SetMembers([], null);
            return;
        }
        var selected = _state.Members[index];
        _member = selected.Entity;
        _window.Member.SelectId(index);
        _window.Detail.Text = selected.Detail;
        _window.Diagram.SetMembers(_state.Members, selected);
    }

    private void RefreshLater() => Timer.Spawn(TimeSpan.FromSeconds(1), () =>
    {
        if (_closed)
            return;
        if (_window?.Live.Pressed == true)
            Send(CMUSquadPanelAction.Refresh);
        RefreshLater();
    });

    public override void Closed()
    {
        _closed = true;
        _window?.Release();
        _window = null;
    }
}
