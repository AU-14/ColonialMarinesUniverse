// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Farming;

// The tool that makes the seedbeds: clicked on the ground (grass, earth, sand) it makes a seedbed there, an entity with the soil of the botany in it, and clicked on a
// seedbed it takes it away.
[RegisterComponent, Access(typeof(RakeSystem))]
public sealed partial class RakeComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1.5f);

    [DataField]
    public SoundSpecifier? Sound;

    // What it makes.
    [DataField]
    public EntProtoId Seedbed = "RUMCSeedbed";

    // A tile counts as ground for the seedbed when its name has one of these words in it, so that the grass, the earth and the sand of the maps of the game do too.
    [DataField]
    public List<string> TileKeywords = new() { "Grass", "Dirt", "Sand", "Mud", "Jungle" };
}

// A seedbed: the soil of the botany that the rake has made on the ground.
[RegisterComponent]
public sealed partial class SeedbedComponent : Component;

// The do-after of raking the ground; carries the place and the target.
[Serializable, NetSerializable]
public sealed partial class RakeDoAfter : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Coordinates;

    // The seedbed that is taken away; none when one is made.
    [DataField]
    public NetEntity? Target;

    public RakeDoAfter(NetCoordinates coordinates, NetEntity? target)
    {
        Coordinates = coordinates;
        Target = target;
    }

    public override DoAfterEvent Clone() => this;
}

// The rake: works the ground with a do-after to make a bed, where it can.
public sealed class RakeSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RakeComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<RakeComponent, RakeDoAfter>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<RakeComponent> rake, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        EntityUid? target = null;
        if (args.Target is { } clicked)
        {
            // A seedbed is taken away; anything else that is clicked is none of the rake's business.
            if (!HasComp<SeedbedComponent>(clicked))
                return;

            target = clicked;
        }
        else if (!CanMakeAt(rake, args.ClickLocation))
        {
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, rake.Comp.Delay, new RakeDoAfter(GetNetCoordinates(args.ClickLocation), target == null ? null : GetNetEntity(target.Value)), rake)
        {
            BreakOnDamage = true,
            BlockDuplicate = false,
            CancelDuplicate = false,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnDoAfter(Entity<RakeComponent> rake, ref RakeDoAfter args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var location = GetCoordinates(args.Coordinates);
        args.Handled = true;

        if (args.Target is { } netTarget)
        {
            if (!TryGetEntity(netTarget, out var target) || TerminatingOrDeleted(target) || !HasComp<SeedbedComponent>(target))
                return;

            _audio.PlayPredicted(rake.Comp.Sound, location, args.User);
            if (_net.IsServer)
                QueueDel(target);

            return;
        }

        if (!CanMakeAt(rake, location, out var grid, out var gridComp, out var tile) || grid is not { } gridUid)
            return;

        _audio.PlayPredicted(rake.Comp.Sound, location, args.User);
        if (_net.IsServer)
            Spawn(rake.Comp.Seedbed, _map.GridTileToLocal(gridUid, gridComp, tile));
    }

    private bool CanMakeAt(Entity<RakeComponent> rake, EntityCoordinates location)
    {
        return CanMakeAt(rake, location, out _, out _, out _);
    }

    // The ground must be of the right kind and have no seedbed on it yet.
    private bool CanMakeAt(Entity<RakeComponent> rake, EntityCoordinates location, out EntityUid? grid, out MapGridComponent gridComp, out Vector2i tile)
    {
        gridComp = default!;
        tile = default;

        // The ground is a grid of its own, or the level itself (the levels are maps that are grids).
        grid = _transform.GetGrid(location) ?? _transform.GetMap(location);
        if (grid == null || !TryComp(grid, out MapGridComponent? found))
            return false;

        gridComp = found;
        var tileRef = _map.GetTileRef(grid.Value, gridComp, location);
        tile = tileRef.GridIndices;

        var id = _turf.GetContentTileDefinition(tileRef).ID;
        var fits = false;
        foreach (var word in rake.Comp.TileKeywords)
        {
            if (id.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                fits = true;
                break;
            }
        }

        if (!fits)
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid.Value, gridComp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<SeedbedComponent>(uid.Value))
                return false;
        }

        return true;
    }
}
