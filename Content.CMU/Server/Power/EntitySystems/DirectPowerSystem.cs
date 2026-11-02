// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._RMC14.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Power;

// The low voltage cable run by hand: a machine joined to a panel or an APC (or to another machine that is joined) takes its power from that APC, wherever it stands,
// and goes back to the nearest one in its reach when the cable is cut. The cables of the higher voltages are plain nodes of the grid and need nothing of this.
public sealed class DirectPowerSystem : EntitySystem
{
    [Dependency] private readonly ExtensionCableSystem _extension = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiver = default!;

    // The receivers that were given their provider by a cable: they are looked after, so that a provider that came and went does not strand them.
    private readonly HashSet<EntityUid> _direct = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly Queue<EntityUid> _queue = new();
    private readonly List<EntityUid> _members = new();
    private TimeSpan _next;

    private static readonly TimeSpan Every = TimeSpan.FromSeconds(2);

    public void Linked(EntityUid a, EntityUid b)
    {
        Refresh(a);
        Refresh(b);
    }

    public void Unlinked(EntityUid a, EntityUid b)
    {
        Refresh(a);
        Refresh(b);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _next || _direct.Count == 0)
            return;

        _next = _timing.CurTime + Every;
        foreach (var receiver in _direct.ToArray())
        {
            if (TerminatingOrDeleted(receiver))
                _direct.Remove(receiver);
            else
                Refresh(receiver);
        }
    }

    // Takes the whole run of the low voltage cables the device is on, and gives the receivers on it the provider on it.
    private void Refresh(EntityUid start)
    {
        _seen.Clear();
        _members.Clear();
        _queue.Clear();
        _queue.Enqueue(start);
        _seen.Add(start);

        while (_queue.Count > 0)
        {
            var uid = _queue.Dequeue();
            _members.Add(uid);

            if (!TryComp<NodeContainerComponent>(uid, out var container))
                continue;

            foreach (var node in container.Nodes.Values)
            {
                if (node.NodeGroupID != NodeGroupID.Apc)
                    continue;

                foreach (var link in node.DirectLinks)
                {
                    if (_seen.Add(link.Owner))
                        _queue.Enqueue(link.Owner);
                }
            }
        }

        Entity<ExtensionCableProviderComponent>? provider = null;
        foreach (var uid in _members)
        {
            if (TryComp<ExtensionCableProviderComponent>(uid, out var found) && found.Connectable)
            {
                provider = (uid, found);
                break;
            }
        }

        foreach (var uid in _members)
        {
            if (!TryComp<ExtensionCableReceiverComponent>(uid, out var receiver))
                continue;

            if (provider is { } source)
            {
                TakeOutOfAreaPower(uid);
                _extension.ConnectDirect((uid, receiver), source);
                _direct.Add(uid);
            }
            else if (_direct.Remove(uid))
            {
                _extension.DisconnectDirect((uid, receiver));
            }
        }
    }

    // On a map of area power every machine is taken into the area of the map, and its power is told by the APC of that area, not by the one its cable runs to.
    // A machine that has a cable of its own to an APC is on that APC's net like the wired ones of the grid maps, so the area lets it go and the net tells its power.
    private void TakeOutOfAreaPower(EntityUid uid)
    {
        if (!TryComp<RMCPowerReceiverComponent>(uid, out var area) || !HasComp<ApcPowerReceiverComponent>(uid))
            return;

        var load = TryComp<PowerStateComponent>(uid, out var state)
            ? state.IsWorking ? state.WorkingPowerDraw : state.IdlePowerDraw
            : area.ActiveLoad;

        RemComp<RMCPowerReceiverComponent>(uid);
        _powerReceiver.SetLoad((uid, null), load);
    }
}
