using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Hypospray;

public sealed partial class CMUHypospraySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUHyposprayStationComponent, MapInitEvent>(OnStationMapInit);
        SubscribeLocalEvent<CMUHyposprayStationComponent, EntityTerminatingEvent>(OnStationTerminating);
        SubscribeLocalEvent<CMUHyposprayStationComponent, InteractHandEvent>(OnStationInteractHand);
        SubscribeLocalEvent<CMUHyposprayStationComponent, InteractUsingEvent>(OnStationInteractUsing);

        SubscribeLocalEvent<CMUHyposprayComponent, EntityTerminatingEvent>(OnInjectorTerminating);
    }

    private void OnStationMapInit(Entity<CMUHyposprayStationComponent> ent, ref MapInitEvent args)
    {
        var container = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);

        // A saved map already carries its injector, so bind it instead of spawning a second one.
        EntityUid? injector;
        if (container.ContainedEntities.Count > 0)
        {
            injector = container.ContainedEntities[0];
        }
        else if (!TrySpawnInContainer(ent.Comp.InjectorId, ent, ent.Comp.ContainerId, out injector))
        {
            Log.Error($"Hypospray station {ToPrettyString(ent)} failed to spawn injector {ent.Comp.InjectorId}.");
            return;
        }

        // The station must never issue an untethered injector; a bad InjectorId would leave one unbound.
        if (!TryComp(injector, out CMUHyposprayComponent? injectorComp))
        {
            Log.Error($"Hypospray station {ToPrettyString(ent)} holds injector {ToPrettyString(injector.Value)} without {nameof(CMUHyposprayComponent)}, deleting it.");
            QueueDel(injector.Value);
            return;
        }

        ent.Comp.Injector = injector;
        Dirty(ent);
        injectorComp.Station = ent;
        Dirty(injector.Value, injectorComp);
    }

    private void OnStationTerminating(Entity<CMUHyposprayStationComponent> ent, ref EntityTerminatingEvent args)
    {
        // The injector is held or on the floor when out, so it has to go with the station.
        if (_net.IsServer && ent.Comp.Injector is { } injector)
            QueueDel(injector);
    }

    private void OnInjectorTerminating(Entity<CMUHyposprayComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!TryComp(ent.Comp.Station, out CMUHyposprayStationComponent? station)
            || station.Injector != ent.Owner)
            return;

        station.Injector = null;
        Dirty(ent.Comp.Station.Value, station);
    }

    private void OnStationInteractHand(Entity<CMUHyposprayStationComponent> ent, ref InteractHandEvent args)
    {
        if (_timing.ApplyingState
            || args.Handled
            || ent.Comp.Injector is not { } injector
            || !_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container)
            || !container.Contains(injector))
            return;

        args.Handled = true;
        _container.Remove(injector, container);
        _hands.TryPickupAnyHand(args.User, injector);

        EnsureComp<CMUHyposprayDeployedComponent>(injector);

        _audio.PlayPredicted(ent.Comp.PickupSound, ent, args.User);
    }

    private void OnStationInteractUsing(Entity<CMUHyposprayStationComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || ent.Comp.Injector != args.Used)
            return;

        args.Handled = true;
        ReturnInjector(ent, args.Used, args.User);
    }

    private bool ReturnInjector(Entity<CMUHyposprayStationComponent> station, EntityUid injector, EntityUid? user)
    {
        if (!_container.TryGetContainer(station, station.Comp.ContainerId, out var container))
            return false;

        var returned = user != null
            ? _hands.TryDropIntoContainer(user.Value, injector, container)
            : _container.Insert(injector, container);

        if (!returned)
            return false;

        RemCompDeferred<CMUHyposprayDeployedComponent>(injector);

        _audio.PlayPredicted(station.Comp.ReturnSound, station, user);

        return true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUHyposprayDeployedComponent, CMUHyposprayComponent>();
        while (query.MoveNext(out var uid, out _, out var injector))
        {
            if (!TryComp(injector.Station, out CMUHyposprayStationComponent? station))
            {
                RemCompDeferred<CMUHyposprayDeployedComponent>(uid);
                continue;
            }

            var injectorPosition = _transform.GetMoverCoordinates(uid);
            var stationPosition = _transform.GetMoverCoordinates(injector.Station.Value);

            if (injectorPosition.TryDistance(EntityManager, _transform, stationPosition, out var distance)
                && distance <= station.Range)
                continue;

            // A failed return is retried next tick, so only announce a successful one.
            if (ReturnInjector((injector.Station.Value, station), uid, null))
            {
                _popup.PopupEntity(Loc.GetString("cmu-hypospray-snap-back", ("injector", uid), ("station", injector.Station.Value)), uid, PopupType.MediumCaution);
                _audio.PlayPvs(station.SnapSound, uid);
            }
        }
    }
}
