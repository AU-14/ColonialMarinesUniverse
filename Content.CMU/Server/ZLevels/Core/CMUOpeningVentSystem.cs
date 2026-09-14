using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed class CMUOpeningVentSystem : EntitySystem
{
    private const string ScrewingQuality = "Screwing";
    private const string PryingQuality = "Prying";

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly CMUZPairingSystem _zPairing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUOpeningVentComponent, AtmosDeviceUpdateEvent>(OnUpdate);
        SubscribeLocalEvent<CMUOpeningVentComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUOpeningVentComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUpdate(Entity<CMUOpeningVentComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!TryComp<CMUZPairedComponent>(ent, out var paired) || paired.Twin is not { } twin)
            return;
        if (!ent.Comp.IsOpen || !TryComp<CMUOpeningVentComponent>(twin, out var twinVent) || !twinVent.IsOpen)
            return;
        if (!TryGetTileMixture(ent, out var mine) || !TryGetTileMixture(twin, out var theirs))
            return;

        // Lockout parity with the vent pump: never feed a deck that has fallen
        // to near-vacuum, or the pair drains the whole z-network into the hole.
        if (MathF.Min(mine.Pressure, theirs.Pressure) < ent.Comp.LockoutThreshold)
            return;

        if (MathF.Abs(mine.Pressure - theirs.Pressure) < ent.Comp.Threshold)
            return;

        // Equal volume per tile, so moving half the mole difference lands on
        // the equalization point. The cap paces it across ticks.
        var (hi, lo) = mine.Pressure > theirs.Pressure ? (mine, theirs) : (theirs, mine);
        var transfer = MathF.Min((hi.TotalMoles - lo.TotalMoles) * 0.5f, ent.Comp.MaxTransferMoles);
        if (transfer > 0f)
            _atmosphere.Merge(lo, hi.Remove(transfer));
    }

    private void OnInteractUsing(Entity<CMUOpeningVentComponent> ent, ref InteractUsingEvent args)
    {
        if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
            return;

        if (!TryComp<CMUZPairedComponent>(ent, out var paired))
            return;

        if (_tool.HasQuality(args.Used, ScrewingQuality))
        {
            args.Handled = true;
            ent.Comp.IsOpen = !ent.Comp.IsOpen;

            var state = Loc.GetString(ent.Comp.IsOpen ? "cmu-opening-vent-open" : "cmu-opening-vent-closed");
            _popup.PopupClient(Loc.GetString("cmu-opening-vent-toggle-state", ("state", state)), ent, args.User);
        }
        else if (_tool.HasQuality(args.Used, PryingQuality))
        {
            args.Handled = true;

            paired.Offset = -paired.Offset;
            Dirty(ent.Owner, paired);

            var direction = Loc.GetString(paired.Offset > 0 ? "cmu-z-direction-above" : "cmu-z-direction-below");
            _popup.PopupClient(Loc.GetString("cmu-opening-vent-dir-toggled", ("direction", direction)), ent, args.User);

            _zPairing.Unpair((ent.Owner, paired));
            _zPairing.TryPair((ent.Owner, paired));
        }
    }

    private void OnExamined(Entity<CMUOpeningVentComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<CMUZPairedComponent>(ent, out var paired))
            return;

        if (paired.Twin is { } twin && TryGetTileMixture(ent, out var mine) && TryGetTileMixture(twin, out var theirs))
        {
            var state = !ent.Comp.IsOpen
                ? "cmu-opening-vent-state-closed"
                : MathF.Min(mine.Pressure, theirs.Pressure) < ent.Comp.LockoutThreshold
                    ? "cmu-opening-vent-state-locked"
                    : "cmu-opening-vent-state-open";
            args.PushMarkup(Loc.GetString(state));
            args.PushMarkup(Loc.GetString("cmu-opening-vent-examine",
                ("here", $"{mine.Pressure:F0}"),
                ("there", $"{theirs.Pressure:F0}")));
        }
        else
        {
            args.PushMarkup(Loc.GetString("cmu-opening-vent-unlinked"));
        }
    }

    private bool TryGetTileMixture(EntityUid uid, out GasMixture mixture)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            mixture = default!;
            return false;
        }

        var indices = _map.CoordinatesToTile(grid, gridComp, xform.Coordinates);
        mixture = _atmosphere.GetTileMixture(grid, xform.MapUid, indices)!;
        return mixture != null;
    }
}
