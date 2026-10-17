using System.Linq;
using Content.Shared._RMC14.SupplyDrop;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Unlatching a vehicle crate by hand: after a do-after the vehicle stands where the crate was and
/// the crate's walls fall flat around it.
/// </summary>
public sealed class CMUVehicleCrateSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleCrateComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CMUVehicleCrateComponent, CMUVehicleCrateOpenDoAfterEvent>(OnOpen);
    }

    private void OnInteractHand(Entity<CMUVehicleCrateComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryStartOpening(ent, args.User);
    }

    public bool TryStartOpening(Entity<CMUVehicleCrateComponent> ent, EntityUid user)
    {
        // Still coming down, or only just landed.
        if (HasComp<BeingSupplyDroppedComponent>(ent))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-crate-landing"), ent, user);
            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.OpenDelay, new CMUVehicleCrateOpenDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _audio.PlayPredicted(ent.Comp.UnlatchSound, ent, user);
        _popup.PopupClient(Loc.GetString("cmu-vehicle-crate-unlatching", ("crate", ent.Owner)), ent, user);
        return true;
    }

    private void OnOpen(Entity<CMUVehicleCrateComponent> ent, ref CMUVehicleCrateOpenDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        // Two players finishing in the same tick: the first one opened it.
        if (_net.IsClient || TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent))
            return;

        Open(ent);
    }

    /// <summary>
    /// Stands the vehicle where the crate is, facing south, and leaves the crate's walls falling round it.
    /// </summary>
    public EntityUid Open(Entity<CMUVehicleCrateComponent> ent)
    {
        var coordinates = Transform(ent).Coordinates;
        var vehicle = SpawnAttachedTo(ent.Comp.Vehicle, coordinates);
        _transform.SetWorldRotation(vehicle, Angle.Zero);

        if (ent.Comp.BareSlot is { } bareSlot && _containers.TryGetContainer(vehicle, bareSlot, out var bare))
        {
            foreach (var item in bare.ContainedEntities.ToArray())
            {
                Del(item);
            }
        }

        if (ent.Comp.Wheels is { } wheels)
            SwapWheels(vehicle, wheels);

        if (ent.Comp.Paints.Count > 0 && TryComp(vehicle, out CMUVehiclePaintComponent? paint))
        {
            paint.Color = _random.Pick(ent.Comp.Paints);
            Dirty(vehicle, paint);
        }

        // The floor stays under the vehicle; the walls falling over it go with the floor.
        var floor = SpawnAttachedTo(ent.Comp.Remains, coordinates);
        _transform.SetWorldRotation(floor, Angle.Zero);
        var walls = SpawnAttachedTo(ent.Comp.Walls, new EntityCoordinates(floor, default));
        foreach (var part in new[] { floor, walls })
        {
            if (!TryComp(part, out CMUVehicleCrateRemainsComponent? remains))
                continue;

            remains.OpenedAt = _timing.CurTime;
            Dirty(part, remains);
        }

        if (TryComp(floor, out CMUVehicleCrateRemainsComponent? fallen))
            _audio.PlayPvs(fallen.FallSound, floor);

        QueueDel(ent);
        return vehicle;
    }

    /// <summary>
    /// Swaps every wheel set aboard, fitted or stowed, for another kind.
    /// </summary>
    private void SwapWheels(EntityUid vehicle, EntProtoId wheels)
    {
        if (!TryComp(vehicle, out ContainerManagerComponent? manager))
            return;

        foreach (var container in _containers.GetAllContainers(vehicle, manager).ToArray())
        {
            foreach (var old in container.ContainedEntities.ToArray())
            {
                if (!HasComp<CMUVehicleTyreComponent>(old))
                    continue;

                Del(old);
                SpawnInContainerOrDrop(wheels, vehicle, container.ID);
            }
        }
    }
}
