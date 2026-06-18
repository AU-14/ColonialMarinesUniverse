using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUDeployableZLevelLadderSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private const int UpperOffset = 1;

    private readonly HashSet<Entity<CMUZLevelLadderComponent>> _ladders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUDeployableZLevelLadderComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUDeployableZLevelLadderComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CMUPackableZLevelLadderComponent, GetVerbsEvent<AlternativeVerb>>(OnPackableGetAltVerbs);
    }

    private void OnAfterInteract(Entity<CMUDeployableZLevelLadderComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        args.Handled = true;
        TryDeploy(ent, args.User, args.ClickLocation);
    }

    private void OnUseInHand(Entity<CMUDeployableZLevelLadderComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryDeploy(ent, args.User, _transform.GetMoverCoordinates(args.User));
    }

    private void TryDeploy(
        Entity<CMUDeployableZLevelLadderComponent> ent,
        EntityUid user,
        EntityCoordinates location)
    {
        var snapped = _transform.GetMoverCoordinates(location).SnapToGrid();
        var lowerMapCoordinates = _transform.ToMapCoordinates(snapped);

        if (!TryGetTileCenter(lowerMapCoordinates, out var lowerCoordinates, out var lowerMap) ||
            HasLadderAt(_transform.ToMapCoordinates(lowerCoordinates), ent.Comp.ExistingLadderRadius))
        {
            Popup(user, "cmu-deployable-z-ladder-blocked");
            return;
        }

        var lowerCenterMapCoordinates = _transform.ToMapCoordinates(lowerCoordinates);
        if (_zLevels.HasOpaqueAbove((lowerMap, null), lowerCenterMapCoordinates.Position))
        {
            Popup(user, "cmu-deployable-z-ladder-blocked");
            return;
        }

        if (!_zLevels.TryProjectToZMap(
                (lowerMap, null),
                UpperOffset,
                lowerCenterMapCoordinates.Position,
                out var upperMapCoordinates,
                out _))
        {
            Popup(user, "cmu-deployable-z-ladder-no-level");
            return;
        }

        if (!TryGetTileCenter(upperMapCoordinates, out var upperCoordinates, out _, allowSpace: true) ||
            HasLadderAt(_transform.ToMapCoordinates(upperCoordinates), ent.Comp.ExistingLadderRadius))
        {
            Popup(user, "cmu-deployable-z-ladder-blocked");
            return;
        }

        var lower = Spawn(ent.Comp.LowerPrototype, lowerCoordinates);
        var upper = Spawn(ent.Comp.UpperPrototype, upperCoordinates);
        MakePackable(lower, upper, ent.Comp.PackedPrototype);

        _hands.TryDrop(user, ent.Owner);
        QueueDel(ent);
        Popup(user, "cmu-deployable-z-ladder-success", PopupType.Small);
    }

    private void OnPackableGetAltVerbs(Entity<CMUPackableZLevelLadderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-deployable-z-ladder-pack-verb"),
            Act = () => TryPack(ent, user),
            Priority = 1,
        });
    }

    public bool TryPack(Entity<CMUPackableZLevelLadderComponent> ent, EntityUid user)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return false;

        var packed = Spawn(ent.Comp.PackedPrototype, Transform(ent.Owner).Coordinates);
        _hands.TryPickupAnyHand(user, packed);

        if (ent.Comp.Partner is { } partner && Exists(partner))
            QueueDel(partner);

        QueueDel(ent.Owner);
        Popup(user, "cmu-deployable-z-ladder-pack-success", PopupType.Small);
        return true;
    }

    private void MakePackable(EntityUid lower, EntityUid upper, EntProtoId packedPrototype)
    {
        var lowerPackable = EnsureComp<CMUPackableZLevelLadderComponent>(lower);
        lowerPackable.PackedPrototype = packedPrototype;
        lowerPackable.Partner = upper;

        var upperPackable = EnsureComp<CMUPackableZLevelLadderComponent>(upper);
        upperPackable.PackedPrototype = packedPrototype;
        upperPackable.Partner = lower;
    }

    private bool TryGetTileCenter(
        MapCoordinates coordinates,
        out EntityCoordinates tileCenter,
        out EntityUid mapUid,
        bool allowSpace = false)
    {
        tileCenter = default;
        mapUid = default;

        if (!_map.TryGetMap(coordinates.MapId, out var map) ||
            map is not { } resolvedMap ||
            !_mapManager.TryFindGridAt(coordinates, out var gridUid, out var grid))
        {
            return false;
        }

        var tile = _map.WorldToTile(gridUid, grid, coordinates.Position);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) ||
            tileRef.Tile.IsEmpty ||
            (!allowSpace && _turf.IsSpace(tileRef)))
        {
            return false;
        }

        tileCenter = _map.GridTileToLocal(gridUid, grid, tile);
        mapUid = resolvedMap;
        return true;
    }

    private bool HasLadderAt(MapCoordinates coordinates, float radius)
    {
        _ladders.Clear();
        _lookup.GetEntitiesInRange(
            coordinates.MapId,
            coordinates.Position,
            radius,
            _ladders,
            LookupFlags.Static | LookupFlags.StaticSundries);

        return _ladders.Count > 0;
    }

    private void Popup(EntityUid user, string message, PopupType type = PopupType.SmallCaution)
    {
        _popup.PopupClient(Loc.GetString(message), user, user, type);
    }
}
