// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Fluids;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Fluids;

public sealed partial class FluidLinkSystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private readonly Dictionary<EntityUid, EntityUid> _picked = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FluidPipeComponent, InteractUsingEvent>(OnPipeUsing);
        SubscribeLocalEvent<FluidPortsComponent, InteractUsingEvent>(OnMachineUsing);
        SubscribeLocalEvent<FluidPortsComponent, GetVerbsEvent<Verb>>(OnMachineVerbs);
        SubscribeLocalEvent<FluidPortsComponent, FluidPortChosenMessage>(OnPortChosen);
        SubscribeLocalEvent<FluidPortsComponent, CMUFluidPortDisconnectMessage>(OnDisconnect);
        SubscribeLocalEvent<FluidPortsComponent, CMUFluidConnectionsChangedEvent>(OnConnectionsChanged);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ => _picked.Clear());
    }

    private bool IsScrewdriver(EntityUid used)
        => TryComp<ToolComponent>(used, out var tool) && tool.Qualities.Contains("Screwing");

    private bool CanConfigure(EntityUid user, EntityUid machine)
        => _hands.TryGetActiveItem(user, out var held) && IsScrewdriver(held.Value)
           && _interaction.InRangeUnobstructed(user, machine);

    private void OnPipeUsing(Entity<FluidPipeComponent> pipe, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsScrewdriver(args.Used))
            return;
        args.Handled = true;
        if (_picked.TryGetValue(args.User, out var selected) && selected == pipe.Owner)
        {
            _picked.Remove(args.User);
            _popup.PopupEntity(Loc.GetString("au14-fluid-pipe-put-down"), pipe, args.User);
            return;
        }

        _picked[args.User] = pipe;
        _popup.PopupEntity(Loc.GetString("au14-fluid-pipe-picked"), pipe, args.User);
    }

    private void OnMachineUsing(Entity<FluidPortsComponent> machine, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsScrewdriver(args.Used))
            return;
        args.Handled = true;
        Open(machine, args.User);
    }

    private void OnMachineVerbs(Entity<FluidPortsComponent> machine, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-fluid-connections-verb"),
            Act = () => Open(machine, user),
        });
    }

    private void Open(Entity<FluidPortsComponent> machine, EntityUid user)
    {
        if (!_ui.HasUi(machine.Owner, FluidPortsUiKey.Key))
            return;
        UpdateState(machine);
        _ui.OpenUi(machine.Owner, FluidPortsUiKey.Key, user);
    }

    private void OnConnectionsChanged(Entity<FluidPortsComponent> machine, ref CMUFluidConnectionsChangedEvent args)
        => UpdateState(machine);

    private void UpdateState(Entity<FluidPortsComponent> machine)
    {
        if (!_ui.HasUi(machine.Owner, FluidPortsUiKey.Key))
            return;
        var entries = new List<FluidPortEntry>();
        foreach (var port in machine.Comp.Ports)
        {
            var links = new List<CMUFluidPortLink>();
            foreach (var pipe in port.Links)
            {
                if (!TerminatingOrDeleted(pipe))
                    links.Add(new CMUFluidPortLink(GetNetEntity(pipe), Name(pipe), _fluids.ConnectionPosition(machine, pipe)));
            }
            entries.Add(new FluidPortEntry(port.Id, port.Output, port.Kind != null ? _fluids.Name(port.Kind) :
                Loc.GetString("cmu-fluid-any-liquid"), port.MaxLinks, links));
        }
        _ui.SetUiState(machine.Owner, FluidPortsUiKey.Key, new FluidPortsBoundUserInterfaceState(entries));
    }

    private void OnPortChosen(Entity<FluidPortsComponent> machine, ref FluidPortChosenMessage args)
    {
        if (!_picked.TryGetValue(args.Actor, out var pipe))
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-pick-pipe"), machine, args.Actor);
            return;
        }
        if (TryConnect(machine, args.Port, pipe, args.Actor))
            _picked.Remove(args.Actor);
    }

    /// <summary>Validates geometry, tool and available capacity at the time of connection.</summary>
    public bool TryConnect(Entity<FluidPortsComponent> machine, string portId, EntityUid pipeUid, EntityUid user)
    {
        if (!CanConfigure(user, machine))
        {
            _popup.PopupEntity(Loc.GetString("cmu-fluid-need-screwdriver"), machine, user);
            return false;
        }
        if (!_fluids.CanConnect(pipeUid, machine))
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-too-far"), machine, user, PopupType.SmallCaution);
            return false;
        }
        var pipe = Comp<FluidPipeComponent>(pipeUid);
        var port = machine.Comp.Ports.Find(p => p.Id == portId);
        if (port == null || port.Links.Count >= port.MaxLinks
            || (port.Kind != null && pipe.Kind != null && port.Kind != pipe.Kind))
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-link-no-port"), machine, user, PopupType.SmallCaution);
            return false;
        }
        foreach (var existing in machine.Comp.Ports)
        {
            if (!existing.Links.Contains(pipeUid))
                continue;
            _popup.PopupEntity(Loc.GetString("cmu-fluid-already-connected"), machine, user);
            return false;
        }
        port.Links.Add(pipeUid);
        _fluids.ConnectionsChanged(machine);
        _popup.PopupEntity(Loc.GetString("au14-fluid-link-made-port", ("port", Loc.GetString($"au14-fluid-port-{port.Id}"))), machine, user);
        return true;
    }

    private void OnDisconnect(Entity<FluidPortsComponent> machine, ref CMUFluidPortDisconnectMessage args)
    {
        if (!CanConfigure(args.Actor, machine))
        {
            _popup.PopupEntity(Loc.GetString("cmu-fluid-need-screwdriver"), machine, args.Actor);
            return;
        }
        var pipe = GetEntity(args.Pipe);
        var portId = args.Port;
        var port = machine.Comp.Ports.Find(p => p.Id == portId);
        if (port == null || !port.Links.Remove(pipe))
            return;
        _fluids.ConnectionsChanged(machine);
        _popup.PopupEntity(Loc.GetString("au14-fluid-link-removed"), machine, args.Actor);
    }
}
