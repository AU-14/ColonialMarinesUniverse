using System.Linq;
using Content.Client.Eui;
using Content.Shared.CMU14.Lobby;
using Content.Shared.Eui;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Lobby;

public sealed class CMUEventSetupEui : BaseEui
{
    private readonly CMUEventSetupWindow _window = new();
    private readonly HashSet<string> _accessJobs = new();
    private CMUEventSetupState? _state;
    private string _side = "govfor";
    private string? _group;
    private string? _slotJob;
    private bool _applying;

    public CMUEventSetupEui()
    {
        _window.OnClose += OnClose;
        _window.EnabledToggle.OnToggled += args =>
        {
            if (!_applying)
                SendMessage(new CMUEventSetupMessage(args.Pressed
                    ? CMUEventSetupAction.Enable : CMUEventSetupAction.Disable));
        };
        _window.RefreshButton.OnPressed += _ => SendMessage(new CMUEventSetupMessage(CMUEventSetupAction.Refresh));
        _window.SidePicker.OnItemSelected += args =>
        {
            if (_applying || _state == null)
                return;
            _window.SidePicker.SelectId(args.Id);
            _side = _state.Sides[args.Id].Id;
            _accessJobs.Clear();
            _slotJob = null;
            RenderRoles();
            RenderSummary();
        };
        _window.GroupPicker.OnItemSelected += args => SelectGroup(args.Id);
        _window.AccessSearch.OnTextChanged += _ => RenderRoles();
        _window.SlotSearch.OnTextChanged += _ => RenderRoles();
        _window.WholeSide.OnToggled += _ => RenderRoles();
        _window.AccessRoles.OnItemSelected += args =>
        {
            if (!_applying && _window.AccessRoles[args.ItemIndex].Metadata is string id)
            {
                _accessJobs.Add(id);
                RenderSelectionCount();
            }
        };
        _window.AccessRoles.OnItemDeselected += args =>
        {
            if (!_applying && _window.AccessRoles[args.ItemIndex].Metadata is string id)
            {
                _accessJobs.Remove(id);
                RenderSelectionCount();
            }
        };
        _window.SlotRoles.OnItemSelected += args =>
        {
            if (!_applying)
                _slotJob = _window.SlotRoles[args.ItemIndex].Metadata as string;
            UpdateSlotButtons();
        };
        _window.SlotRoles.OnItemDeselected += _ =>
        {
            if (_applying)
                return;
            _slotJob = null;
            UpdateSlotButtons();
        };
        _window.SaveGroup.OnPressed += _ =>
        {
            _group = _window.GroupName.Text;
            SendMessage(new CMUEventSetupMessage(CMUEventSetupAction.SaveGroup, _group, _window.Code.Text, _side,
                _window.WholeSide.Pressed ? new List<string> { "all" } : _accessJobs.ToList()));
            _window.Code.Text = string.Empty;
        };
        _window.RemoveGroup.OnPressed += _ =>
        {
            if (_group != null)
                SendMessage(new CMUEventSetupMessage(CMUEventSetupAction.RemoveGroup, group: _group));
        };
        _window.SlotOperation.AddItem(Loc.GetString("cmu-event-setup-add-slots"), 0);
        _window.SlotOperation.AddItem(Loc.GetString("cmu-event-setup-set-slots"), 1);
        _window.SlotOperation.SelectId(0);
        _window.SlotOperation.OnItemSelected += args => _window.SlotOperation.SelectId(args.Id);
        _window.ApplySlots.OnPressed += _ =>
        {
            if (_slotJob != null)
                SendMessage(new CMUEventSetupMessage(_window.SlotOperation.SelectedId == 0
                    ? CMUEventSetupAction.AddSlots : CMUEventSetupAction.SetSlots,
                    side: _side, roles: new List<string> { _slotJob }, amount: _window.Amount.Value));
        };
        _window.ClearPending.OnPressed += _ => SendMessage(new CMUEventSetupMessage(CMUEventSetupAction.ClearPending));
        _window.GroupName.IsValid = text => text.Length <= CMUEventCodeRequestEvent.MaxCodeLength;
        _window.Code.IsValid = text => text.Length <= CMUEventCodeRequestEvent.MaxCodeLength;
    }

    public override void Opened() => _window.OpenCentered();

    public override void Closed()
    {
        _window.OnClose -= OnClose;
        _window.Close();
        base.Closed();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not CMUEventSetupState snapshot)
            return;

        _state = snapshot;
        _applying = true;
        _window.EnabledToggle.Pressed = snapshot.Enabled;
        _window.SidePicker.Clear();
        for (var i = 0; i < snapshot.Sides.Count; i++)
            _window.SidePicker.AddItem(snapshot.Sides[i].Name, i);
        var sideIndex = snapshot.Sides.FindIndex(side => side.Id == _side);
        if (sideIndex >= 0)
            _window.SidePicker.SelectId(sideIndex);

        _window.GroupPicker.Clear();
        _window.GroupPicker.AddItem(Loc.GetString("cmu-event-setup-new-group"), 0);
        for (var i = 0; i < snapshot.Groups.Count; i++)
        {
            var group = snapshot.Groups[i];
            _window.GroupPicker.AddItem(Loc.GetString("cmu-event-access-group", ("group", group.Name),
                ("jobs", group.Jobs.Count), ("players", group.Players)), i + 1);
        }
        var groupIndex = snapshot.Groups.FindIndex(group => group.Name == _group);
        _window.GroupPicker.SelectId(groupIndex + 1);
        _window.RemoveGroup.Disabled = groupIndex < 0;
        _window.SlotHelp.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString(snapshot.InLobby
            ? "cmu-event-setup-lobby-slots-help" : "cmu-event-setup-live-slots-help")));
        _window.Feedback.SetMessage(FormattedMessage.FromUnformatted(snapshot.Feedback));
        _applying = false;
        RenderRoles();
        RenderSummary();
    }

    private void SelectGroup(int id)
    {
        if (_applying || _state == null)
            return;

        _applying = true;
        _window.GroupPicker.SelectId(id);
        _accessJobs.Clear();
        _window.Code.Text = string.Empty;
        if (id == 0)
        {
            _group = null;
            _window.GroupName.Text = string.Empty;
            _window.WholeSide.Pressed = true;
        }
        else
        {
            var group = _state.Groups[id - 1];
            _group = group.Name;
            _window.GroupName.Text = group.Name;
            _accessJobs.UnionWith(group.Jobs);
            var side = _state.Jobs.FirstOrDefault(job => _accessJobs.Contains(job.Id))?.Side;
            if (side != null)
            {
                _side = side;
                _window.SidePicker.SelectId(_state.Sides.FindIndex(option => option.Id == side));
            }
            var sideJobs = _state.Jobs.Where(job => job.Side == _side).ToList();
            _window.WholeSide.Pressed = sideJobs.Count > 0 && sideJobs.All(job => _accessJobs.Contains(job.Id));
        }
        _slotJob = null;
        _window.RemoveGroup.Disabled = id == 0;
        _applying = false;
        RenderRoles();
        RenderSummary();
    }

    private void RenderRoles()
    {
        if (_applying || _state == null)
            return;

        _applying = true;
        _window.AccessRoles.Clear();
        _window.SlotRoles.Clear();
        var slotVisible = false;
        foreach (var job in _state.Jobs.Where(job => job.Side == _side))
        {
            if (Matches(job, _window.AccessSearch.Text))
            {
                var item = _window.AccessRoles.AddItem(job.Name, metadata: job.Id);
                item.TooltipText = job.Id;
                item.Selected = _accessJobs.Contains(job.Id);
                item.Disabled = _window.WholeSide.Pressed;
            }
            if (!Matches(job, _window.SlotSearch.Text))
                continue;
            var slot = _window.SlotRoles.AddItem(job.Name, metadata: job.Id);
            slot.TooltipText = job.Id;
            slot.Selected = job.Id == _slotJob;
            slotVisible |= job.Id == _slotJob;
        }
        if (!slotVisible)
            _slotJob = null;
        _window.AccessSearch.Editable = !_window.WholeSide.Pressed;
        _applying = false;
        RenderSelectionCount();
        UpdateSlotButtons();
    }

    private static bool Matches(CMUEventJobInfo job, string search) =>
        job.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        job.Id.Contains(search, StringComparison.OrdinalIgnoreCase);

    private void RenderSummary()
    {
        var lines = _state?.Slots.GetValueOrDefault(_side) ?? [];
        _window.SlotSummary.SetMessage(FormattedMessage.FromUnformatted(string.Join('\n', lines)));
    }

    private void RenderSelectionCount()
    {
        var count = _window.WholeSide.Pressed ? _state?.Jobs.Count(job => job.Side == _side) ?? 0 : _accessJobs.Count;
        _window.AccessSelection.Text = Loc.GetString("cmu-event-setup-selected", ("count", count));
    }

    private void UpdateSlotButtons() => _window.ApplySlots.Disabled = _state?.Enabled != true || _slotJob == null;

    private void OnClose() => SendMessage(new CloseEuiMessage());
}
