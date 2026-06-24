using System;
using Content.Shared._RMC14.Input;
using Content.Shared._RMC14.Inventory;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Input;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleSpotlightSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private VehicleSystem _rmcVehicles = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleSpotlightComponent, ComponentStartup>(OnSpotlightStartup);
        SubscribeLocalEvent<HardpointSlotsChangedEvent>(OnHardpointSlotsChanged);

        if (_net.IsClient)
        {
            CommandBinds.Builder
                .Bind(ContentKeyFunctions.FlipObject, new VehicleSpotlightInputCmdHandler(this))
                .BindBefore(CMKeyFunctions.CMHolsterPrimary,
                    new VehicleSpotlightInputCmdHandler(this),
                    [typeof(SharedCMInventorySystem)])
                .Register<VehicleSpotlightSystem>();
        }

        SubscribeNetworkEvent<VehicleSpotlightToggleRequestEvent>(OnSpotlightToggleRequest);
    }

    private bool TryRequestToggle(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } user)
            return false;

        if (!TryGetSpotlightVehicle(user, out var vehicle))
            return false;

        RaiseNetworkEvent(new VehicleSpotlightToggleRequestEvent(GetNetEntity(vehicle)));
        return true;
    }

    private bool TryGetSpotlightVehicle(EntityUid user, out EntityUid vehicle)
    {
        vehicle = EntityUid.Invalid;
        if (!_rmcVehicles.TryResolveControlledVehicle(user, out var vehicleUid))
            return false;

        if (!HasComp<VehicleSpotlightComponent>(vehicleUid))
            return false;

        vehicle = vehicleUid;
        return true;
    }

    private void OnSpotlightStartup(Entity<VehicleSpotlightComponent> ent, ref ComponentStartup args)
    {
        EnsureBase(ent.Comp);
        if (_net.IsServer)
            RecalculateFromHardpoints(ent.Owner, ent.Comp);

        ApplySpotlight(ent.Owner, ent.Comp);
    }

    private void OnHardpointSlotsChanged(HardpointSlotsChangedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp(args.Vehicle, out VehicleSpotlightComponent? spotlight))
            return;

        RecalculateFromHardpoints(args.Vehicle, spotlight);
        ApplySpotlight(args.Vehicle, spotlight);
        Dirty(args.Vehicle, spotlight);
    }

    private void OnSpotlightToggleRequest(VehicleSpotlightToggleRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_net.IsServer)
            return;

        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var vehicle = GetEntity(ev.Vehicle);
        if (!TryComp(vehicle, out VehicleComponent? vehicleComp) ||
            vehicleComp.Operator != user &&
            (!_rmcVehicles.TryGetVehicleFromInterior(user, out var interiorVehicle) || interiorVehicle != vehicle))
        {
            return;
        }

        if (!TryComp(vehicle, out VehicleSpotlightComponent? spotlight))
            return;

        spotlight.Enabled = !spotlight.Enabled;
        ApplySpotlight(vehicle, spotlight);
        Dirty(vehicle, spotlight);
    }

    private void ApplySpotlight(EntityUid uid, VehicleSpotlightComponent spotlight)
    {
        SharedPointLightComponent? light = null;
        if (!_lights.ResolveLight(uid, ref light))
            return;

        _lights.SetRadius(uid, spotlight.Radius, light);
        _lights.SetEnergy(uid, spotlight.Energy, light);
        _lights.SetSoftness(uid, spotlight.Softness, light);
        _lights.SetEnabled(uid, spotlight.Enabled, light);
    }

    private static void EnsureBase(VehicleSpotlightComponent spotlight)
    {
        if (spotlight.BaseInitialized)
            return;

        spotlight.BaseInitialized = true;
        spotlight.BaseRadius = spotlight.Radius;
        spotlight.BaseEnergy = spotlight.Energy;
        spotlight.BaseSoftness = spotlight.Softness;
    }

    private void RecalculateFromHardpoints(
        EntityUid vehicle,
        VehicleSpotlightComponent spotlight,
        HardpointSlotsComponent? hardpoints = null,
        ItemSlotsComponent? itemSlots = null)
    {
        EnsureBase(spotlight);

        var radius = spotlight.BaseRadius;
        var energy = spotlight.BaseEnergy;
        var softness = spotlight.BaseSoftness;

        if (Resolve(vehicle, ref hardpoints, ref itemSlots, logMissing: false))
        {
            foreach (var slot in hardpoints.Slots)
            {
                if (string.IsNullOrWhiteSpace(slot.Id))
                    continue;

                if (!_itemSlots.TryGetSlot(vehicle, slot.Id, out var itemSlot, itemSlots) || !itemSlot.HasItem)
                    continue;

                var item = itemSlot.Item!.Value;
                if (!TryComp(item, out VehicleSpotlightModifierComponent? modifier))
                    continue;

                var performance = _hardpoints.GetHardpointPerformanceMultiplier(item);
                if (performance <= 0f)
                    continue;

                radius = radius * ScaleMultiplierTowardNeutral(modifier.RadiusMultiplier, performance) +
                         modifier.RadiusAdd * performance;
                energy = energy * ScaleMultiplierTowardNeutral(modifier.EnergyMultiplier, performance) +
                         modifier.EnergyAdd * performance;
                softness = softness * ScaleMultiplierTowardNeutral(modifier.SoftnessMultiplier, performance) +
                           modifier.SoftnessAdd * performance;
            }
        }

        spotlight.Radius = radius;
        spotlight.Energy = energy;
        spotlight.Softness = softness;
    }

    private static float ScaleMultiplierTowardNeutral(float multiplier, float performance)
    {
        var clamped = Math.Clamp(performance, 0f, 1f);
        return 1f + (multiplier - 1f) * clamped;
    }

    private sealed class VehicleSpotlightInputCmdHandler(VehicleSpotlightSystem system) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            if (message.State != BoundKeyState.Down)
                return false;

            return system.TryRequestToggle(session);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class VehicleSpotlightToggleRequestEvent : EntityEventArgs
{
    public NetEntity Vehicle;

    public VehicleSpotlightToggleRequestEvent()
    {
    }

    public VehicleSpotlightToggleRequestEvent(NetEntity vehicle)
    {
        Vehicle = vehicle;
    }
}
