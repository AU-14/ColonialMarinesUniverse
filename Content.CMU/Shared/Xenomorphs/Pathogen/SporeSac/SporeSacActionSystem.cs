using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared._RMC14.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Xenomorphs.Pathogen.SporeSac;

/// <summary>
/// Handles the Popper's ability to place a Spore Sac structure at a target
/// tile. Owns CMUXenoSporeSacComponent (the ability/caster side), distinct
/// from CMUPathogenSporeSacSystem which owns the placed sac itself.
/// </summary>
public sealed partial class CMUXenoSporeSacSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoSporeSacComponent, CMUXenoSporeSacActionEvent>(OnAction);
        SubscribeLocalEvent<CMUXenoSporeSacComponent, CMUXenoPlaceSporeSacDoAfterEvent>(OnPlaceFinished);
    }

    private void OnAction(Entity<CMUXenoSporeSacComponent> xeno, ref CMUXenoSporeSacActionEvent args)
    {
        if (args.Handled)
            return;

        if (!args.Target.TryDistance(EntityManager, _transform.GetMoverCoordinates(xeno), out var dist)
            || dist > xeno.Comp.Range)
        {
            _popup.PopupClient(
                Loc.GetString("cmu-xeno-spore-sac-too-far"),
                xeno,
                xeno);

            return;
        }

        var coords = args.Target.SnapToGrid(EntityManager);
        if (!CanPlaceAt(xeno, coords))
        {
            _popup.PopupClient(Loc.GetString("cmu-xeno-spore-sac-blocked"), xeno, xeno);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        xeno.Comp.PlacedSacs.RemoveAll(s => Deleted(s));
        xeno.Comp.PendingCoords = coords;

        args.Handled = true;

        var doAfter = new DoAfterArgs(
            EntityManager,
            xeno.Owner,
            xeno.Comp.PlaceDelay,
            new CMUXenoPlaceSporeSacDoAfterEvent(),
            xeno.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter);

    }

    private void OnPlaceFinished(
        Entity<CMUXenoSporeSacComponent> xeno,
        ref CMUXenoPlaceSporeSacDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        xeno.Comp.PlacedSacs.RemoveAll(uid => Deleted(uid));

        if (xeno.Comp.PlacedSacs.Count >= xeno.Comp.MaxSacs)
        {
            _popup.PopupClient(
                Loc.GetString("cmu-xeno-spore-sac-max"),
                xeno,
                xeno);

            return;
        }

        // something could have been built on the tile during the doafter
        if (xeno.Comp.PendingCoords is { } pending && !CanPlaceAt(xeno, pending))
        {
            xeno.Comp.PendingCoords = null;
            _popup.PopupClient(Loc.GetString("cmu-xeno-spore-sac-blocked"), xeno, xeno);
            return;
        }

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        if (xeno.Comp.PendingCoords is not { } coords)
            return;

        xeno.Comp.PendingCoords = null;

        args.Handled = true;

        if (_net.IsServer)
        {
            var sac = Spawn(xeno.Comp.SacPrototype, coords);

            if (TryComp(sac, out CMUPathogenSporeSacComponent? comp))
                comp.Placer = xeno.Owner;

            xeno.Comp.PlacedSacs.Add(sac);
        }

        _popup.PopupPredicted(
            Loc.GetString("cmu-xeno-spore-sac-place-self"),
            Loc.GetString("cmu-xeno-spore-sac-place-others", ("xeno", xeno.Owner)),
            xeno,
            xeno);
    }

    /// <summary>
    /// Needs a real floor tile with nothing solid on it, and a clear line from the popper to it,
    /// so no sacs through walls, windows, cades or out into space.
    /// </summary>
    private bool CanPlaceAt(Entity<CMUXenoSporeSacComponent> xeno, EntityCoordinates coords)
    {
        if (!_turf.TryGetTileRef(coords, out var tile) || tile.Value.Tile.IsEmpty)
            return false;

        if (_rmcMap.IsTileBlocked(coords))
            return false;

        return _interaction.InRangeUnobstructed(xeno.Owner, coords, xeno.Comp.Range);
    }
}
