using Content.Server.Administration.Managers;
using Content.Shared.Actions;
using Content.Shared.Administration;
using Content.Shared.CMU14.Round.Antags.Rider;
using Content.Shared.Mind;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.Rider;

public sealed class RiderLocatorSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityManager _entities = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        SubscribeLocalEvent<RiderLocatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RiderLocatorComponent, RiderLocatorActionEvent>(OnAction);
        SubscribeLocalEvent<RiderLocatorComponent, RiderLocatorFollowMessage>(OnFollow);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;

        var query = EntityQueryEnumerator<RiderLocatorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, RiderLocatorUiKey.Key))
                UpdateUi(uid);
        }
    }

    private void OnMapInit(Entity<RiderLocatorComponent> ent, ref MapInitEvent args)
        => _actions.AddAction(ent, "ActionRiderLocator");

    private void OnAction(Entity<RiderLocatorComponent> ent, ref RiderLocatorActionEvent args)
    {
        if (args.Handled)
            return;

        _ui.OpenUi(ent.Owner, RiderLocatorUiKey.Key, ent.Owner);
        UpdateUi(ent.Owner);
        args.Handled = true;
    }

    private void UpdateUi(EntityUid uid)
    {
        var state = new RiderLocatorState();

        var query = EntityQueryEnumerator<RiderComponent>();
        while (query.MoveNext(out var rider, out var comp))
        {
            var latched = comp.Host is { } host && Exists(host);
            var hostName = latched ? Name(comp.Host!.Value) : Loc.GetString("rider-locator-free");
            state.Riders.Add(new RiderLocatorEntry(
                GetNetEntity(rider),
                Name(rider),
                hostName,
                latched));
        }

        _ui.SetUiState(uid, RiderLocatorUiKey.Key, state);
    }

    private void OnFollow(Entity<RiderLocatorComponent> ent, ref RiderLocatorFollowMessage args)
    {
        if (!_admin.HasAdminFlag(args.Actor, AdminFlags.Admin))
            return;

        var rider = _entities.GetEntity(args.Rider);
        if (!Exists(rider) || TerminatingOrDeleted(rider))
            return;

        _xform.SetCoordinates(args.Actor, _xform.GetMoverCoordinates(rider));
    }
}
