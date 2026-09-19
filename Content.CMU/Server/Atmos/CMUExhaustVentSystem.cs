using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Atmos;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Atmos;

public sealed class CMUExhaustVentSystem : EntitySystem
{
    private const string ScrewingQuality = "Screwing";

    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly CMUOutdoorAtmosphereSystem _outdoor = default!;

    private EntityQuery<MapGridComponent> _gridQuery = default!;

    public override void Initialize()
    {
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<CMUExhaustVentComponent, AtmosDeviceUpdateEvent>(OnUpdate);
        SubscribeLocalEvent<CMUExhaustVentComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUExhaustVentComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUpdate(Entity<CMUExhaustVentComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!ent.Comp.IsOpen
            || !_nodeContainer.TryGetNode<PipeNode>(ent.Owner, ent.Comp.InletName, out var inlet))
            return;

        var inletAir = inlet.Air;
        if (inletAir.Pressure < ent.Comp.MinInletPressure)
            return;

        var xform = Transform(ent);
        if (!xform.Anchored
            || xform.GridUid is not { } grid
            || !_gridQuery.TryComp(grid, out var mapGrid))
            return;

        var indices = _map.CoordinatesToTile(grid, mapGrid, xform.Coordinates);
        if (!_outdoor.IsSkyExposed(grid, indices))
            return;

        // Dumped into the sky reservoir: deletion, the same bookkeeping as gas
        // moved onto a space tile.
        inletAir.Remove(MathF.Min(inletAir.TotalMoles, ent.Comp.MaxTransferMoles));
    }

    private void OnInteractUsing(Entity<CMUExhaustVentComponent> ent, ref InteractUsingEvent args)
    {
        if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
            return;

        if (!_tool.HasQuality(args.Used, ScrewingQuality))
            return;

        args.Handled = true;
        ent.Comp.IsOpen = !ent.Comp.IsOpen;

        var state = Loc.GetString(ent.Comp.IsOpen ? "cmu-exhaust-vent-open" : "cmu-exhaust-vent-closed");
        _popup.PopupClient(Loc.GetString("cmu-exhaust-vent-toggle-state", ("state", state)), ent, args.User);
    }

    private void OnExamined(Entity<CMUExhaustVentComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var xform = Transform(ent);
        if (xform.Anchored
            && xform.GridUid is { } grid
            && _gridQuery.TryComp(grid, out var mapGrid))
        {
            var indices = _map.CoordinatesToTile(grid, mapGrid, xform.Coordinates);
            if (!_outdoor.IsSkyExposed(grid, indices))
            {
                args.PushMarkup(Loc.GetString("cmu-exhaust-vent-state-blocked"));
                return;
            }
        }

        args.PushMarkup(Loc.GetString(ent.Comp.IsOpen
            ? "cmu-exhaust-vent-state-open"
            : "cmu-exhaust-vent-state-closed"));
    }
}
