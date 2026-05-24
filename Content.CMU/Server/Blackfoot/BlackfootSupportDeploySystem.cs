using System;
using System.Numerics;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._CMU14.Blackfoot;

public sealed partial class BlackfootSupportDeploySystem : EntitySystem
{
    private const CollisionGroup SupportBlockMask =
        CollisionGroup.Impassable |
        CollisionGroup.MidImpassable |
        CollisionGroup.HighImpassable |
        CollisionGroup.DropshipImpassable;

    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackfootDeployableSupportComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BlackfootDeployableSupportComponent, InteractUsingEvent>(OnDeployInteractUsing);
        SubscribeLocalEvent<BlackfootDeployableSupportComponent, BlackfootSupportDeployDoAfterEvent>(OnDeployDoAfter);

        SubscribeLocalEvent<BlackfootPackableSupportComponent, ActivateInWorldEvent>(OnPackableActivate);
        SubscribeLocalEvent<BlackfootPackableSupportComponent, InteractUsingEvent>(OnPackInteractUsing);
        SubscribeLocalEvent<BlackfootPackableSupportComponent, BlackfootSupportInitialWrenchDoAfterEvent>(OnInitialWrenchDoAfter);
        SubscribeLocalEvent<BlackfootPackableSupportComponent, BlackfootSupportPanelScrewdriverDoAfterEvent>(OnPanelScrewdriverDoAfter);
        SubscribeLocalEvent<BlackfootPackableSupportComponent, BlackfootSupportFinalWrenchDoAfterEvent>(OnFinalWrenchDoAfter);
    }

    private void OnActivate(Entity<BlackfootDeployableSupportComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _popup.PopupEntity(ent.Comp.ToolPopup, ent, args.User, PopupType.SmallCaution);
    }

    private void OnDeployInteractUsing(Entity<BlackfootDeployableSupportComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tool.HasQuality(args.Used, ent.Comp.DeployTool))
        {
            args.Handled = true;
            _popup.PopupEntity(ent.Comp.ToolPopup, ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!CanDeploy(ent, out var reason))
        {
            args.Handled = true;
            _popup.PopupEntity(reason, ent, args.User, PopupType.SmallCaution);
            return;
        }

        args.Handled = _tool.UseTool(
            args.Used,
            args.User,
            ent.Owner,
            ent.Comp.DeployDelay,
            ent.Comp.DeployTool,
            new BlackfootSupportDeployDoAfterEvent());
    }

    private void OnDeployDoAfter(Entity<BlackfootDeployableSupportComponent> ent, ref BlackfootSupportDeployDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!CanDeploy(ent, out var reason) ||
            !TryGetDeploymentTarget(ent, out var coordinates, out var rotation, out var linkedPad, out reason))
        {
            _popup.PopupEntity(reason, ent, args.User, PopupType.SmallCaution);
            return;
        }

        var deployed = Spawn(ent.Comp.Prototype, coordinates);
        _transform.SetLocalRotation(deployed, rotation);

        if (TryComp(deployed, out BlackfootLandingPadComponent? pad))
        {
            pad.State = BlackfootLandingPadState.Deployed;
            Dirty(deployed, pad);
        }
        else if (linkedPad is { } landingPad)
        {
            LinkPadAttachment(deployed, landingPad, ent.Comp.LandingPadAttachment);
        }

        if (ent.Comp.DeleteOnDeploy)
            QueueDel(ent.Owner);

        _popup.PopupEntity(ent.Comp.DeployPopup, deployed, args.User);
    }

    private void OnPackableActivate(Entity<BlackfootPackableSupportComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<BlackfootFlightComputerComponent>(ent.Owner))
            return;

        args.Handled = true;
        _popup.PopupEntity(GetPackToolPopup(ent.Comp), ent, args.User, PopupType.SmallCaution);
    }

    private void OnPackInteractUsing(Entity<BlackfootPackableSupportComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var tool = GetPackTool(ent.Comp);
        if (!_tool.HasQuality(args.Used, tool))
        {
            args.Handled = true;
            _popup.PopupEntity(GetPackToolPopup(ent.Comp), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!CanPack(ent, out var reason))
        {
            args.Handled = true;
            _popup.PopupEntity(reason, ent, args.User, PopupType.SmallCaution);
            return;
        }

        args.Handled = ent.Comp.Stage switch
        {
            BlackfootSupportPackStage.Secured => _tool.UseTool(
                args.Used,
                args.User,
                ent.Owner,
                ent.Comp.InitialDelay,
                ent.Comp.InitialTool,
                new BlackfootSupportInitialWrenchDoAfterEvent()),
            BlackfootSupportPackStage.AnchorsLoosened => _tool.UseTool(
                args.Used,
                args.User,
                ent.Owner,
                ent.Comp.PanelDelay,
                ent.Comp.PanelTool,
                new BlackfootSupportPanelScrewdriverDoAfterEvent()),
            BlackfootSupportPackStage.PanelOpen => _tool.UseTool(
                args.Used,
                args.User,
                ent.Owner,
                ent.Comp.FinalDelay,
                ent.Comp.FinalTool,
                new BlackfootSupportFinalWrenchDoAfterEvent()),
            _ => false,
        };
    }

    private void OnInitialWrenchDoAfter(Entity<BlackfootPackableSupportComponent> ent, ref BlackfootSupportInitialWrenchDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Stage != BlackfootSupportPackStage.Secured)
            return;

        if (!CanPack(ent, out var reason))
        {
            _popup.PopupEntity(reason, ent, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Stage = BlackfootSupportPackStage.AnchorsLoosened;
        Dirty(ent);
        _popup.PopupEntity(ent.Comp.InitialPopup, ent, args.User);
    }

    private void OnPanelScrewdriverDoAfter(Entity<BlackfootPackableSupportComponent> ent, ref BlackfootSupportPanelScrewdriverDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Stage != BlackfootSupportPackStage.AnchorsLoosened)
            return;

        if (!CanPack(ent, out var reason))
        {
            _popup.PopupEntity(reason, ent, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Stage = BlackfootSupportPackStage.PanelOpen;
        Dirty(ent);
        _popup.PopupEntity(ent.Comp.PanelPopup, ent, args.User);
    }

    private void OnFinalWrenchDoAfter(Entity<BlackfootPackableSupportComponent> ent, ref BlackfootSupportFinalWrenchDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Stage != BlackfootSupportPackStage.PanelOpen)
            return;

        if (!CanPack(ent, out var reason))
        {
            _popup.PopupEntity(reason, ent, args.User, PopupType.SmallCaution);
            return;
        }

        ClearSupportLinks(ent);

        var xform = Transform(ent.Owner);
        var packed = Spawn(ent.Comp.PackedPrototype, xform.Coordinates);
        _transform.SetLocalRotation(packed, xform.LocalRotation);
        QueueDel(ent.Owner);

        _popup.PopupEntity(ent.Comp.PackedPopup, packed, args.User);
    }

    private bool CanDeploy(Entity<BlackfootDeployableSupportComponent> ent, out string reason)
    {
        reason = string.Empty;
        if (!TryGetDeploymentTarget(ent, out var coordinates, out _, out _, out reason))
            return false;

        if (!ent.Comp.RequireClearFootprint)
            return true;

        return HasClearFootprint(ent.Owner, coordinates, ent.Comp.ClearFootprint, out reason);
    }

    private bool TryGetDeploymentTarget(
        Entity<BlackfootDeployableSupportComponent> ent,
        out EntityCoordinates coordinates,
        out Angle rotation,
        out Entity<BlackfootLandingPadComponent>? linkedPad,
        out string reason)
    {
        reason = string.Empty;
        linkedPad = null;

        var xform = Transform(ent.Owner);
        coordinates = xform.Coordinates.Offset(ent.Comp.Offset);
        rotation = xform.LocalRotation;

        if (!ent.Comp.RequireLandingPad)
            return true;

        if (!TryFindDeploymentPad(ent, out var pad, out reason))
            return false;

        linkedPad = pad;
        var padXform = Transform(pad.Owner);
        var offset = GetLandingPadAttachmentOffset(ent.Comp, pad.Comp);
        coordinates = padXform.Coordinates.Offset(padXform.LocalRotation.RotateVec(offset));
        rotation = padXform.LocalRotation;
        return true;
    }

    private static Vector2 GetLandingPadAttachmentOffset(
        BlackfootDeployableSupportComponent deploy,
        BlackfootLandingPadComponent pad)
    {
        return deploy.LandingPadAttachment switch
        {
            BlackfootLandingPadAttachment.FuelPump => pad.FuelPumpOffset,
            BlackfootLandingPadAttachment.FlightComputer => pad.FlightComputerOffset,
            _ => deploy.LandingPadOffset,
        };
    }

    private bool TryFindDeploymentPad(
        Entity<BlackfootDeployableSupportComponent> ent,
        out Entity<BlackfootLandingPadComponent> pad,
        out string reason)
    {
        pad = default;
        reason = string.Empty;

        var xform = Transform(ent.Owner);
        if (xform.MapUid is not { } mapUid)
        {
            reason = "Place the Blackfoot support case on a deployed landing pad.";
            return false;
        }

        var position = _transform.GetWorldPosition(ent.Owner);
        var bestDistance = float.MaxValue;
        var query = EntityQueryEnumerator<BlackfootLandingPadComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var padXform))
        {
            if (comp.State != BlackfootLandingPadState.Deployed ||
                padXform.MapUid != mapUid)
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(position, _transform.GetWorldPosition(uid));
            if (distance > ent.Comp.LandingPadSearchRange * ent.Comp.LandingPadSearchRange ||
                distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            pad = (uid, comp);
        }

        if (pad.Owner == default)
        {
            reason = "Place the Blackfoot support case on a deployed landing pad, then wrench it into the mount.";
            return false;
        }

        return CanUsePadAttachmentSlot(ent.Comp, pad, out reason);
    }

    private bool CanUsePadAttachmentSlot(
        BlackfootDeployableSupportComponent deploy,
        Entity<BlackfootLandingPadComponent> pad,
        out string reason)
    {
        reason = string.Empty;

        switch (deploy.LandingPadAttachment)
        {
            case BlackfootLandingPadAttachment.FuelPump:
                if (pad.Comp.FuelPump is { } pump && Exists(pump))
                {
                    reason = "This Blackfoot landing pad already has a mounted fuel pump.";
                    return false;
                }

                break;
            case BlackfootLandingPadAttachment.FlightComputer:
                var computers = EntityQueryEnumerator<BlackfootFlightComputerComponent>();
                while (computers.MoveNext(out var uid, out var computer))
                {
                    if (computer.LandingPad == pad.Owner && Exists(uid))
                    {
                        reason = "This Blackfoot landing pad already has a mounted flight computer.";
                        return false;
                    }
                }

                break;
        }

        return true;
    }

    private void LinkPadAttachment(
        EntityUid deployed,
        Entity<BlackfootLandingPadComponent> pad,
        BlackfootLandingPadAttachment attachment)
    {
        switch (attachment)
        {
            case BlackfootLandingPadAttachment.FuelPump:
                if (!TryComp(deployed, out BlackfootFuelPumpComponent? fuelPump))
                    break;

                fuelPump.LandingPad = pad.Owner;
                pad.Comp.FuelPump = deployed;
                Dirty(deployed, fuelPump);
                Dirty(pad);
                break;
            case BlackfootLandingPadAttachment.FlightComputer:
                if (!TryComp(deployed, out BlackfootFlightComputerComponent? computer))
                    break;

                computer.LandingPad = pad.Owner;
                Dirty(deployed, computer);
                break;
        }
    }

    private bool HasClearFootprint(EntityUid ignoredEntity, EntityCoordinates coordinates, Vector2i footprint, out string reason)
    {
        reason = string.Empty;

        if (!_turf.TryGetTileRef(coordinates, out var centerTile) ||
            centerTile.Value.Tile.IsEmpty ||
            !TryComp(centerTile.Value.GridUid, out MapGridComponent? grid))
        {
            reason = "The Blackfoot support equipment must be deployed on valid ground.";
            return false;
        }

        var halfX = Math.Max(0, footprint.X / 2);
        var halfY = Math.Max(0, footprint.Y / 2);

        for (var x = -halfX; x <= halfX; x++)
        {
            for (var y = -halfY; y <= halfY; y++)
            {
                var tile = new Vector2i(centerTile.Value.GridIndices.X + x, centerTile.Value.GridIndices.Y + y);
                if (!_map.TryGetTileRef(centerTile.Value.GridUid, grid, tile, out var tileRef) ||
                    tileRef.Tile.IsEmpty)
                {
                    reason = $"The Blackfoot support equipment needs floor at deployment offset {x},{y}.";
                    return false;
                }

                if (IsDeploymentTileBlocked(tileRef, grid, SupportBlockMask, ignoredEntity, out var blocker))
                {
                    var blockerName = blocker is { } blockerUid ? Name(blockerUid) : "something";
                    reason = $"The Blackfoot support equipment is blocked by {blockerName} at deployment offset {x},{y}.";
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsDeploymentTileBlocked(
        TileRef tile,
        MapGridComponent grid,
        CollisionGroup mask,
        EntityUid ignoredEntity,
        out EntityUid? blocker,
        float minIntersectionArea = 0.1f)
    {
        blocker = null;

        if (!TryComp(tile.GridUid, out TransformComponent? gridXform))
            return false;

        var xformQuery = GetEntityQuery<TransformComponent>();
        var fixtureQuery = GetEntityQuery<FixturesComponent>();
        var (gridPos, gridRot, matrix) = _transform.GetWorldPositionRotationMatrix(gridXform, xformQuery);

        var size = grid.TileSize;
        var localPos = new Vector2(tile.GridIndices.X * size + size / 2f, tile.GridIndices.Y * size + size / 2f);
        var worldPos = Vector2.Transform(localPos, matrix);

        var tileAabb = Box2.UnitCentered.Scale(0.95f * size);
        var worldBox = new Box2Rotated(tileAabb.Translated(worldPos), gridRot, worldPos);
        tileAabb = tileAabb.Translated(localPos);

        var intersectionArea = 0f;
        foreach (var ent in _lookup.GetEntitiesIntersecting(tile.GridUid, worldBox, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (ent == ignoredEntity ||
                !fixtureQuery.TryGetComponent(ent, out var fixtures) ||
                !xformQuery.TryGetComponent(ent, out var entXform))
            {
                continue;
            }

            var (pos, rot) = _transform.GetWorldPositionRotation(entXform, xformQuery);
            rot -= gridRot;
            pos = (-gridRot).RotateVec(pos - gridPos);

            var xform = new Transform(pos, (float) rot.Theta);
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard ||
                    (fixture.CollisionLayer & (int) mask) == 0)
                {
                    continue;
                }

                for (var i = 0; i < fixture.Shape.ChildCount; i++)
                {
                    var intersection = fixture.Shape.ComputeAABB(xform, i).Intersect(tileAabb);
                    intersectionArea += intersection.Width * intersection.Height;
                    if (intersectionArea <= minIntersectionArea)
                        continue;

                    blocker = ent;
                    return true;
                }
            }
        }

        return false;
    }

    private bool CanPack(Entity<BlackfootPackableSupportComponent> ent, out string reason)
    {
        reason = string.Empty;

        if (TryComp(ent.Owner, out BlackfootLandingPadComponent? pad))
        {
            if (pad.Refueling || pad.Recharging)
            {
                reason = "Stop the Blackfoot service cycle before packing the landing pad.";
                return false;
            }

            if (pad.ParkedAircraft != null ||
                FindParkedAircraft((ent.Owner, pad)) != null)
            {
                reason = "Move the Blackfoot off the landing pad before packing it.";
                return false;
            }

            if (pad.FuelPump is { } pump && Exists(pump))
            {
                reason = "Pack the linked fuel pump before packing the landing pad.";
                return false;
            }
        }

        if (TryComp(ent.Owner, out BlackfootFuelPumpComponent? fuelPump) &&
            fuelPump.LandingPad is { } linkedPad &&
            TryComp(linkedPad, out BlackfootLandingPadComponent? linkedPadComp) &&
            linkedPadComp.Refueling)
        {
            reason = "Stop refueling before packing the fuel pump.";
            return false;
        }

        if (TryComp(ent.Owner, out BlackfootFlightComputerComponent? computer) &&
            computer.LandingPad is { } computerPad &&
            TryComp(computerPad, out BlackfootLandingPadComponent? computerPadComp) &&
            (computerPadComp.Refueling || computerPadComp.Recharging))
        {
            reason = "Stop the Blackfoot service cycle before packing the flight computer.";
            return false;
        }

        return true;
    }

    private EntityUid? FindParkedAircraft(Entity<BlackfootLandingPadComponent> pad)
    {
        var padXform = Transform(pad.Owner);
        if (padXform.MapUid is not { } mapUid ||
            !TryComp(mapUid, out MapGridComponent? grid) ||
            !_map.TryGetTileRef(mapUid, grid, _transform.GetWorldPosition(pad.Owner), out var padTile))
        {
            return null;
        }

        var halfX = Math.Max(0, pad.Comp.Footprint.X / 2);
        var halfY = Math.Max(0, pad.Comp.Footprint.Y / 2);
        var aircraftQuery = EntityQueryEnumerator<BlackfootFlightComponent, TransformComponent>();

        while (aircraftQuery.MoveNext(out var aircraft, out var flight, out var aircraftXform))
        {
            if (aircraftXform.MapUid != mapUid ||
                flight.State is not (BlackfootFlightState.Grounded or BlackfootFlightState.Idling or BlackfootFlightState.Stowed))
            {
                continue;
            }

            if (!_map.TryGetTileRef(mapUid, grid, _transform.GetWorldPosition(aircraft), out var aircraftTile))
                continue;

            var dx = Math.Abs(aircraftTile.GridIndices.X - padTile.GridIndices.X);
            var dy = Math.Abs(aircraftTile.GridIndices.Y - padTile.GridIndices.Y);
            if (dx <= halfX && dy <= halfY)
                return aircraft;
        }

        return null;
    }

    private void ClearSupportLinks(Entity<BlackfootPackableSupportComponent> ent)
    {
        if (TryComp(ent.Owner, out BlackfootLandingPadComponent? pad))
        {
            if (pad.FuelPump is { } linkedPump &&
                TryComp(linkedPump, out BlackfootFuelPumpComponent? linkedPumpComp) &&
                linkedPumpComp.LandingPad == ent.Owner)
            {
                linkedPumpComp.LandingPad = null;
                Dirty(linkedPump, linkedPumpComp);
            }

            var computerQuery = EntityQueryEnumerator<BlackfootFlightComputerComponent>();
            while (computerQuery.MoveNext(out var computerUid, out var linkedComputer))
            {
                if (linkedComputer.LandingPad != ent.Owner)
                    continue;

                linkedComputer.LandingPad = null;
                Dirty(computerUid, linkedComputer);
            }

            var lightQuery = EntityQueryEnumerator<BlackfootLandingPadLightComponent>();
            while (lightQuery.MoveNext(out var lightUid, out var light))
            {
                if (light.LandingPad != ent.Owner)
                    continue;

                light.LandingPad = null;
                light.State = BlackfootLandingPadLightState.Off;
                Dirty(lightUid, light);
            }

            foreach (var light in pad.Lights)
            {
                if (Exists(light))
                    QueueDel(light);
            }

            pad.Lights.Clear();
            pad.ParkedAircraft = null;
            pad.Refueling = false;
            pad.Recharging = false;
            pad.FuelPump = null;
            Dirty(ent.Owner, pad);
        }

        if (TryComp(ent.Owner, out BlackfootFuelPumpComponent? fuelPump))
        {
            if (fuelPump.LandingPad is { } padUid &&
                TryComp(padUid, out BlackfootLandingPadComponent? padComp) &&
                padComp.FuelPump == ent.Owner)
            {
                padComp.FuelPump = null;
                padComp.Refueling = false;
                Dirty(padUid, padComp);
            }

            fuelPump.LandingPad = null;
            Dirty(ent.Owner, fuelPump);
        }

        if (TryComp(ent.Owner, out BlackfootFlightComputerComponent? computer))
        {
            computer.LandingPad = null;
            Dirty(ent.Owner, computer);
        }
    }

    private static string GetPackTool(BlackfootPackableSupportComponent comp)
    {
        return comp.Stage switch
        {
            BlackfootSupportPackStage.Secured => comp.InitialTool,
            BlackfootSupportPackStage.AnchorsLoosened => comp.PanelTool,
            BlackfootSupportPackStage.PanelOpen => comp.FinalTool,
            _ => comp.InitialTool,
        };
    }

    private static string GetPackToolPopup(BlackfootPackableSupportComponent comp)
    {
        return comp.Stage switch
        {
            BlackfootSupportPackStage.Secured => "Use a wrench to loosen the anchor bolts.",
            BlackfootSupportPackStage.AnchorsLoosened => "Use a screwdriver to open the service panel.",
            BlackfootSupportPackStage.PanelOpen => "Use a wrench to fold and pack the support equipment.",
            _ => comp.ToolPopup,
        };
    }
}
