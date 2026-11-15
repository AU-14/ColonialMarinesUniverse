// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Construction.Components;
using Content.Shared.Destructible;
using Content.Shared.NodeContainer;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Steam;

// The puff of steam that comes out of a gas pipe when it lets go of its steam: the pipe that a wrench takes off the floor, or that breaks, with
// water vapor in it. The gas itself is let out by the atmosphere systems of the game; this only shows it, with puffs and a hiss, before they do.
public sealed class SteamLeakEffectSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly EntProtoId PuffEffect = "RUMCEffectSteamPuff";
    private static readonly SoundSpecifier HissSound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg", AudioParams.Default.WithVolume(-4f));

    // Less water vapor than this in a pipe is not worth a puff.
    private const float MinMoles = 0.1f;

    // One puff for this many moles, from one to four.
    private const float MolesPerPuff = 1.5f;

    public override void Initialize()
    {
        base.Initialize();

        // On the node container, not on the component of the atmosphere system: one component takes one handler of an event. Before that system
        // empties the pipe, or there would be nothing left to see.
        SubscribeLocalEvent<NodeContainerComponent, UserUnanchoredEvent>(OnUnanchored, before: new[] { typeof(AtmosUnsafeUnanchorSystem) });
        SubscribeLocalEvent<NodeContainerComponent, BreakageEventArgs>(OnBreak, before: new[] { typeof(AtmosUnsafeUnanchorSystem) });
    }

    private void OnUnanchored(EntityUid uid, NodeContainerComponent comp, UserUnanchoredEvent args)
    {
        if (TryComp<AtmosUnsafeUnanchorComponent>(uid, out var unsafeUnanchor) && unsafeUnanchor.Enabled)
            Puff(uid);
    }

    private void OnBreak(EntityUid uid, NodeContainerComponent comp, BreakageEventArgs args)
    {
        if (HasComp<AtmosUnsafeUnanchorComponent>(uid))
            Puff(uid);
    }

    // Moles of water vapor that the pipe holds of the steam of its network: its share of the network, by volume.
    public float GetSteam(EntityUid uid)
    {
        if (!TryComp<NodeContainerComponent>(uid, out var nodes))
            return 0f;

        var total = 0f;
        foreach (var node in nodes.Nodes.Values)
        {
            if (node is not PipeNode pipe)
                continue;

            var air = pipe.Air;
            if (air.Volume <= 0f)
                continue;

            total += air.GetMoles(Gas.WaterVapor) * Math.Min(1f, pipe.Volume / air.Volume);
        }

        return total;
    }

    private void Puff(EntityUid uid)
    {
        var moles = GetSteam(uid);
        if (moles < MinMoles)
            return;

        var puffs = Math.Clamp((int) Math.Ceiling(moles / MolesPerPuff), 1, 4);
        var coordinates = Transform(uid).Coordinates;
        for (var i = 0; i < puffs; i++)
            Spawn(PuffEffect, coordinates.Offset(_random.NextVector2(0.3f)));

        _audio.PlayPvs(HissSound, coordinates);
    }
}
