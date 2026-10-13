// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Fluids;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Fluids;

// Joins the pipes to the machines, and the pipes to each other: a screwdriver on a pipe picks it, and a screwdriver on a machine
// then joins the pipe to a port of the machine that has the fluid the pipe carries. The same two clicks again take them apart.
public sealed class FluidLinkSystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    // The farthest a link reaches: a pipe and a machine further apart than this cannot be joined.
    private const float MaxDistance = 25f;

    // The pipe each user has picked.
    private readonly Dictionary<EntityUid, EntityUid> _picked = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FluidPipeComponent, InteractUsingEvent>(OnPipeUsing);
        SubscribeLocalEvent<FluidPortsComponent, InteractUsingEvent>(OnMachineUsing);
        SubscribeLocalEvent<FluidPortsComponent, FluidPortChosenMessage>(OnPortChosen);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ => _picked.Clear());
    }

    private bool IsScrewdriver(EntityUid used)
    {
        return TryComp<ToolComponent>(used, out var tool) && tool.Qualities.Contains("Screwing");
    }

    private void OnPipeUsing(Entity<FluidPipeComponent> pipe, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsScrewdriver(args.Used))
            return;

        args.Handled = true;
        var user = args.User;

        // Nothing picked yet, or the same pipe again: pick it (or put it down).
        if (!_picked.TryGetValue(user, out var first) || TerminatingOrDeleted(first))
        {
            _picked[user] = pipe.Owner;
            _popup.PopupEntity(Loc.GetString("au14-fluid-pipe-picked"), pipe, user);
            return;
        }

        if (first == pipe.Owner)
        {
            _picked.Remove(user);
            _popup.PopupEntity(Loc.GetString("au14-fluid-pipe-put-down"), pipe, user);
            return;
        }

        // Another pipe: the two are joined, or taken apart if they were joined already.
        if (!TryComp<FluidPipeComponent>(first, out var firstPipe) || (firstPipe.Kind != null && pipe.Comp.Kind != null && firstPipe.Kind != pipe.Comp.Kind))
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-wrong-kind"), pipe, user, PopupType.SmallCaution);
            return;
        }

        if (!InReach(first, pipe.Owner))
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-too-far"), pipe, user, PopupType.SmallCaution);
            return;
        }

        if (firstPipe.Links.Remove(pipe.Owner))
        {
            pipe.Comp.Links.Remove(first);
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-removed"), pipe, user);
        }
        else
        {
            firstPipe.Links.Add(pipe.Owner);
            pipe.Comp.Links.Add(first);
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-made"), pipe, user);
        }

        _fluids.MarkPipesChanged();
        _picked.Remove(user);
    }

    private void OnMachineUsing(Entity<FluidPortsComponent> machine, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsScrewdriver(args.Used))
            return;

        var user = args.User;
        if (!_picked.TryGetValue(user, out var pipeUid) || TerminatingOrDeleted(pipeUid) || !TryComp<FluidPipeComponent>(pipeUid, out var pipe))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-pick-pipe"), machine, user, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        if (!InReach(pipeUid, machine.Owner))
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-too-far"), machine, user, PopupType.SmallCaution);
            return;
        }

        // The second click on a machine that the pipe is already joined to takes them apart.
        foreach (var port in machine.Comp.Ports)
        {
            if (port.Links.Remove(pipeUid))
            {
                _popup.PopupEntity(Loc.GetString("au14-fluid-link-removed"), machine, user);
                _picked.Remove(user);
                _fluids.MarkPipesChanged();
                return;
            }
        }

        // Otherwise the pipe goes to a port of the machine that fits it. With more than one to choose from the user is asked.
        var fits = Fitting(machine.Comp, pipe);
        if (fits.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-no-port"), machine, user, PopupType.SmallCaution);
            return;
        }

        // More than one port fits: the window asks which. Somebody without a player (a test, a puppet) has no window to ask in.
        if (fits.Count > 1)
        {
            if (_ui.HasUi(machine.Owner, FluidPortsUiKey.Key) && _player.TryGetSessionByEntity(user, out _))
            {
                _ui.SetUiState(machine.Owner, FluidPortsUiKey.Key, StateFor(machine.Comp, pipeUid));
                _ui.OpenUi(machine.Owner, FluidPortsUiKey.Key, user);
            }

            return;
        }

        Join(machine, fits[0], pipeUid, user);
    }

    // The free ports of the machine that the pipe could be joined to.
    private static List<FluidPort> Fitting(FluidPortsComponent machine, FluidPipeComponent pipe)
    {
        var fits = new List<FluidPort>();
        foreach (var port in machine.Ports)
        {
            if ((port.Kind != null && pipe.Kind != null && port.Kind != pipe.Kind) || port.Links.Count >= port.MaxLinks)
                continue;

            fits.Add(port);
        }

        return fits;
    }

    private void Join(Entity<FluidPortsComponent> machine, FluidPort port, EntityUid pipe, EntityUid user)
    {
        port.Links.Add(pipe);
        _popup.PopupEntity(Loc.GetString("au14-fluid-link-made-port", ("port", Loc.GetString($"au14-fluid-port-{port.Id}"))), machine, user);
        _picked.Remove(user);
        _fluids.MarkPipesChanged();
    }

    private FluidPortsBoundUserInterfaceState StateFor(FluidPortsComponent machine, EntityUid pipe)
    {
        var entries = new List<FluidPortEntry>();
        foreach (var port in machine.Ports)
        {
            entries.Add(new FluidPortEntry(port.Id, port.Output, port.Kind != null ? _fluids.Name(port.Kind) : string.Empty, port.Links.Contains(pipe), port.Links.Count));
        }

        return new FluidPortsBoundUserInterfaceState(entries);
    }

    // A port picked in the window takes the pipe that the user has picked, or lets it go if it is joined to it already.
    private void OnPortChosen(Entity<FluidPortsComponent> machine, ref FluidPortChosenMessage args)
    {
        var user = args.Actor;
        _ui.CloseUi(machine.Owner, FluidPortsUiKey.Key, user);

        if (!_picked.TryGetValue(user, out var pipeUid) || TerminatingOrDeleted(pipeUid) || !TryComp<FluidPipeComponent>(pipeUid, out var pipe) ||
            !InReach(pipeUid, machine.Owner))
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-pick-pipe"), machine, user, PopupType.SmallCaution);
            return;
        }

        var portId = args.Port;
        var port = machine.Comp.Ports.Find(p => p.Id == portId);
        if (port == null)
            return;

        if (port.Links.Remove(pipeUid))
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-removed"), machine, user);
            _picked.Remove(user);
            _fluids.MarkPipesChanged();
            return;
        }

        if ((port.Kind != null && pipe.Kind != null && port.Kind != pipe.Kind) || port.Links.Count >= port.MaxLinks)
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-no-port"), machine, user, PopupType.SmallCaution);
            return;
        }

        Join(machine, port, pipeUid, user);
    }

    private bool InReach(EntityUid a, EntityUid b)
    {
        var first = _transform.ToMapCoordinates(Transform(a).Coordinates);
        var second = _transform.ToMapCoordinates(Transform(b).Coordinates);
        return first.MapId == second.MapId && (first.Position - second.Position).Length() <= MaxDistance;
    }
}
