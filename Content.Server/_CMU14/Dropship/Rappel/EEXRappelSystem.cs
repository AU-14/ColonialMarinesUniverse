using System.Numerics;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.Dropship.Rappel;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Utility.Systems;
using Content.Shared._RMC14.Ladder;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CMU14.Dropship.Rappel;

public sealed partial class EEXRappelSystem : SharedEEXRappelSystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedRMCEquipmentDeployerSystem _equipmentDeployer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EEXRappelSystemComponent, RMCEquipmentDeployAttemptEvent>(OnDeployAttempt);
        SubscribeLocalEvent<EEXRappelSystemComponent, RMCEquipmentDeployedEvent>(OnEquipmentDeployed);
        SubscribeLocalEvent<EEXRappelSystemComponent, ComponentShutdown>(OnRappelShutdown);
        SubscribeLocalEvent<DropshipTacticalHoverEndedEvent>(OnTacticalHoverEnded);
    }

    private void OnDeployAttempt(Entity<EEXRappelSystemComponent> ent, ref RMCEquipmentDeployAttemptEvent args)
    {
        if (!args.Deploy)
            return;

        string failure;
        if (!Dropship.TryGetGridDropship(ent, out var dropship) ||
            !HasComp<DropshipTacticalHoverComponent>(dropship))
        {
            failure = "cmu-eex-rappel-tactical-hover-only";
        }
        else if (TryGetRappelCoordinates(ent, args.DeployOffset, out _, out _, out failure))
        {
            return;
        }

        args.Cancel();
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString(failure), ent, user, PopupType.SmallCaution);
    }

    private void OnEquipmentDeployed(Entity<EEXRappelSystemComponent> ent, ref RMCEquipmentDeployedEvent args)
    {
        if (!args.Deployed)
        {
            DeleteGroundEndpoint(ent);
            ent.Comp.Dropship = null;
            return;
        }

        if (Transform(args.Equipment).MapUid is not { } dropshipMap ||
            !_zLevels.TryProjectToZMap(
                (dropshipMap, null),
                -1,
                _transform.GetWorldPosition(args.Equipment),
                out var groundCoordinates,
                out _))
        {
            // The placement changed between validation and deployment. Put the line back
            // so the deployer and console cannot remain in a false deployed state.
            _equipmentDeployer.TryDeploy(ent, false);
            return;
        }

        DeleteGroundEndpoint(ent);
        ent.Comp.GroundEndpoint = Spawn(ent.Comp.GroundEndpointPrototype, groundCoordinates);

        if (Dropship.TryGetGridDropship(ent, out var dropship))
            ent.Comp.Dropship = dropship;
    }

    private void OnTacticalHoverEnded(ref DropshipTacticalHoverEndedEvent args)
    {
        var query = EntityQueryEnumerator<EEXRappelSystemComponent, RMCEquipmentDeployerComponent>();
        while (query.MoveNext(out var uid, out var rappel, out var deployer))
        {
            if (rappel.Dropship != args.Dropship || !deployer.IsDeployed)
                continue;

            _equipmentDeployer.TryDeploy(uid, false, equipmentDeployerComponent: deployer);
        }
    }

    private void OnRappelShutdown(Entity<EEXRappelSystemComponent> ent, ref ComponentShutdown args)
    {
        DeleteGroundEndpoint(ent);
    }

    private void DeleteGroundEndpoint(Entity<EEXRappelSystemComponent> ent)
    {
        if (ent.Comp.GroundEndpoint is { } ground && !TerminatingOrDeleted(ground))
            QueueDel(ground);

        ent.Comp.GroundEndpoint = null;
    }

    private bool TryGetRappelCoordinates(
        Entity<EEXRappelSystemComponent> ent,
        Vector2 deployOffset,
        out EntityCoordinates dropshipCoordinates,
        out MapCoordinates groundCoordinates,
        out string failure)
    {
        dropshipCoordinates = Transform(ent).Coordinates.Offset(deployOffset);
        groundCoordinates = default;

        if (!Dropship.TryGetGridDropship(ent, out var dropship) ||
            !TryComp(dropship, out MapGridComponent? dropshipGrid))
        {
            failure = "cmu-eex-rappel-no-opening";
            return false;
        }

        var dropshipMapCoordinates = _transform.ToMapCoordinates(dropshipCoordinates);
        // The rappel line deploys through the dropship's floor. That floor does not
        // need to be an empty/transparent z-level opening; only the landing tile
        // below must exist and be clear of impassable structures.
        var dropshipTile = _map.WorldToTile(dropship, dropshipGrid, dropshipMapCoordinates.Position);
        if (HasLadderAt((dropship.Owner, dropshipGrid), dropshipTile))
        {
            failure = "cmu-eex-rappel-blocked";
            return false;
        }

        if (Transform(dropship).MapUid is not { } dropshipMap ||
            !_zLevels.TryProjectToZMap(
                (dropshipMap, null),
                -1,
                dropshipMapCoordinates.Position,
                out groundCoordinates,
                out var groundMap) ||
            !TryComp(groundMap.Value.Owner, out MapGridComponent? groundGrid))
        {
            failure = "cmu-eex-rappel-no-ground";
            return false;
        }

        var groundTile = _map.WorldToTile(groundMap.Value.Owner, groundGrid, groundCoordinates.Position);
        if (!_map.TryGetTileRef(groundMap.Value.Owner, groundGrid, groundTile, out var groundTileRef) ||
            groundTileRef.Tile.IsEmpty)
        {
            failure = "cmu-eex-rappel-no-ground";
            return false;
        }

        const CollisionGroup blockMask = CollisionGroup.Impassable |
                                         CollisionGroup.MidImpassable |
                                         CollisionGroup.HighImpassable;
        if (_turf.IsTileBlocked(groundTileRef, blockMask))
        {
            failure = "cmu-eex-rappel-ground-blocked";
            return false;
        }

        if (HasLadderAt((groundMap.Value.Owner, groundGrid), groundTile))
        {
            failure = "cmu-eex-rappel-blocked";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private bool HasLadderAt(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid.Owner, grid.Comp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<CMUZLevelLadderComponent>(uid) || HasComp<LadderComponent>(uid))
                return true;
        }

        return false;
    }
}
