using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.CMU14.Lobby;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Lobby;

public sealed partial class CMUEventSetupEui : BaseEui
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntitySystemManager _systems = default!;

    private readonly CMUEventAccessSystem _access;
    private readonly CMUEventSlotsSystem _slots;
    private readonly CMUEventJobTargetsSystem _targets;
    private readonly GameTicker _ticker;

    private string _feedback = string.Empty;

    public CMUEventSetupEui()
    {
        IoCManager.InjectDependencies(this);
        _access = _systems.GetEntitySystem<CMUEventAccessSystem>();
        _slots = _systems.GetEntitySystem<CMUEventSlotsSystem>();
        _targets = _systems.GetEntitySystem<CMUEventJobTargetsSystem>();
        _ticker = _systems.GetEntitySystem<GameTicker>();
    }

    private bool IsAllowed => _admins.HasAdminFlag(Player, AdminFlags.Admin);

    public override void Opened()
    {
        base.Opened();
        if (!IsAllowed)
        {
            Close();
            return;
        }
        _access.Changed += OnChanged;
        _slots.Changed += OnChanged;
        _admins.OnPermsChanged += OnPermsChanged;
        StateDirty();
    }

    public override void Closed()
    {
        _access.Changed -= OnChanged;
        _slots.Changed -= OnChanged;
        _admins.OnPermsChanged -= OnPermsChanged;
        base.Closed();
    }

    public override EuiStateBase GetNewState()
    {
        // A state already queued before deadminning must not disclose the event configuration.
        if (!IsAllowed)
            return new CMUEventSetupState(false, false, [], [], [], new(), string.Empty);

        var sides = _targets.GetSides();
        var roster = CMUEventJobCatalog.GetJobs(_prototypes);
        var repeatedNames = roster.GroupBy(job => (job.RoundSide, job.LocalizedName))
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        var jobs = roster
            .Select(job => new CMUEventJobInfo(job.ID,
                repeatedNames.Contains((job.RoundSide, job.LocalizedName))
                    ? Loc.GetString("cmu-event-role-variant", ("role", job.LocalizedName), ("id", job.ID))
                    : job.LocalizedName, job.RoundSide.ToString().ToLowerInvariant()))
            .OrderBy(job => job.Name).ThenBy(job => job.Id).ToList();
        return new CMUEventSetupState(_access.Enabled, _ticker.RunLevel == GameRunLevel.PreRoundLobby,
            sides, jobs, _access.GetGroups(), sides.ToDictionary(side => side.Id, side => _slots.Describe(side.Id)), _feedback);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (IsShutDown)
            return;
        base.HandleMessage(msg);
        if (!IsAllowed)
        {
            Close();
            return;
        }
        if (msg is not CMUEventSetupMessage request)
            return;

        switch (request.Action)
        {
            case CMUEventSetupAction.Enable:
            case CMUEventSetupAction.Disable:
                _access.SetEnabled(request.Action == CMUEventSetupAction.Enable);
                _feedback = Loc.GetString(_access.Enabled ? "cmu-event-enabled" : "cmu-event-disabled");
                break;
            case CMUEventSetupAction.SaveGroup:
                _access.TryConfigureGroup(request.Group, request.Code, request.Side, request.Roles, out _feedback);
                break;
            case CMUEventSetupAction.RemoveGroup:
                _feedback = Loc.GetString(_access.RemoveGroup(request.Group)
                    ? "cmu-event-access-removed" : "cmu-event-access-not-found", ("group", request.Group));
                break;
            case CMUEventSetupAction.AddSlots:
            case CMUEventSetupAction.SetSlots:
                if (request.Roles.Count != 1)
                    _feedback = Loc.GetString("cmu-event-setup-select-one-role");
                else
                    _slots.TryChange(request.Side, request.Roles[0], request.Amount,
                        request.Action == CMUEventSetupAction.SetSlots, out _feedback);
                break;
            case CMUEventSetupAction.ClearPending:
                _slots.ClearPending();
                _feedback = Loc.GetString("cmu-event-slots-cleared");
                break;
            case CMUEventSetupAction.Refresh:
                break;
            default:
                return;
        }
        StateDirty();
    }

    private void OnChanged()
    {
        if (IsAllowed)
            StateDirty();
        else
            Close();
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !IsAllowed)
            Close();
    }
}
