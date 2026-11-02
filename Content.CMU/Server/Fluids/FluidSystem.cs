// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Fluids;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server.CMU14.Fluids;

// The fluid pipes, built on the idea of the gas pipes: a run of pipes is a net with a volume, machines join it through
// ports, and what a machine gives out one end comes in at the other. Here every length of pipe holds its own amount of
// a single liquid (any reagent, the one that came into it first) and levels out with the pipes around it a little every tick,
// so that a liquid spreads slowly and a long run of pipe fills up from the end that is fed. Machines take it in and give it
// out through their ports at the rate of the port. Steam is not carried here: it is a gas and goes through the gas pipes.
public sealed class FluidSystem : EntitySystem
{
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // The four sides of a tile, with the bit each one has in the look of a pipe: north 1, east 2, south 4, west 8.
    private static readonly (Vector2i Offset, int Bit)[] Sides = { (new(0, 1), 1), (new(1, 0), 2), (new(0, -1), 4), (new(-1, 0), 8) };

    // How often what machines hold is sent to the clients that look at it.
    private const float DirtyInterval = 0.25f;

    // The longest step a pipe is levelled in: a stalled tick must not move more than a second's worth.
    private const float MaxStep = 0.5f;

    // The farthest a link made with a screwdriver reaches, in tiles, between two pipes or between a pipe and a machine. A link is a long pipe that is not drawn: the farther it
    // reaches the less goes through it (see Conductance), and one that is stretched past this is cut.
    public const float MaxLinkDistance = 12f;

    private EntityQuery<FluidPipeComponent> _pipeQuery;
    private EntityQuery<FluidTankComponent> _tankQuery;

    private bool _pipesChanged = true;
    private float _dirtyTimer;
    private readonly HashSet<EntityUid> _dirtyTanks = new();

    public override void Initialize()
    {
        base.Initialize();

        _pipeQuery = GetEntityQuery<FluidPipeComponent>();
        _tankQuery = GetEntityQuery<FluidTankComponent>();

        SubscribeLocalEvent<FluidPipeComponent, ComponentStartup>(OnPipeStartup);
        SubscribeLocalEvent<FluidPipeComponent, ComponentShutdown>(OnPipeShutdown);
        SubscribeLocalEvent<FluidPipeComponent, AnchorStateChangedEvent>(OnPipeAnchor);
        SubscribeLocalEvent<FluidPipeComponent, ExaminedEvent>(OnPipeExamined);
        SubscribeLocalEvent<FluidTankComponent, ExaminedEvent>(OnTankExamined);
    }

    private void OnPipeStartup(Entity<FluidPipeComponent> pipe, ref ComponentStartup args)
    {
        _pipesChanged = true;
    }

    private void OnPipeShutdown(Entity<FluidPipeComponent> pipe, ref ComponentShutdown args)
    {
        _pipesChanged = true;
    }

    private void OnPipeAnchor(Entity<FluidPipeComponent> pipe, ref AnchorStateChangedEvent args)
    {
        _pipesChanged = true;
    }

    #region The tank of a machine

    public float GetAmount(EntityUid owner, string kind)
    {
        return _tankQuery.TryComp(owner, out var tank) ? tank.Get(kind) : 0f;
    }

    // How much more of the fluid fits: the tank has room and the machine does not already keep as much of it as it wants to.
    public float Room(FluidTankComponent tank, string kind, float? limit = null)
    {
        var room = tank.Capacity - tank.Total;
        if (limit != null)
            room = Math.Min(room, limit.Value - tank.Get(kind));

        return Math.Max(0f, room);
    }

    // Puts fluid into the tank. Returns how much of it went in.
    public float Add(EntityUid owner, string kind, float amount, float? limit = null)
    {
        if (amount <= 0f || !_tankQuery.TryComp(owner, out var tank))
            return 0f;

        var added = Math.Min(amount, Room(tank, kind, limit));
        if (added <= 0f)
            return 0f;

        tank.Contents[kind] = tank.Get(kind) + added;
        _dirtyTanks.Add(owner);
        return added;
    }

    // Takes fluid out of the tank. Returns how much came out.
    public float Remove(EntityUid owner, string kind, float amount)
    {
        if (amount <= 0f || !_tankQuery.TryComp(owner, out var tank))
            return 0f;

        var have = tank.Get(kind);
        var taken = Math.Min(amount, have);
        if (taken <= 0f)
            return 0f;

        if (have - taken <= 0.0001f)
            tank.Contents.Remove(kind);
        else
            tank.Contents[kind] = have - taken;

        _dirtyTanks.Add(owner);
        return taken;
    }

    // Takes the whole amount or nothing: for a machine that needs all of what it uses.
    public bool TryUse(EntityUid owner, string kind, float amount)
    {
        if (GetAmount(owner, kind) + 0.0001f < amount)
            return false;

        Remove(owner, kind, amount);
        return true;
    }

    #endregion

    public void MarkPipesChanged()
    {
        _pipesChanged = true;
    }

    // How much of its flow a link lets through: all of it between neighbours, and the less of it the longer the link, as a long pipe would, so that a liquid does not
    // arrive as fast from the far end of a link as from the next tile.
    public static float Conductance(float distance) => 1f / Math.Max(1f, distance);

    // How far apart two things stand, in tiles; two things on different maps are as far as can be.
    public float DistanceBetween(EntityUid a, EntityUid b)
    {
        var first = _transform.GetMapCoordinates(a);
        var second = _transform.GetMapCoordinates(b);
        return first.MapId == second.MapId ? (first.Position - second.Position).Length() : float.MaxValue;
    }

    // Cuts a pipe off from everything: its links to pipes and machines go, and it is no longer joined to the pipes next to it either. Taking it back joins it again.
    public void SetIsolated(EntityUid uid, bool isolated)
    {
        if (!_pipeQuery.TryComp(uid, out var pipe) || pipe.Isolated == isolated)
            return;

        pipe.Isolated = isolated;
        if (isolated)
            CutLinks(uid, pipe);

        _pipesChanged = true;
    }

    // Every link of a pipe goes: the links it has to pipes, and the ones machines have to it.
    public void CutLinks(EntityUid uid, FluidPipeComponent pipe)
    {
        foreach (var other in pipe.Links)
        {
            if (_pipeQuery.TryComp(other, out var otherPipe))
                otherPipe.Links.Remove(uid);
        }

        pipe.Links.Clear();

        var machines = EntityQueryEnumerator<FluidPortsComponent>();
        while (machines.MoveNext(out _, out var ports))
        {
            foreach (var port in ports.Ports)
                port.Links.Remove(uid);
        }

        _pipesChanged = true;
    }

    // Every pipe a machine is joined to lets go of it.
    public void CutPorts(EntityUid machine)
    {
        if (!TryComp<FluidPortsComponent>(machine, out var ports))
            return;

        foreach (var port in ports.Ports)
            port.Links.Clear();

        _pipesChanged = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pipesChanged)
            RebuildNeighbours();

        var step = Math.Min(frameTime, MaxStep);

        StepPipes(step);
        StepPorts(step);

        _dirtyTimer += frameTime;
        if (_dirtyTimer < DirtyInterval)
            return;

        _dirtyTimer = 0f;
        foreach (var owner in _dirtyTanks)
        {
            if (!TerminatingOrDeleted(owner) && _tankQuery.TryComp(owner, out var tank))
                Dirty(owner, tank);
        }

        _dirtyTanks.Clear();
    }

    // The pipes next to a pipe, and the ones it was linked to, found again after a pipe was built or taken away. The same
    // pass works out how each pipe is to look: an arm on every side where a pipe is next to it, where it was linked to a pipe
    // or a machine on the next tile.
    private void RebuildNeighbours()
    {
        _pipesChanged = false;

        // The pipes joined to a port of a machine on the next tile have an arm towards the machine.
        var toMachine = new Dictionary<EntityUid, int>();
        var machines = EntityQueryEnumerator<FluidPortsComponent, TransformComponent>();
        while (machines.MoveNext(out var machine, out var ports, out var machineXform))
        {
            foreach (var port in ports.Ports)
            {
                foreach (var link in port.Links)
                {
                    if (!_pipeQuery.HasComp(link) || TerminatingOrDeleted(link))
                        continue;

                    var bit = SideOf(link, machine);
                    if (bit != 0)
                        toMachine[link] = toMachine.GetValueOrDefault(link) | bit;
                }
            }
        }

        var query = EntityQueryEnumerator<FluidPipeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pipe, out var xform))
        {
            pipe.Neighbours.Clear();
            var mask = toMachine.GetValueOrDefault(uid);

            if (!pipe.Isolated && xform.Anchored && xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
            {
                var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

                // Pipes on the same tile level each other too.
                var here = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
                while (here.MoveNext(out var other))
                {
                    if (other.Value != uid && _pipeQuery.TryComp(other.Value, out var sameTile) && !sameTile.Isolated)
                        pipe.Neighbours.Add(other.Value);
                }

                foreach (var (offset, bit) in Sides)
                {
                    var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile + offset);
                    while (anchored.MoveNext(out var other))
                    {
                        if (other.Value == uid || !_pipeQuery.TryComp(other.Value, out var next) || next.Isolated)
                            continue;

                        pipe.Neighbours.Add(other.Value);
                        mask |= bit;
                    }
                }
            }

            // The links made with a screwdriver reach as far as they were made to, and are kept only while both ends are there.
            pipe.Links.RemoveAll(other => TerminatingOrDeleted(other) || !_pipeQuery.HasComp(other) || DistanceBetween(uid, other) > MaxLinkDistance);
            foreach (var link in pipe.Links)
            {
                mask |= SideOf(uid, link);
            }

            SetLook(uid, pipe, mask);
        }

        // A pipe that goes down is joined to the pipes in the same place on the level below, and they to it.
        var down = EntityQueryEnumerator<FluidPipeComponent, TransformComponent>();
        while (down.MoveNext(out var uid, out var pipe, out var xform))
        {
            if (!pipe.Down || !xform.Anchored || xform.MapUid is not { } map)
                continue;

            foreach (var below in PipesBelow(uid, map))
            {
                if (!_pipeQuery.TryComp(below, out var lower))
                    continue;

                pipe.Neighbours.Add(below);
                lower.Neighbours.Add(uid);
            }
        }
    }

    // The anchored pipes in the same place on the level under the map.
    private IEnumerable<EntityUid> PipesBelow(EntityUid uid, EntityUid map)
    {
        if (!_zLevels.TryMapDown(new Entity<CMUZLevelMapComponent?>(map, null), out var below))
            yield break;

        // The levels are maps that are grids themselves.
        var gridUid = below.Value.Owner;
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            yield break;

        var tile = _map.WorldToTile(gridUid, grid, _transform.GetWorldPosition(uid));
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (_pipeQuery.HasComp(other.Value))
                yield return other.Value;
        }
    }

    // The bit of the side of the first entity that the second stands on, if it is on the next tile of the same grid.
    private int SideOf(EntityUid from, EntityUid to)
    {
        var a = Transform(from);
        var b = Transform(to);
        if (!a.Anchored || !b.Anchored || a.GridUid == null || a.GridUid != b.GridUid || !TryComp<MapGridComponent>(a.GridUid, out var grid))
            return 0;

        var diff = _map.TileIndicesFor(a.GridUid.Value, grid, b.Coordinates) - _map.TileIndicesFor(a.GridUid.Value, grid, a.Coordinates);
        foreach (var (offset, bit) in Sides)
        {
            if (offset == diff)
                return bit;
        }

        return 0;
    }

    // The look of a pipe goes to the clients as the sides it is joined on.
    private void SetLook(EntityUid uid, FluidPipeComponent pipe, int mask)
    {
        if (pipe.Look == mask)
            return;

        pipe.Look = mask;
        _appearance.SetData(uid, FluidPipeVisuals.Connections, mask);
        _appearance.SetData(uid, FluidPipeVisuals.Down, pipe.Down);
    }

    private void StepPipes(float step)
    {
        var query = EntityQueryEnumerator<FluidPipeComponent>();
        while (query.MoveNext(out var uid, out var pipe))
        {
            foreach (var other in pipe.Neighbours)
            {
                Level(uid, pipe, other, step, 1f);
            }

            foreach (var other in pipe.Links)
            {
                // A link stretched past its reach carries nothing (it is cut when the pipes are next counted).
                if (other.Id <= uid.Id)
                    continue;

                var distance = DistanceBetween(uid, other);
                if (distance > MaxLinkDistance)
                {
                    _pipesChanged = true;
                    continue;
                }

                Level(uid, pipe, other, step, Conductance(distance));
            }
        }
    }

    // Moves liquid between two pipes so that they fill the same share of themselves; each pair is done once, from the lower id.
    // Two pipes with different liquids in them do not mix; an empty one takes whatever the other has.
    private void Level(EntityUid uid, FluidPipeComponent pipe, EntityUid other, float step, float conductance)
    {
        if (other.Id < uid.Id || !_pipeQuery.TryComp(other, out var otherPipe))
            return;

        if (pipe.Kind != null && otherPipe.Kind != null && pipe.Kind != otherPipe.Kind)
            return;

        var kind = pipe.Kind ?? otherPipe.Kind;
        var total = pipe.Capacity + otherPipe.Capacity;
        if (kind == null || total <= 0f)
            return;

        // The amounts the two would have if they were levelled out, and the part of the way there that this step covers.
        var target = (pipe.Amount + otherPipe.Amount) * pipe.Capacity / total;
        var move = (pipe.Amount - target) * Math.Min(1f, pipe.Rate * step * conductance);

        pipe.Amount = Math.Clamp(pipe.Amount - move, 0f, pipe.Capacity);
        otherPipe.Amount = Math.Clamp(otherPipe.Amount + move, 0f, otherPipe.Capacity);
        Settle(pipe, kind);
        Settle(otherPipe, kind);
    }

    // A pipe with something in it carries that liquid; an empty one carries nothing.
    private static void Settle(FluidPipeComponent pipe, string kind)
    {
        if (pipe.Amount < 0.001f)
        {
            pipe.Amount = 0f;
            pipe.Kind = null;
        }
        else
        {
            pipe.Kind = kind;
        }
    }

    // The ports: an input takes liquid out of its pipes into the machine, an output gives the machine's liquid to its pipes.
    private void StepPorts(float step)
    {
        var query = EntityQueryEnumerator<FluidPortsComponent, FluidTankComponent>();
        while (query.MoveNext(out var uid, out var ports, out var tank))
        {
            foreach (var port in ports.Ports)
            {
                if (port.Links.Count == 0)
                    continue;

                port.Links.RemoveAll(link => TerminatingOrDeleted(link) || !_pipeQuery.HasComp(link) || DistanceBetween(uid, link) > MaxLinkDistance);

                foreach (var link in port.Links)
                {
                    if (!_pipeQuery.TryComp(link, out var pipe) || pipe.Isolated)
                        continue;

                    // A pipe far from the machine gives and takes less, the way the link between two pipes does.
                    var rate = port.Rate * port.Scale * step * Conductance(DistanceBetween(uid, link));
                    if (rate <= 0f)
                        continue;

                    if (port.Output)
                    {
                        // A port for a certain liquid gives that one; one for any gives what the pipe already has, or else
                        // whatever the machine has most of.
                        var kind = port.Kind ?? (pipe.Kind != null && tank.Get(pipe.Kind) > 0f ? pipe.Kind : Largest(tank));
                        if (kind == null || (pipe.Kind != null && pipe.Kind != kind))
                            continue;

                        var moved = Math.Min(Math.Min(rate, tank.Get(kind)), pipe.Capacity - pipe.Amount);
                        if (moved <= 0f)
                            continue;

                        Remove(uid, kind, moved);
                        pipe.Amount += moved;
                        pipe.Kind = kind;
                    }
                    else
                    {
                        var kind = port.Kind ?? pipe.Kind;
                        if (kind == null || pipe.Kind != kind)
                            continue;

                        var moved = Math.Min(Math.Min(rate, pipe.Amount), Room(tank, kind, port.Limit));
                        if (moved <= 0f)
                            continue;

                        pipe.Amount -= moved;
                        Add(uid, kind, moved, port.Limit);
                        Settle(pipe, kind);
                    }
                }
            }
        }
    }

    // The liquid a tank has most of.
    private static string? Largest(FluidTankComponent tank)
    {
        string? best = null;
        var most = 0f;
        foreach (var (kind, amount) in tank.Contents)
        {
            if (amount > most)
            {
                best = kind;
                most = amount;
            }
        }

        return best;
    }

    // The name of a liquid as the player reads it.
    public string Name(string kind)
    {
        return _reagents.TryIndex(kind, out var reagent) ? reagent.LocalizedName : kind;
    }

    private void OnPipeExamined(Entity<FluidPipeComponent> pipe, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (pipe.Comp.Kind == null)
        {
            args.PushMarkup(Loc.GetString("au14-fluid-pipe-empty"));
            return;
        }

        var percent = pipe.Comp.Capacity <= 0f ? 0 : (int) Math.Round(pipe.Comp.Amount / pipe.Comp.Capacity * 100f);
        args.PushMarkup(Loc.GetString("au14-fluid-pipe-examine", ("fluid", Name(pipe.Comp.Kind)), ("percent", percent)));
    }

    private void OnTankExamined(Entity<FluidTankComponent> tank, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (tank.Comp.Contents.Count == 0)
        {
            args.PushMarkup(Loc.GetString("au14-fluid-tank-empty"));
            return;
        }

        foreach (var (kind, amount) in tank.Comp.Contents)
        {
            args.PushMarkup(Loc.GetString("au14-fluid-tank-examine", ("fluid", Name(kind)), ("amount", (int) amount), ("capacity", (int) tank.Comp.Capacity)));
        }
    }
}
