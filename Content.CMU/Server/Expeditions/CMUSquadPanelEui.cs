using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Eui;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUSquadPanelEui : BaseEui
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IGameTiming _timing = default!;
    private CMUExpeditionAgentSystem _agents = default!;
    private NetEntity? _selected;
    private string _status = "";
    private TimeSpan _nextRequest;
    private TimeSpan _nextRefresh;

    public CMUSquadPanelEui(NetEntity? selected = null)
    {
        _selected = selected;
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        _agents = _systems.GetEntitySystem<CMUExpeditionAgentSystem>();
        _admin.OnPermsChanged += OnPermissions;
        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }
        StateDirty();
    }

    public override void Closed() => _admin.OnPermsChanged -= OnPermissions;

    private void OnPermissions(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admin.HasAdminFlag(Player, AdminFlags.Admin))
            Close();
    }

    public override EuiStateBase GetNewState() => _admin.HasAdminFlag(Player, AdminFlags.Admin)
        ? _agents.SquadPanelState(_selected, _status) : new CMUSquadPanelState();

    public override void HandleMessage(EuiMessageBase message)
    {
        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }
        base.HandleMessage(message);
        if (message is not CMUSquadPanelMessage request)
            return;
        if (request.Action == CMUSquadPanelAction.Refresh)
        {
            if (_timing.CurTime < _nextRefresh)
                return;
            _nextRefresh = _timing.CurTime + TimeSpan.FromSeconds(.8);
        }
        else
        {
            if (_timing.CurTime < _nextRequest)
                return;
            _nextRequest = _timing.CurTime + TimeSpan.FromSeconds(.1);
        }
        _status = _agents.ControlSquad(Player, request, ref _selected);
        StateDirty();
    }
}
