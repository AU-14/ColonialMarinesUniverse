using Content.Client.IconSmoothing;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.CMU14.Xenonids.Weeds;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.Xenonids.Weeds;

public sealed partial class CMUWeededJunctionSpriteSystem : EntitySystem
{
    [Dependency] private IconSmoothSystem _iconSmooth = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private EntityQuery<CMUWeededJunctionSpriteComponent> _overlayQuery;
    private EntityQuery<XenoWeedableComponent> _weedableQuery;

    // done on frame update so the window under us has its own smoothing in place first
    private readonly HashSet<EntityUid> _dirty = new();

    public override void Initialize()
    {
        _overlayQuery = GetEntityQuery<CMUWeededJunctionSpriteComponent>();
        _weedableQuery = GetEntityQuery<XenoWeedableComponent>();

        SubscribeLocalEvent<CMUWeededJunctionSpriteComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<IconSmoothComponent, IconSmoothingUpdatedEvent>(OnHostSmoothed);
    }

    private void OnStartup(Entity<CMUWeededJunctionSpriteComponent> ent, ref ComponentStartup args)
    {
        _dirty.Add(ent);
    }

    // a neighbour window broke or got built, the window re-smooths and the weeds on it follow
    private void OnHostSmoothed(Entity<IconSmoothComponent> ent, ref IconSmoothingUpdatedEvent args)
    {
        if (!_weedableQuery.HasComp(ent) || !TryGetTile(ent, out var grid, out var tile))
            return;

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (_overlayQuery.HasComp(other))
                _dirty.Add(other.Value);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_dirty.Count == 0)
            return;

        foreach (var uid in _dirty)
        {
            if (_overlayQuery.TryComp(uid, out var overlay) && TryComp(uid, out SpriteComponent? sprite))
                UpdateSprite((uid, overlay, sprite));
        }

        _dirty.Clear();
    }

    private void UpdateSprite(Entity<CMUWeededJunctionSpriteComponent, SpriteComponent> ent)
    {
        if (!TryGetTile(ent, out var grid, out var tile))
            return;

        var junction = 0;
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (other.Value == ent.Owner || !_weedableQuery.HasComp(other) || !HasComp<IconSmoothComponent>(other))
                continue;

            junction = _iconSmooth.CMUGetCardinalJunction(other.Value);
            break;
        }

        var state = PickState(ent.Comp1.States, junction);
        _sprite.LayerSetRsiState((ent.Owner, ent.Comp2), 0, $"{ent.Comp1.Base}{state}");
    }

    // corners and tees have no cmss13 sprite, keep whichever straight run is complete (horizontal on a tie)
    public static int PickState(List<int> states, int junction)
    {
        if (states.Contains(junction))
            return junction;

        var horizontal = junction & 12;
        var vertical = junction & 3;
        var pick = horizontal == 12 || vertical != 3 && horizontal != 0 ? horizontal : vertical;
        return states.Contains(pick) ? pick : 0;
    }

    private bool TryGetTile(EntityUid uid, out Entity<MapGridComponent> grid, out Vector2i tile)
    {
        grid = default;
        tile = default;
        var xform = Transform(uid);
        if (!xform.Anchored || !TryComp(xform.GridUid, out MapGridComponent? gridComp))
            return false;

        grid = (xform.GridUid.Value, gridComp);
        tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        return true;
    }
}
