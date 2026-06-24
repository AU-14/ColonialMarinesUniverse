using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Ladder;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUDeployableZLevelLadderSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUDeployableZLevelLadderComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CMUDeployedZLevelLadderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnUseInHand(Entity<CMUDeployableZLevelLadderComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryDeploy(ent, args.User);
    }

    private void OnGetAlternativeVerbs(Entity<CMUDeployedZLevelLadderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-zlevel-ladder-retract"),
            Act = () => TryRetract(ent, user),
            Priority = 90,
        });
    }

    private bool TryDeploy(Entity<CMUDeployableZLevelLadderComponent> ent, EntityUid user)
    {
        if (!TryGetDeployment(ent, user, out var deployment, out var popup))
        {
            if (popup != null)
                _popup.PopupEntity(popup, ent, user, PopupType.SmallCaution);

            return false;
        }

        var lower = SpawnAtPosition(ent.Comp.UpLadderPrototype, deployment.LowerCoordinates);
        var upper = SpawnAtPosition(ent.Comp.DownLadderPrototype, deployment.UpperCoordinates);

        PrepareSpawnedLadder(lower, deployment.LowerCoordinates);
        PrepareSpawnedLadder(upper, deployment.UpperCoordinates);

        var packed = ent.Comp.PackedPrototype ?? MetaData(ent).EntityPrototype?.ID ?? "CMUDeployableZLevelLadder";
        SetDeployedLadderData(lower, upper, packed);
        SetDeployedLadderData(upper, lower, packed);

        _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-deploy-finish", ("ladder", ent)), user, user);
        QueueDel(ent);
        return true;
    }

    private bool TryRetract(Entity<CMUDeployedZLevelLadderComponent> ent, EntityUid user)
    {
        if (!_hands.TryGetEmptyHand(user, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-retract-no-hand"), ent, user, PopupType.SmallCaution);
            return false;
        }

        var packed = SpawnAtPosition(ent.Comp.PackedPrototype, _transform.GetMoverCoordinates(user));
        if (!_hands.TryPickupAnyHand(user, packed))
        {
            QueueDel(packed);
            _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-retract-no-hand"), ent, user, PopupType.SmallCaution);
            return false;
        }

        if (ent.Comp.OtherLadder is { } other &&
            other != ent.Owner &&
            Exists(other))
        {
            QueueDel(other);
        }

        QueueDel(ent);
        _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-retract-finish", ("ladder", packed)), user, user);
        return true;
    }

    private bool TryGetDeployment(
        Entity<CMUDeployableZLevelLadderComponent> ent,
        EntityUid user,
        out LadderDeployment deployment,
        out string? popup)
    {
        deployment = default;
        popup = null;

        var userXform = Transform(user);
        if (userXform.MapUid is not { } map ||
            !TryComp<CMUZLevelMapComponent>(map, out var zMap) ||
            !TryComp<MapGridComponent>(map, out var grid))
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-no-level");
            return false;
        }

        Entity<CMUZLevelMapComponent?> currentMap = (map, zMap);
        if (!_zLevels.TryMapUp(currentMap, out var upperMap) ||
            !TryComp<MapGridComponent>(upperMap.Value, out var upperGrid))
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-no-level");
            return false;
        }

        var worldPosition = _transform.GetWorldPosition(user);
        var tile = _map.WorldToTile(map, grid, worldPosition);
        if (!_map.TryGetTileRef(map, grid, tile, out var lowerTile) ||
            lowerTile.Tile.IsEmpty)
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-no-floor");
            return false;
        }

        if (HasRoofAbove(currentMap, grid, upperMap.Value, upperGrid, tile))
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-roof");
            return false;
        }

        if (HasLadderAt(map, grid, tile) ||
            HasLadderAt(upperMap.Value, upperGrid, tile))
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-blocked");
            return false;
        }

        deployment = new LadderDeployment(
            _map.GridTileToLocal(map, grid, tile),
            _map.GridTileToLocal(upperMap.Value, upperGrid, tile));

        return true;
    }

    private bool HasRoofAbove(
        Entity<CMUZLevelMapComponent?> currentMap,
        MapGridComponent currentGrid,
        Entity<CMUZLevelMapComponent> upperMap,
        MapGridComponent upperGrid,
        Vector2i tile)
    {
        if (TryComp<RoofComponent>(currentMap, out var roof) &&
            _roof.IsRooved((currentMap.Owner, currentGrid, roof), tile))
        {
            return true;
        }

        return !CMUZLevelOpeningCache.IsOpeningTile((upperMap.Owner, upperGrid), tile, _map, _tile);
    }

    private bool HasLadderAt(EntityUid map, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(map, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<CMUZLevelLadderComponent>(uid) ||
                HasComp<LadderComponent>(uid))
            {
                return true;
            }
        }

        return false;
    }

    private void PrepareSpawnedLadder(EntityUid ladder, EntityCoordinates coordinates)
    {
        _transform.SetCoordinates(ladder, coordinates);
        _transform.SetLocalRotation(ladder, Angle.Zero);

        var xform = Transform(ladder);
        if (!xform.Anchored)
            _transform.AnchorEntity((ladder, xform));
    }

    private void SetDeployedLadderData(EntityUid ladder, EntityUid otherLadder, EntProtoId packed)
    {
        var deployed = EnsureComp<CMUDeployedZLevelLadderComponent>(ladder);
        deployed.OtherLadder = otherLadder;
        deployed.PackedPrototype = packed;
    }

    private readonly record struct LadderDeployment(
        EntityCoordinates LowerCoordinates,
        EntityCoordinates UpperCoordinates);
}
