using Content.Shared._RMC14.Vehicle;
using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Gives an open vehicle's driver buttons for the hazard lights, the automatic turn signals, the horn,
/// the headlight switch and taking the key out of the ignition while they drive.
/// The horn is the vehicle's own <see cref="VehicleSoundComponent.HornSound"/>, with its cooldown.
/// </summary>
public sealed class CMUVehicleDriverActionsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private CMUJeepSystem _jeep = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleSeatsComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<CMUVehicleDriverActionsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VehicleOperatorComponent, CMUVehicleHazardsActionEvent>(OnHazards);
        SubscribeLocalEvent<VehicleOperatorComponent, CMUVehicleHornActionEvent>(OnHorn);
        SubscribeLocalEvent<VehicleOperatorComponent, CMUVehicleHeadlightsActionEvent>(OnHeadlights);
        SubscribeLocalEvent<VehicleOperatorComponent, CMUVehicleAutoSignalsActionEvent>(OnAutoSignals);
        SubscribeLocalEvent<VehicleOperatorComponent, CMUVehicleEjectKeyActionEvent>(OnEjectKey);
    }

    private void OnOperatorSet(Entity<CMUVehicleSeatsComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (_net.IsClient)
            return;

        var actions = EnsureComp<CMUVehicleDriverActionsComponent>(ent);
        RemoveActions(actions);
        if (args.NewOperator is not { } driver)
            return;

        _actions.AddAction(driver, ref actions.HazardsActionEntity, actions.HazardsAction);
        _actions.AddAction(driver, ref actions.AutoSignalsActionEntity, actions.AutoSignalsAction);
        _actions.AddAction(driver, ref actions.HornActionEntity, actions.HornAction);
        _actions.SetToggled(actions.HazardsActionEntity, actions.Hazards);
        _actions.SetToggled(actions.AutoSignalsActionEntity, actions.AutoSignals);
        if (HasComp<CMUVehicleHeadlightsComponent>(ent))
        {
            _actions.AddAction(driver, ref actions.HeadlightsActionEntity, actions.HeadlightsAction);
            _jeep.RefreshHeadlightsAction(ent.Owner);
        }

        if (HasComp<CMUVehicleIgnitionComponent>(ent))
            _actions.AddAction(driver, ref actions.EjectKeyActionEntity, actions.EjectKeyAction);
    }

    private void OnShutdown(Entity<CMUVehicleDriverActionsComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsServer)
            RemoveActions(ent.Comp);
    }

    /// <summary>
    /// Takes the buttons from the driver and deletes them; each driver gets new ones. Server only.
    /// </summary>
    private void RemoveActions(CMUVehicleDriverActionsComponent actions)
    {
        _actions.RemoveAction(actions.HazardsActionEntity);
        _actions.RemoveAction(actions.HornActionEntity);
        _actions.RemoveAction(actions.HeadlightsActionEntity);
        _actions.RemoveAction(actions.AutoSignalsActionEntity);
        _actions.RemoveAction(actions.EjectKeyActionEntity);
        QueueDel(actions.HazardsActionEntity);
        QueueDel(actions.HornActionEntity);
        QueueDel(actions.HeadlightsActionEntity);
        QueueDel(actions.AutoSignalsActionEntity);
        QueueDel(actions.EjectKeyActionEntity);
        actions.HazardsActionEntity = null;
        actions.HornActionEntity = null;
        actions.HeadlightsActionEntity = null;
        actions.AutoSignalsActionEntity = null;
        actions.EjectKeyActionEntity = null;
    }

    private void OnHazards(Entity<VehicleOperatorComponent> ent, ref CMUVehicleHazardsActionEvent args)
    {
        if (args.Handled || !TryGetDriven(ent, args.Performer, out var vehicle, out var actions))
            return;

        args.Handled = true;
        actions.Hazards = !actions.Hazards;
        Dirty(vehicle, actions);
        _actions.SetToggled(actions.HazardsActionEntity, actions.Hazards);
    }

    private void OnAutoSignals(Entity<VehicleOperatorComponent> ent, ref CMUVehicleAutoSignalsActionEvent args)
    {
        if (args.Handled || !TryGetDriven(ent, args.Performer, out var vehicle, out var actions))
            return;

        args.Handled = true;
        actions.AutoSignals = !actions.AutoSignals;
        Dirty(vehicle, actions);
        _actions.SetToggled(actions.AutoSignalsActionEntity, actions.AutoSignals);
    }

    private void OnHorn(Entity<VehicleOperatorComponent> ent, ref CMUVehicleHornActionEvent args)
    {
        if (args.Handled || !TryGetDriven(ent, args.Performer, out var vehicle, out _))
            return;

        args.Handled = true;
        if (_net.IsClient || !TryComp(vehicle, out VehicleSoundComponent? sound) || sound.HornSound == null)
            return;

        var now = _timing.CurTime;
        if (sound.NextHornSound > now)
            return;

        sound.NextHornSound = now + TimeSpan.FromSeconds(sound.HornCooldown);
        _audio.PlayPvs(sound.HornSound, vehicle);
        Dirty(vehicle, sound);
    }

    private void OnHeadlights(Entity<VehicleOperatorComponent> ent, ref CMUVehicleHeadlightsActionEvent args)
    {
        if (args.Handled ||
            !TryGetDriven(ent, args.Performer, out var vehicle, out _) ||
            !TryComp(vehicle, out CMUVehicleHeadlightsComponent? lights))
        {
            return;
        }

        args.Handled = true;
        _jeep.CycleHeadlights((vehicle, lights), args.Performer);
    }

    /// <summary>
    /// The driver pulls the key from the ignition into their hand, which stops the engine.
    /// </summary>
    private void OnEjectKey(Entity<VehicleOperatorComponent> ent, ref CMUVehicleEjectKeyActionEvent args)
    {
        if (args.Handled ||
            !TryGetDriven(ent, args.Performer, out var vehicle, out _) ||
            !TryComp(vehicle, out CMUVehicleIgnitionComponent? ignition))
        {
            return;
        }

        args.Handled = true;
        if (!ignition.KeySlot.HasItem)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-ignition-examine-no-key"), vehicle, args.Performer);
            return;
        }

        _itemSlots.TryEjectToHands(vehicle, ignition.KeySlot, args.Performer, true);
    }

    private bool TryGetDriven(
        Entity<VehicleOperatorComponent> driver,
        EntityUid performer,
        out EntityUid vehicle,
        out CMUVehicleDriverActionsComponent actions)
    {
        vehicle = default;
        actions = default!;
        if (driver.Comp.Vehicle is not { } driven ||
            !TryComp(driven, out VehicleComponent? vehicleComp) ||
            vehicleComp.Operator != performer ||
            !TryComp(driven, out CMUVehicleDriverActionsComponent? found))
        {
            return false;
        }

        vehicle = driven;
        actions = found;
        return true;
    }
}
