// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.CMU14.Steam;
using Content.Shared.Examine;

namespace Content.Server.CMU14.Fluids;

// Steam is the water vapor gas of the atmosphere, in the ordinary gas pipes. The machines that run on steam, and the boiler that
// makes it, have a pipe node of the gas network; this is how they take it out of that node and put it into it.
public sealed class SteamGasSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly NodeContainerSystem _nodes = default!;

    // The heat of steam as it comes out of a boiler.
    public const float BoilingPoint = 373.15f;

    private const float MirrorInterval = 0.25f;

    private float _timer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SteamPortComponent, ExaminedEvent>(OnExamined);
    }

    private bool TryNode(EntityUid uid, out PipeNode node)
    {
        node = default!;
        if (!TryComp<SteamPortComponent>(uid, out var port) || !_nodes.TryGetNode(uid, port.Node, out PipeNode? found) || found.NodeGroup == null)
            return false;

        node = found;
        return true;
    }

    // Moles of water vapor in the pipes the machine is joined to.
    public float GetMoles(EntityUid uid)
    {
        return TryNode(uid, out var node) ? node.Air.GetMoles(Gas.WaterVapor) : 0f;
    }

    public float GetPressure(EntityUid uid)
    {
        return TryNode(uid, out var node) ? node.Air.Pressure : 0f;
    }

    // Uses up steam: all of it or none.
    public bool TryUse(EntityUid uid, float moles)
    {
        if (moles <= 0f)
            return true;

        if (!TryNode(uid, out var node) || node.Air.GetMoles(Gas.WaterVapor) < moles)
            return false;

        node.Air.AdjustMoles(Gas.WaterVapor, -moles);
        return true;
    }

    // Puts steam into the pipes, as long as their pressure is under the limit. Returns how much went in.
    public float Add(EntityUid uid, float moles, float maxPressure, float temperature = BoilingPoint)
    {
        if (moles <= 0f || !TryNode(uid, out var node) || node.Air.Pressure >= maxPressure)
            return 0f;

        var steam = new GasMixture(1f) { Temperature = temperature };
        steam.AdjustMoles(Gas.WaterVapor, moles);
        _atmos.Merge(node.Air, steam);
        return moles;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _timer += frameTime;
        if (_timer < MirrorInterval)
            return;

        _timer = 0f;

        // What the windows show: the steam in the pipes next to each machine.
        var query = EntityQueryEnumerator<SteamPortComponent>();
        while (query.MoveNext(out var uid, out var port))
        {
            var moles = GetMoles(uid);
            var pressure = GetPressure(uid);
            if (MathF.Abs(moles - port.Moles) < 0.02f && MathF.Abs(pressure - port.Pressure) < 0.5f)
                continue;

            port.Moles = moles;
            port.Pressure = pressure;
            Dirty(uid, port);
        }
    }

    private void OnExamined(Entity<SteamPortComponent> machine, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("au14-steam-port-examine", ("moles", MathF.Round(GetMoles(machine), 1)), ("pressure", (int) GetPressure(machine))));
    }
}
