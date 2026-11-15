// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using Content.Shared.Popups;
using Content.Server.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Automation;

// The button "disconnect from everything" in the right-click menu of a machine, a pipe and what has ports: the pipes let go of the machine (and the machine of them), the
// automatic input and output are turned off, a pipe is cut off from the pipes beside it, and the machine is cut off from the power. "Connect back" gives the power and the
// pipe its place again; the pipes and the ports of a machine have to be joined once more by hand.
public sealed class DisconnectSystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly Content.Server.CMU14.Electric.AutoPortsSystem _ports = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FluidPortsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FluidPipeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AutoPortsComponent, GetVerbsEvent<AlternativeVerb>>(OnPortsVerbs);
    }

    private void OnPortsVerbs(Entity<AutoPortsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        OnGetVerbs(ent, ref args);
        _ports.AddSwitchVerbs(ent, ref args);
    }

    private void OnGetVerbs<T>(Entity<T> ent, ref GetVerbsEvent<AlternativeVerb> args) where T : IComponent
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // A machine with ports of several kinds (liquids and boxes) is asked twice, but a verb that is the same as one in the menu is not added again.
        var user = args.User;
        var target = ent.Owner;

        if (IsCutOff(target))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("au14-reconnect-verb"),
                Priority = 9,
                Act = () => Reconnect(target, user),
            });
        }
        else
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("au14-disconnect-verb"),
                Priority = 9,
                Act = () => Disconnect(target, user),
            });
        }
    }

    // Whether it has been cut off: a pipe that is isolated, or a machine with its power cut.
    private bool IsCutOff(EntityUid target)
    {
        if (TryComp<FluidPipeComponent>(target, out var pipe))
            return pipe.Isolated;

        return TryComp<ApcPowerReceiverComponent>(target, out var power) && power.PowerDisabled;
    }

    public void Disconnect(EntityUid target, EntityUid? user)
    {
        _fluids.CutPorts(target);
        _fluids.SetIsolated(target, true);

        if (TryComp<AutoPortsComponent>(target, out var ports))
        {
            ports.AutoInput = false;
            ports.AutoOutput = false;
            Dirty(target, ports);
        }

        if (HasComp<ApcPowerReceiverComponent>(target))
            _power.SetPowerDisabled(target, true);

        if (user != null)
            _popup.PopupEntity(Loc.GetString("au14-disconnect-done"), target, user.Value);
    }

    public void Reconnect(EntityUid target, EntityUid? user)
    {
        _fluids.SetIsolated(target, false);

        if (HasComp<ApcPowerReceiverComponent>(target))
            _power.SetPowerDisabled(target, false);

        if (user != null)
            _popup.PopupEntity(Loc.GetString("au14-reconnect-done"), target, user.Value);
    }
}
