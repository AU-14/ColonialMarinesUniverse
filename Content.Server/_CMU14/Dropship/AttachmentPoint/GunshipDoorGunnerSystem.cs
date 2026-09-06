using Content.Server.Shuttles.Components;
using Content.Shared._CMU14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Dropship.AttachmentPoint;

/// <summary>
/// Links an MTU-4B deployed from a dedicated hardpoint to an adjacent exterior
/// dropship door. The deployed mount moves into the doorway and holds an
/// unlocked door open until it is retracted.
/// </summary>
public sealed partial class GunshipDoorGunnerSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunshipDoorGunnerAttachmentComponent, RMCEquipmentDeployedEvent>(OnEquipmentDeployed);
        SubscribeLocalEvent<ActiveGunshipDoorGunnerComponent, StrappedEvent>(OnGunnerStrapped);
        SubscribeLocalEvent<ActiveGunshipDoorGunnerComponent, ComponentShutdown>(OnGunnerShutdown);
        SubscribeLocalEvent<GunshipDoorGunnerHeldOpenComponent, BeforeDoorClosedEvent>(OnHeldDoorClosing);
    }

    private void OnEquipmentDeployed(
        Entity<GunshipDoorGunnerAttachmentComponent> attachment,
        ref RMCEquipmentDeployedEvent args)
    {
        if (!args.Deployed)
        {
            if (TryComp(args.Equipment, out ActiveGunshipDoorGunnerComponent? active))
            {
                ReleaseDoor((args.Equipment, active));
                RemCompDeferred<ActiveGunshipDoorGunnerComponent>(args.Equipment);
            }

            return;
        }

        var point = Transform(attachment).ParentUid;
        if (!HasComp<GunshipHardpointAttachmentPointComponent>(point))
            return;

        var gunner = EnsureComp<ActiveGunshipDoorGunnerComponent>(args.Equipment);
        gunner.AttachmentPoint = point;

        if (!TryFindAdjacentExteriorDoor(point, out var door, out var doorComp, out var bolt) || bolt.BoltsDown)
            return;

        MoveIntoDoorway(args.Equipment, point, door);
        HoldDoor((args.Equipment, gunner), (door, doorComp));
    }

    private void OnGunnerStrapped(Entity<ActiveGunshipDoorGunnerComponent> gunner, ref StrappedEvent args)
    {
        if (gunner.Comp.HeldDoor != null ||
            gunner.Comp.AttachmentPoint is not { } point ||
            !TryFindAdjacentExteriorDoor(point, out var door, out var doorComp, out var bolt) ||
            bolt.BoltsDown)
        {
            return;
        }

        MoveIntoDoorway(gunner.Owner, point, door);
        HoldDoor(gunner, (door, doorComp));
    }

    private void MoveIntoDoorway(EntityUid gunner, EntityUid point, EntityUid door)
    {
        var pointXform = Transform(point);
        var doorXform = Transform(door);
        var outward = doorXform.LocalPosition - pointXform.LocalPosition;

        _transform.SetCoordinates(gunner,
            new EntityCoordinates(doorXform.ParentUid, doorXform.LocalPosition));
        _transform.SetLocalRotation(gunner, outward.ToWorldAngle());
    }

    private void HoldDoor(Entity<ActiveGunshipDoorGunnerComponent> gunner, Entity<DoorComponent> door)
    {
        var heldOpen = EnsureComp<GunshipDoorGunnerHeldOpenComponent>(door);
        heldOpen.Holders.Add(gunner.Owner);
        gunner.Comp.HeldDoor = door.Owner;

        if (door.Comp.State is not DoorState.Open and not DoorState.Opening)
            _doors.StartOpening(door.Owner, door.Comp, gunner.Owner);
    }

    private void OnGunnerShutdown(Entity<ActiveGunshipDoorGunnerComponent> gunner, ref ComponentShutdown args)
    {
        ReleaseDoor(gunner);
    }

    private void OnHeldDoorClosing(Entity<GunshipDoorGunnerHeldOpenComponent> door, ref BeforeDoorClosedEvent args)
    {
        door.Comp.Holders.RemoveWhere(holder => TerminatingOrDeleted(holder));
        if (door.Comp.Holders.Count > 0)
            args.Cancel();
    }

    private void ReleaseDoor(Entity<ActiveGunshipDoorGunnerComponent> gunner)
    {
        if (gunner.Comp.HeldDoor is not { } door)
            return;

        gunner.Comp.HeldDoor = null;
        if (TerminatingOrDeleted(door) || !TryComp(door, out GunshipDoorGunnerHeldOpenComponent? heldOpen))
            return;

        heldOpen.Holders.Remove(gunner.Owner);
        if (heldOpen.Holders.Count > 0)
            return;

        RemCompDeferred<GunshipDoorGunnerHeldOpenComponent>(door);
        if (TryComp(door, out DoorComponent? doorComp))
            _doors.TryClose(door, doorComp);
    }

    private bool TryFindAdjacentExteriorDoor(
        EntityUid point,
        out EntityUid nearest,
        out DoorComponent nearestDoor,
        out DoorBoltComponent nearestBolt)
    {
        nearest = default;
        nearestDoor = default!;
        nearestBolt = default!;

        var xform = Transform(point);
        if (xform.GridUid is not { } grid)
            return false;

        var pointPosition = xform.LocalPosition;
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!HasComp<DockingComponent>(child) ||
                !TryComp(child, out DoorComponent? door) ||
                door.Location is DoorLocation.None or DoorLocation.Cockpit ||
                !TryComp(child, out DoorBoltComponent? bolt))
            {
                continue;
            }

            var doorXform = Transform(child);
            if (doorXform.ParentUid != grid)
                continue;

            var delta = doorXform.LocalPosition - pointPosition;
            var cardinalDistance = MathF.Abs(delta.X) + MathF.Abs(delta.Y);
            if (!MathHelper.CloseTo(cardinalDistance, 1f) ||
                MathF.Min(MathF.Abs(delta.X), MathF.Abs(delta.Y)) > 0.01f)
            {
                continue;
            }

            nearest = child;
            nearestDoor = door;
            nearestBolt = bolt;
            return true;
        }

        return false;
    }
}
