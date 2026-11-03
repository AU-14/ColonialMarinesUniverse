// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Fluids;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.Verbs;
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
public sealed partial class FluidSystem : EntitySystem
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
        SubscribeLocalEvent<FluidPipeComponent, MoveEvent>(OnPipeMove);
        SubscribeLocalEvent<FluidPipeComponent, GetVerbsEvent<Verb>>(OnPipeVerbs);
        SubscribeLocalEvent<FluidPipeComponent, ExaminedEvent>(OnPipeExamined);
        SubscribeLocalEvent<FluidTankComponent, ExaminedEvent>(OnTankExamined);
        SubscribeLocalEvent<FluidPortsComponent, ExaminedEvent>(OnPortsExamined);
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

    private void OnPipeMove(Entity<FluidPipeComponent> pipe, ref MoveEvent args)
    {
        if (args.Component.Anchored)
            _pipesChanged = true;
        else if (args.OldRotation != args.NewRotation)
            SetLook(pipe, pipe.Comp, GetPortMask(pipe, pipe.Comp));
    }

    private void OnPipeVerbs(Entity<FluidPipeComponent> pipe, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        foreach (var shape in Enum.GetValues<CMUFluidPipeShape>())
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString($"cmu-fluid-shape-{shape.ToString().ToLowerInvariant()}"),
                Category = new VerbCategory("cmu-fluid-shape-category", null),
                Disabled = pipe.Comp.Shape == shape,
                Act = () => SetShape(pipe, shape),
            });
        }

        if (pipe.Comp.Amount > 0)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-fluid-drain-pipe"),
                Act = () =>
                {
                    pipe.Comp.Amount = 0;
                    pipe.Comp.Kind = null;
                },
            });
        }
    }

    public void SetShape(Entity<FluidPipeComponent> pipe, CMUFluidPipeShape shape)
    {
        pipe.Comp.Shape = shape;
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

    /// <summary>Only anchored, touching pipe ends on the same grid can carry liquid.</summary>
    public bool CanConnect(EntityUid pipeUid, EntityUid other)
    {
        if (TerminatingOrDeleted(pipeUid) || TerminatingOrDeleted(other)
            || !_pipeQuery.TryComp(pipeUid, out var pipe) || pipe.Isolated)
            return false;

        var a = Transform(pipeUid);
        var b = Transform(other);
        if (!a.Anchored || !b.Anchored || a.GridUid is not { } gridUid || b.GridUid != gridUid
            || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var start = _map.TileIndicesFor(gridUid, grid, a.Coordinates);
        var end = _map.TileIndicesFor(gridUid, grid, b.Coordinates);
        if (_pipeQuery.TryComp(other, out var otherPipe))
        {
            return !otherPipe.Isolated && HasOpening(pipeUid, pipe, end - start)
                   && HasOpening(other, otherPipe, start - end);
        }

        if (!TryComp<FluidPortsComponent>(other, out var ports))
            return false;

        foreach (var offset in ports.ConnectionTiles)
        {
            var rotated = b.LocalRotation.RotateVec(offset);
            var tile = end + new Vector2i((int) Math.Round(rotated.X), (int) Math.Round(rotated.Y));
            if (HasOpening(pipeUid, pipe, tile - start))
                return true;
        }

        return false;
    }

    private bool HasOpening(EntityUid uid, FluidPipeComponent pipe, Vector2i offset)
    {
        if (offset == Vector2i.Zero)
            return true;
        var mask = GetPortMask(uid, pipe);
        foreach (var (side, bit) in Sides)
        {
            if (side == offset)
                return (mask & bit) != 0;
        }

        return false;
    }

    private int GetPortMask(EntityUid uid, FluidPipeComponent pipe)
    {
        var mask = 0;
        foreach (var (offset, bit) in Sides)
        {
            if (((int) pipe.Shape & bit) == 0)
                continue;
            var rotated = Transform(uid).LocalRotation.RotateVec(offset);
            var direction = new Vector2i((int) Math.Round(rotated.X), (int) Math.Round(rotated.Y));
            foreach (var (side, rotatedBit) in Sides)
            {
                if (side == direction)
                    mask |= rotatedBit;
            }
        }

        return mask;
    }

    // Isolating a pipe removes machine links and prevents flow through neighbouring pipes.
    public void SetIsolated(EntityUid uid, bool isolated)
    {
        if (!_pipeQuery.TryComp(uid, out var pipe) || pipe.Isolated == isolated)
            return;

        pipe.Isolated = isolated;
        if (isolated)
            CutLinks(uid);

        _pipesChanged = true;
    }

    private void CutLinks(EntityUid uid)
    {
        var machines = EntityQueryEnumerator<FluidPortsComponent>();
        while (machines.MoveNext(out var machine, out var ports))
        {
            var changed = false;
            foreach (var port in ports.Ports)
                changed |= port.Links.Remove(uid);
            if (changed)
                ConnectionsChanged(machine);
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

        ConnectionsChanged(machine);
        _pipesChanged = true;
    }

    public void ConnectionsChanged(EntityUid machine)
    {
        var ev = new CMUFluidConnectionsChangedEvent();
        RaiseLocalEvent(machine, ref ev);
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

    // Rebuild only when topology changes. Open ends define both the sprite and connectivity.
    private void RebuildNeighbours()
    {
        _pipesChanged = false;
        var query = EntityQueryEnumerator<FluidPipeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pipe, out var xform))
        {
            pipe.Neighbours.Clear();
            SetLook(uid, pipe, GetPortMask(uid, pipe));
            if (pipe.Isolated || !xform.Anchored || xform.GridUid is not { } gridUid
                || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            AddAt(tile);
            foreach (var (offset, _) in Sides)
                AddAt(tile + offset);

            void AddAt(Vector2i position)
            {
                var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, position);
                while (anchored.MoveNext(out var other))
                {
                    if (other.Value != uid && _pipeQuery.HasComp(other.Value) && CanConnect(uid, other.Value))
                        pipe.Neighbours.Add(other.Value);
                }
            }
        }

        var down = EntityQueryEnumerator<FluidPipeComponent, TransformComponent>();
        while (down.MoveNext(out var uid, out var pipe, out var xform))
        {
            if (!pipe.Down || pipe.Isolated || !xform.Anchored || xform.MapUid is not { } map)
                continue;

            foreach (var below in PipesBelow(uid, map))
            {
                if (!_pipeQuery.TryComp(below, out var lower) || lower.Isolated)
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

    // Send open ends to clients, including ends that are not connected yet.
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
                Level(uid, pipe, other, step);
            }

        }
    }

    // Moves liquid between two pipes so that they fill the same share of themselves; each pair is done once, from the lower id.
    // Two pipes with different liquids in them do not mix; an empty one takes whatever the other has.
    private void Level(EntityUid uid, FluidPipeComponent pipe, EntityUid other, float step)
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
        var move = (pipe.Amount - target) * Math.Min(1f, pipe.Rate * step);

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

                if (port.Links.RemoveAll(link => !CanConnect(link, uid)) > 0)
                    ConnectionsChanged(uid);

                foreach (var link in port.Links)
                {
                    if (!_pipeQuery.TryComp(link, out var pipe) || pipe.Isolated)
                        continue;

                    var rate = port.Rate * port.Scale * step;
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
        }
        else
        {
            args.PushMarkup(Loc.GetString("au14-fluid-pipe-examine", ("fluid", Name(pipe.Comp.Kind)),
                ("amount", Math.Round(pipe.Comp.Amount, 1)), ("capacity", pipe.Comp.Capacity)));
        }
        args.PushMarkup(Loc.GetString("cmu-fluid-pipe-routing",
            ("shape", Loc.GetString($"cmu-fluid-shape-{pipe.Comp.Shape.ToString().ToLowerInvariant()}"))));
        if (pipe.Comp.Isolated)
            args.PushMarkup(Loc.GetString("cmu-fluid-pipe-isolated"));
        var machines = EntityQueryEnumerator<FluidPortsComponent>();
        while (machines.MoveNext(out var machine, out var ports))
        {
            foreach (var port in ports.Ports)
            {
                if (port.Links.Contains(pipe))
                    args.PushMarkup(Loc.GetString("cmu-fluid-pipe-machine", ("machine", Name(machine)),
                        ("port", Loc.GetString($"au14-fluid-port-{port.Id}")),
                        ("direction", Loc.GetString(port.Output ? "cmu-fluid-from-machine" : "cmu-fluid-to-machine"))));
            }
        }
    }

    private void OnTankExamined(Entity<FluidTankComponent> tank, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("cmu-fluid-tank-volume", ("amount", Math.Round(tank.Comp.Total, 1)), ("capacity", tank.Comp.Capacity)));
        if (tank.Comp.Contents.Count == 0)
        {
            args.PushMarkup(Loc.GetString("au14-fluid-tank-empty"));
            return;
        }

        foreach (var (kind, amount) in tank.Comp.Contents)
        {
            args.PushMarkup(Loc.GetString("au14-fluid-tank-examine", ("fluid", Name(kind)), ("amount", Math.Round(amount, 1))));
        }
    }

    public string ConnectionPosition(EntityUid machine, EntityUid pipe)
    {
        var xform = Transform(machine);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return Loc.GetString("cmu-fluid-port-unconnected");
        var offset = _map.TileIndicesFor(gridUid, grid, Transform(pipe).Coordinates) -
                     _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        return Loc.GetString("cmu-fluid-link-position", ("x", offset.X), ("y", offset.Y));
    }

    private void OnPortsExamined(Entity<FluidPortsComponent> machine, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        foreach (var port in machine.Comp.Ports)
        {
            args.PushMarkup(Loc.GetString("cmu-fluid-port-status",
                ("port", Loc.GetString($"au14-fluid-port-{port.Id}")),
                ("direction", Loc.GetString(port.Output ? "au14-fluid-ports-out" : "au14-fluid-ports-in")),
                ("count", port.Links.Count), ("capacity", port.MaxLinks)));
        }
        args.PushMarkup(Loc.GetString("cmu-fluid-connections-hint"));
    }
}
