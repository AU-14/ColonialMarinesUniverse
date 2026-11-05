// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Power;

namespace Content.Server.CMU14.Power;

// Keeps the list of the cables run by hand on both devices, for the drawing of them.
public sealed class DirectCableVisualSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DirectCableVisualComponent, ComponentShutdown>(OnShutdown);
    }

    public void Link(EntityUid a, EntityUid b, string group)
    {
        Add(a, b, group);
        Add(b, a, group);
    }

    public void Unlink(EntityUid a, EntityUid b)
    {
        Remove(a, b);
        Remove(b, a);
    }

    private void Add(EntityUid uid, EntityUid other, string group)
    {
        var comp = EnsureComp<DirectCableVisualComponent>(uid);
        var net = GetNetEntity(other);
        for (var i = 0; i < comp.Others.Count; i++)
        {
            if (comp.Others[i] == net && comp.Groups[i] == group)
                return;
        }

        comp.Others.Add(net);
        comp.Groups.Add(group);
        Dirty(uid, comp);
    }

    private void Remove(EntityUid uid, EntityUid other)
    {
        if (!TryComp<DirectCableVisualComponent>(uid, out var comp))
            return;

        var net = GetNetEntity(other);
        var changed = false;
        for (var i = comp.Others.Count - 1; i >= 0; i--)
        {
            if (comp.Others[i] != net)
                continue;

            comp.Others.RemoveAt(i);
            comp.Groups.RemoveAt(i);
            changed = true;
        }

        if (!changed)
            return;

        if (comp.Others.Count == 0)
            RemCompDeferred<DirectCableVisualComponent>(uid);
        else
            Dirty(uid, comp);
    }

    // A device that is gone takes its cables with it.
    private void OnShutdown(Entity<DirectCableVisualComponent> device, ref ComponentShutdown args)
    {
        foreach (var other in device.Comp.Others.ToArray())
        {
            if (TryGetEntity(other, out var uid))
                Remove(uid.Value, device.Owner);
        }
    }
}
