using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Ship;

/// <summary>
///     Makes the space tiles around a <see cref="CMUZeroGSpaceComponent"/> ship zero-g, and works out who is floating
///     there (or anywhere aboard once the ship's gravity is off) so z-level falling can leave them be.
/// </summary>
public sealed partial class CMUZeroGSpaceSystem : EntitySystem
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(0.25);

    /// <summary>
    ///     Bare space counts, and so do open-air tiles (see <see cref="IsOpenAt"/>). Grates and glass floors are
    ///     see-through but still something to stand on.
    /// </summary>
    private const string SpaceTile = "Space";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private readonly Dictionary<EntityUid, bool> _zeroGMaps = new();
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        // Humans and xenos ignore gravity everywhere (RMC planets have none), so this has to run after that and win.
        SubscribeLocalEvent<MobStateComponent, IsWeightlessEvent>(OnIsWeightless,
            after: [typeof(InventorySystem), typeof(MovementIgnoreGravitySystem)]);
        SubscribeLocalEvent<MobStateComponent, CMUZeroGMovementEvent>(OnZeroGMovement);
        SubscribeLocalEvent<CMUXenoProofComponent, BeforeDamageChangedEvent>(OnXenoProofDamage);
    }

    public override void Update(float frameTime)
    {
        // Gravity only re-checks weightlessness on grid changes, so floating on or off a space tile needs a nudge.
        var now = _timing.CurTime;
        if (now < _nextRefresh)
            return;

        _nextRefresh = now + RefreshInterval;
        _zeroGMaps.Clear();

        var query = EntityQueryEnumerator<MobStateComponent, GravityAffectedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var gravity, out var xform))
        {
            if (xform.MapUid is { } map && IsZeroGMap(map))
                _gravity.RefreshWeightless((uid, gravity));
        }
    }

    private void OnIsWeightless(Entity<MobStateComponent> ent, ref IsWeightlessEvent args)
    {
        var xform = Transform(ent);
        if (xform.MapUid is not { } map || !IsZeroGMap(map))
            return;

        // Out over open space, or anywhere aboard once the ship's gravity is off.
        if (!IsOpenAt(_transform.GetMapCoordinates(ent, xform)) &&
            _gravity.EntityGridOrMapHaveGravity((ent, xform)))
        {
            return;
        }

        // Magboots and the like still keep you down. Ask them directly, since the blanket gravity immunity above has
        // already overwritten whatever they answered.
        if (TryComp(ent, out InventoryComponent? inventory))
        {
            var gear = new IsWeightlessEvent();
            _inventory.RelayEvent((ent.Owner, inventory), ref gear);
            if (gear.Handled && !gear.IsWeightless)
                return;
        }

        args.IsWeightless = true;
        args.Handled = true;
    }

    private void OnZeroGMovement(Entity<MobStateComponent> ent, ref CMUZeroGMovementEvent args)
    {
        args.Adrift |= IsOverZeroGSpace(ent);

        // Xenos are built for scrambling over any surface; zero-g doesn't slow them down.
        args.KeepSpeed |= HasComp<XenoComponent>(ent);
    }

    private void OnXenoProofDamage(Entity<CMUXenoProofComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin is { } origin && HasComp<XenoComponent>(origin))
            args.Cancelled = true;
    }

    /// <summary>
    ///     True if this entity is weightless aboard a zero-g ship, so it floats instead of falling between decks.
    /// </summary>
    public bool IsFloating(EntityUid uid)
    {
        return Transform(uid).MapUid is { } map && IsZeroGMap(map) && _gravity.IsWeightless(uid);
    }

    /// <summary>
    ///     True if the tile under this entity is open space on a zero-g ship.
    /// </summary>
    public bool IsOverZeroGSpace(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } map || !IsZeroGMap(map))
            return false;

        return IsOpenAt(_transform.GetMapCoordinates(uid, xform));
    }

    /// <summary>
    ///     True if there's no floor at these coordinates, so something can float through to the deck above or below:
    ///     bare space, or an open-air tile like the hangar ceilings. Aboard a zero-g ship both count as open space.
    /// </summary>
    public bool IsOpenAt(MapCoordinates coordinates)
    {
        if (!_map.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return true;

        var tile = _map.WorldToTile(gridUid, grid, coordinates.Position);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return true;

        var definition = (ContentTileDefinition) _tileDefinitions[tileRef.Tile.TypeId];
        return definition.ID == SpaceTile || definition.CMUFallThrough;
    }

    /// <summary>True if this entity is anywhere aboard a zero-g ship, on any of its decks.</summary>
    public bool IsOnZeroGMap(EntityUid uid)
    {
        return Transform(uid).MapUid is { } map && IsZeroGMap(map);
    }

    public bool IsZeroGMap(EntityUid map)
    {
        // Every deck gets the marker once it's been looked up on the server, so the client can tell from its own map
        // without needing the rest of the network.
        if (HasComp<CMUZeroGSpaceComponent>(map))
            return true;

        if (_zeroGMaps.TryGetValue(map, out var cached))
            return cached;

        var zeroG = false;
        foreach (var member in _zLevels.GetAllNetworkMaps(map))
        {
            if (HasComp<CMUZeroGSpaceComponent>(member))
            {
                zeroG = true;
                break;
            }
        }

        _zeroGMaps[map] = zeroG;
        if (zeroG && _net.IsServer)
            EnsureComp<CMUZeroGSpaceComponent>(map);

        return zeroG;
    }
}
