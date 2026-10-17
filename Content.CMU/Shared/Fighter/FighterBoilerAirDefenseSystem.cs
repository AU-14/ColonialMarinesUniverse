using Content.Shared._RMC14.Xenonids.Bombard;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Fighter;

public sealed partial class FighterBoilerAirDefenseSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoBombardComponent, MapInitEvent>(OnBoilerInit);
        SubscribeLocalEvent<FighterBoilerAirDefenseComponent, FighterBoilerWatchAirspaceEvent>(OnWatch);
        SubscribeLocalEvent<FighterBoilerAirDefenseComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnBoilerInit(Entity<XenoBombardComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient) return;
        var air = EnsureComp<FighterBoilerAirDefenseComponent>(ent);
        _actions.AddAction(ent, ref air.Action, air.ActionId);
        Dirty(ent, air);
    }

    public bool CanWatch(EntityUid user) => HasComp<XenoBombardComponent>(user) &&
        _blocker.CanConsciouslyPerformAction(user) && !_containers.IsEntityOrParentInContainer(user);

    private void OnWatch(Entity<FighterBoilerAirDefenseComponent> ent, ref FighterBoilerWatchAirspaceEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner || !CanWatch(ent)) return;
        args.Handled = true;
        SetWatching(ent, !ent.Comp.Watching);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Watching ? "cmu-boiler-air-watching" : "cmu-boiler-air-lowered"), ent, ent);
    }

    public void SetWatching(Entity<FighterBoilerAirDefenseComponent> ent, bool watching)
    {
        if (ent.Comp.Watching == watching) return;
        ent.Comp.Watching = watching;
        _actions.SetToggled(ent.Comp.Action, watching);
        Dirty(ent);
        if (!watching)
        {
            var stopped = new FighterBoilerAirDefenseStoppedEvent();
            RaiseLocalEvent(ent, ref stopped);
        }
    }

    private void OnShutdown(Entity<FighterBoilerAirDefenseComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient) return;
        _actions.RemoveAction(ent.Comp.Action);
        var stopped = new FighterBoilerAirDefenseStoppedEvent();
        RaiseLocalEvent(ent, ref stopped);
    }
}
