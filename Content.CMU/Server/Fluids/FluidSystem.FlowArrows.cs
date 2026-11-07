using System.Linq;
using Content.Shared.CMU14.Fluids;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server.CMU14.Fluids;

public sealed partial class FluidSystem
{
    private bool _flowArrowsChanged;
    private Dictionary<EntityUid, List<CMUFluidPipeArrow>> _flowArrowState = new();

    private void OnPortsShutdown(Entity<FluidPortsComponent> machine, ref ComponentShutdown args)
    {
        _flowArrowsChanged = true;
    }

    private void OnPortsMoved(Entity<FluidPortsComponent> machine, ref MoveEvent args)
    {
        foreach (var port in machine.Comp.Ports)
        {
            if (port.Links.Count == 0)
                continue;
            _flowArrowsChanged = true;
            break;
        }
    }

    // Connection markers change with topology, not with each fluid simulation tick.
    private void RefreshFlowArrows()
    {
        _flowArrowsChanged = false;
        var next = new Dictionary<EntityUid, List<CMUFluidPipeArrow>>();
        var machines = EntityQueryEnumerator<FluidPortsComponent, TransformComponent>();
        while (machines.MoveNext(out var machine, out var ports, out var xform))
        {
            if (!xform.Anchored || xform.GridUid is not { } gridUid
                || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;
            var end = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            foreach (var port in ports.Ports)
            {
                foreach (var pipeUid in port.Links)
                {
                    if (!CanConnect(pipeUid, machine) || !_pipeQuery.TryComp(pipeUid, out var pipe))
                        continue;
                    var start = _map.TileIndicesFor(gridUid, grid, Transform(pipeUid).Coordinates);
                    if (!TryGetMachineContact(pipeUid, pipe, xform, ports, start, end, out var side))
                        continue;
                    var onMachineTile = side == Vector2i.Zero;
                    if (onMachineTile)
                    {
                        // A pipe under the machine marks an open arm: toward the centre is INPUT.
                        var mask = GetPortMask(pipeUid, pipe);
                        foreach (var (offset, bit) in Sides)
                        {
                            if ((mask & bit) == 0)
                                continue;
                            side = offset;
                            break;
                        }
                    }
                    if (!next.TryGetValue(pipeUid, out var arrows))
                        next[pipeUid] = arrows = new List<CMUFluidPipeArrow>();
                    var arrow = new CMUFluidPipeArrow(side, port.Output, onMachineTile);
                    if (!arrows.Contains(arrow))
                        arrows.Add(arrow);
                }
            }
        }

        foreach (var pipe in _flowArrowState.Keys)
        {
            if (!next.ContainsKey(pipe) && !TerminatingOrDeleted(pipe))
                _appearance.SetData(pipe, FluidPipeVisuals.CMUFlowArrows, new CMUFluidPipeArrowState(new()));
        }
        foreach (var (pipe, arrows) in next)
        {
            if (!_flowArrowState.TryGetValue(pipe, out var old) || !old.SequenceEqual(arrows))
                _appearance.SetData(pipe, FluidPipeVisuals.CMUFlowArrows, new CMUFluidPipeArrowState(arrows));
        }
        _flowArrowState = next;
    }
}
