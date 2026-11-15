using System.Numerics;
using Content.Shared.CMU14.Yautja.Lodge;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaWallTorchFrameSystem : EntitySystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private TransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaWallTorchFrameComponent, AfterInteractEvent>(OnAfterInteract);
    }

    // cmss13 drops the torch on the wall turf itself. here it goes on the floor tile in front of the wall,
    // pushed up against it, since a light sitting inside the wall's occluder doesn't light anything
    private void OnAfterInteract(Entity<YautjaWallTorchFrameComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } wall ||
            !_tag.HasTag(wall, WallTag))
        {
            return;
        }

        var wallXform = Transform(wall);
        if (!wallXform.Anchored ||
            wallXform.GridUid is not { } gridUid ||
            !TryComp(gridUid, out MapGridComponent? grid))
        {
            return;
        }

        var wallPos = _transform.WithEntityId(wallXform.Coordinates, gridUid).Position;
        var userPos = _transform.WithEntityId(Transform(args.User).Coordinates, gridUid).Position;
        var delta = userPos - wallPos;
        if (delta.LengthSquared() < 0.01f)
            return;

        var dir = MathF.Abs(delta.X) >= MathF.Abs(delta.Y)
            ? new Vector2(MathF.Sign(delta.X), 0)
            : new Vector2(0, MathF.Sign(delta.Y));

        args.Handled = true;

        var wallTile = _map.TileIndicesFor(gridUid, grid, wallXform.Coordinates);
        var frontTile = wallTile + new Vector2i((int) dir.X, (int) dir.Y);
        foreach (var anchored in _map.GetAnchoredEntities((gridUid, grid), frontTile))
        {
            if (!_tag.HasTag(anchored, WallTag))
                continue;

            _popup.PopupEntity(Loc.GetString("cmu-yautja-wall-torch-no-room"), wall, args.User);
            return;
        }

        var coords = _map.GridTileToLocal(gridUid, grid, wallTile).Offset(dir * ent.Comp.Offset);
        Spawn(ent.Comp.Mounted, coords);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-wall-torch-placed"), wall, args.User);
        QueueDel(ent);
    }
}
