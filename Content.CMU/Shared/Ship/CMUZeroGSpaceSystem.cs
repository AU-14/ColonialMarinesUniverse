using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
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
    ///     Bare space. Open-air tiles also let you float through (see <see cref="IsOpenAt"/>), but grates and glass
    ///     floors are see-through and still something to stand on.
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

        // Out over bare space outside the hull, or anywhere aboard once the ship's gravity is off. Open-air tiles inside
        // the ship (the hangar ceilings) keep gravity while the generator runs.
        if (!IsSpaceAt(_transform.GetMapCoordinates(ent, xform)) &&
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

    /// <summary>
    ///     Xenos are built for climbing and clinging, so in zero-g they claw along at nearly their normal speed and get
    ///     going quicker than people do. They still slide and drift with no friction, like anyone else.
    /// </summary>
    private const float XenoZeroGSpeed = 0.9f / MovementSpeedModifierComponent.DefaultWeightlessModifier;

    private const float XenoZeroGAcceleration = 3f;

    private void OnZeroGMovement(Entity<MobStateComponent> ent, ref CMUZeroGMovementEvent args)
    {
        args.Adrift |= IsOverZeroGSpace(ent);

        if (HasComp<XenoComponent>(ent) && Transform(ent).MapUid is { } xenoMap && IsZeroGMap(xenoMap))
        {
            args.SpeedMultiplier *= XenoZeroGSpeed;
            args.AccelerationMultiplier *= XenoZeroGAcceleration;
        }

        // Pulling yourself along on the floor is slower than pushing off it, the same as crawling with gravity.
        if (HasComp<KnockedDownComponent>(ent) &&
            TryComp(ent, out MovementSpeedModifierComponent? speed) &&
            Transform(ent).MapUid is { } map && IsZeroGMap(map))
        {
            args.SpeedMultiplier *= Math.Min(1f, speed.WalkSpeedModifier);
        }
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
    ///     True if this entity is floating over open space on a zero-g ship: bare space at any time, or an open-air tile
    ///     like the hangar ceilings while the ship's gravity is off. With gravity on, open-air tiles drop you to the deck
    ///     below as usual.
    /// </summary>
    public bool IsOverZeroGSpace(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } map || !IsZeroGMap(map))
            return false;

        var coordinates = _transform.GetMapCoordinates(uid, xform);
        if (IsSpaceAt(coordinates))
            return true;

        return IsOpenAt(coordinates) && _gravity.IsWeightless(uid);
    }

    /// <summary>
    ///     True if this entity is out over bare space on a zero-g ship, outside the hull rather than over an opening
    ///     inside it.
    /// </summary>
    public bool IsOutsideZeroGShip(EntityUid uid)
    {
        var xform = Transform(uid);
        return xform.MapUid is { } map && IsZeroGMap(map) && IsSpaceAt(_transform.GetMapCoordinates(uid, xform));
    }

    /// <summary>
    ///     True if there's no floor at these coordinates, so something floating can pass through to the deck above or
    ///     below: bare space, or an open-air tile like the hangar ceilings.
    /// </summary>
    public bool IsOpenAt(MapCoordinates coordinates)
    {
        return GetTileAt(coordinates) is not { } definition || definition.ID == SpaceTile || definition.CMUFallThrough;
    }

    /// <summary>True if these coordinates are bare space, with no floor or open-air tile at all.</summary>
    public bool IsSpaceAt(MapCoordinates coordinates)
    {
        return GetTileAt(coordinates) is not { } definition || definition.ID == SpaceTile;
    }

    /// <summary>The tile at these coordinates, or null if there's none there.</summary>
    private ContentTileDefinition? GetTileAt(MapCoordinates coordinates)
    {
        if (!_map.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return null;

        var tile = _map.WorldToTile(gridUid, grid, coordinates.Position);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return null;

        return (ContentTileDefinition) _tileDefinitions[tileRef.Tile.TypeId];
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
