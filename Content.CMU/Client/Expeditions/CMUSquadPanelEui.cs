using System.Globalization;
using System.Linq;
using System.Text;
using Content.Client.Eui;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Eui;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Expeditions;

public sealed partial class CMUSquadPanelEui : BaseEui
{
    [Dependency] private IClipboardManager _clipboard = default!;

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
        _window.CopyDiagnostics.OnPressed += _ => CopyDiagnostics();
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
        Bind(_window.Assault, CMUSquadPanelAction.Assault);
        Bind(_window.Guard, CMUSquadPanelAction.Guard);
        Bind(_window.Hold, CMUSquadPanelAction.Hold);
        Bind(_window.Regroup, CMUSquadPanelAction.Regroup);
        Bind(_window.Resupply, CMUSquadPanelAction.Resupply);
        Bind(_window.PatrolAdd, CMUSquadPanelAction.PatrolAdd);
        Bind(_window.PatrolStart, CMUSquadPanelAction.PatrolStart);
        Bind(_window.PatrolStop, CMUSquadPanelAction.PatrolStop);
        Bind(_window.PatrolClear, CMUSquadPanelAction.PatrolClear);
        Bind(_window.AutoPatrol, CMUSquadPanelAction.AutoPatrol);
        Bind(_window.Cooperate, CMUSquadPanelAction.Cooperation);
        Bind(_window.ApplyDoctrine, CMUSquadPanelAction.Doctrine);
        Bind(_window.SetFriendly, CMUSquadPanelAction.Friendly);
        Bind(_window.SetTarget, CMUSquadPanelAction.Target);
        _window.AddFriendly.OnPressed += _ => Append(_window.Friendlies);
        _window.AddTarget.OnPressed += _ => Append(_window.Targets);
        _window.Here.OnToggled += _ => UpdatePositionFields();
        _window.AimSkill.OnValueChanged += value =>
            _window.AimSkillLabel.Text = Loc.GetString("cmu-squads-aim-skill", ("percent", (int) value.Value));
        _window.AimSkill.OnReleased += _ => Send(CMUSquadPanelAction.AimSkill);
        Fill(_window.Facing, _facings, "auto");
        UpdatePositionFields();
        UpdateOrderControls();
        _window.OpenCentered();
        RefreshLater();
    }

    private void Bind(BaseButton button, CMUSquadPanelAction action) => button.OnPressed += _ => Send(action);

    private void Append(LineEdit edit)
    {
        if (_window == null || _factions.Length == 0)
            return;
        var values = edit.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => value != "default")
            .Append(_factions[_window.Faction.SelectedId]).Distinct();
        edit.Text = string.Join(",", values);
    }

    private static void Fill(OptionButton button, string[] values, string preferred, Func<string, string>? label = null)
    {
        button.Clear();
        for (var i = 0; i < values.Length; i++)
            button.AddItem(label?.Invoke(values[i]) ?? values[i], i);
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
        var usesPoint = action is CMUSquadPanelAction.Spawn or CMUSquadPanelAction.Move or CMUSquadPanelAction.Assault or CMUSquadPanelAction.Guard or CMUSquadPanelAction.PatrolAdd;
        if (action == CMUSquadPanelAction.Spawn && (!int.TryParse(w.Count.Text, out count) || count is < 1 or > 12) ||
            usesPoint && !w.Here.Pressed && (!int.TryParse(w.Map.Text, out map) ||
                !float.TryParse(w.X.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(w.Y.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !float.IsFinite(x) || !float.IsFinite(y)))
        {
            w.Status.SetMessage(Loc.GetString("cmu-squads-invalid"));
            return;
        }
        SendMessage(new CMUSquadPanelMessage
        {
            Action = action, Root = _state.Selected, Count = count, Variant = _variants[w.Variant.SelectedId],
            Outfit = _outfits[w.Outfit.SelectedId], Doctrine = _doctrines[w.Doctrine.SelectedId],
            Here = w.Here.Pressed, Map = map, X = x, Y = y, Facing = _facings[w.Facing.SelectedId],
            Enabled = action == CMUSquadPanelAction.AutoPatrol ? w.AutoPatrol.Pressed : w.Cooperate.Pressed,
            AimSkillPercent = (int) w.AimSkill.Value,
            Value = action == CMUSquadPanelAction.Doctrine ? _doctrines[w.OrderDoctrine.SelectedId] :
                action == CMUSquadPanelAction.Friendly ? w.Friendlies.Text : w.Targets.Text,
        });
    }

    public override void HandleState(EuiStateBase state)
    {
        if (_window == null || state is not CMUSquadPanelState panel)
            return;
        var previous = _state;
        _state = panel;
        _window.CopyDiagnostics.Disabled = panel.Members.Count == 0;
        if (!_optionsLoaded)
        {
            _variants = panel.Variants.ToArray(); _outfits = panel.Outfits.ToArray();
            _doctrines = panel.Doctrines.ToArray(); _factions = panel.Factions.ToArray();
            if (_variants.Length == 0 || _outfits.Length == 0 || _doctrines.Length == 0)
                return;
            Fill(_window.Variant, _variants, "mixed", value => Loc.GetString($"cmu-squads-variant-{value}"));
            Fill(_window.Outfit, _outfits, "scavenger");
            Fill(_window.Doctrine, _doctrines, "balanced"); Fill(_window.Faction, _factions, "GOVFOR");
            Fill(_window.OrderDoctrine, _doctrines, panel.CurrentDoctrine);
            _optionsLoaded = true;
            if (panel.Squads.Count == 0)
                _window.CommandTabs.CurrentTab = 1;
        }
        if (panel.Status.Length > 0)
            _window.Status.SetMessage(FormattedMessage.FromUnformatted(panel.Status));
        _window.Overview.SetMessage(FormattedMessage.FromUnformatted(panel.Overview.Length > 0 ? panel.Overview : Loc.GetString("cmu-squads-select")));
        _window.AutoPatrol.Pressed = panel.AutomaticPatrol;
        _window.Cooperate.Pressed = panel.Coordinating;
        if (!_window.AimSkill.Grabbed)
        {
            _window.AimSkill.SetValueWithoutEvent(panel.AimSkillPercent);
            _window.AimSkillLabel.Text = Loc.GetString("cmu-squads-aim-skill", ("percent", panel.AimSkillPercent));
        }
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
            _window.OrderDoctrine.SelectId(Math.Max(0, Array.IndexOf(_doctrines, panel.CurrentDoctrine)));
        }
        UpdateOrderControls();
        ShowMember();
    }

    private void UpdatePositionFields()
    {
        if (_window == null)
            return;
        _window.Map.Editable = _window.X.Editable = _window.Y.Editable = !_window.Here.Pressed;
    }

    private void CopyDiagnostics()
    {
        if (_window == null || _state.Members.Count == 0)
            return;
        var report = new StringBuilder(_state.Overview).AppendLine();
        foreach (var member in _state.Members)
        {
            report.AppendLine().AppendLine(member.Name).AppendLine(member.Summary);
            report.AppendLine($"{Loc.GetString("cmu-squads-order-label")}: {member.Order}");
            report.AppendLine($"{Loc.GetString("cmu-squads-tactic-label")}: {member.Tactic} — {member.TacticReason}");
            report.AppendLine($"{Loc.GetString("cmu-squads-action-label")}: {member.Action}");
            report.AppendLine($"{Loc.GetString("cmu-squads-status-label")}: {member.Status}");
            report.AppendLine(member.Detail);
        }
        _clipboard.SetText(report.ToString());
        _window.Status.SetMessage(Loc.GetString("cmu-squads-copied"));
    }

    private void UpdateOrderControls()
    {
        if (_window == null)
            return;
        var disabled = _state.Selected == null || !_state.Members.Any(member => member.Active);
        _window.AimSkill.Disabled = disabled;
        foreach (var button in new BaseButton[] { _window.Move, _window.Assault, _window.Guard, _window.Hold, _window.Regroup,
                     _window.Resupply, _window.PatrolAdd, _window.PatrolStart, _window.PatrolStop, _window.PatrolClear,
                     _window.AutoPatrol, _window.Cooperate, _window.ApplyDoctrine, _window.SetFriendly, _window.SetTarget,
                     _window.AddFriendly, _window.AddTarget })
            button.Disabled = disabled;
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
            _window.Detail.SetMessage(Loc.GetString("cmu-squads-select"));
            _window.MemberSummary.SetMessage("");
            _window.ActivityHeading.Text = Loc.GetString("cmu-squads-activity-heading");
            _window.OrderSummary.SetMessage("");
            _window.TacticSummary.SetMessage("");
            _window.ActionSummary.SetMessage("");
            _window.ActivityStatus.SetMessage("");
            _window.Diagram.SetMembers([], null);
            return;
        }
        var selected = _state.Members[index];
        _member = selected.Entity;
        _window.Member.SelectId(index);
        _window.Detail.SetMessage(FormattedMessage.FromUnformatted(selected.Detail));
        _window.MemberSummary.SetMessage(FormattedMessage.FromUnformatted(selected.Summary));
        _window.ActivityHeading.Text = Loc.GetString(selected.Recorded ? "cmu-squads-activity-recorded" : "cmu-squads-activity-heading");
        _window.OrderSummary.SetMessage(FormattedMessage.FromUnformatted(selected.Order));
        _window.TacticSummary.SetMessage(FormattedMessage.FromUnformatted(selected.Tactic + "\n" + selected.TacticReason));
        _window.ActionSummary.SetMessage(FormattedMessage.FromUnformatted(selected.Action));
        _window.ActivityStatus.SetMessage(FormattedMessage.FromUnformatted(selected.Status));
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
