using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Popups;
using Content.Shared._RMC14.Actions;
using Content.Shared.DoAfter;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.SporeSac;

/// <summary>
/// Handles the Popper's ability to place a Spore Sac structure at a target
/// tile. Owns CMUXenoSporeSacComponent (the ability/caster side), distinct
/// from CMUPathogenSporeSacSystem which owns the placed sac itself.
/// </summary>
public sealed partial class CMUXenoSporeSacSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoSporeSacComponent, CMUXenoSporeSacActionEvent>(OnAction);
        SubscribeLocalEvent<CMUXenoSporeSacComponent, CMUXenoPlaceSporeSacDoAfterEvent>(OnPlaceFinished);
    }

    private void OnAction(Entity<CMUXenoSporeSacComponent> xeno, ref CMUXenoSporeSacActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        xeno.Comp.PlacedSacs.RemoveAll(s => Deleted(s));

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

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        var sac = Spawn(xeno.Comp.SacPrototype, Transform(xeno).Coordinates);

        if (TryComp(sac, out CMUPathogenSporeSacComponent? comp))
            comp.Placer = xeno.Owner;

        xeno.Comp.PlacedSacs.Add(sac);

        _popup.PopupPredicted(
            Loc.GetString("cmu-xeno-spore-sac-place-self"),
            Loc.GetString("cmu-xeno-spore-sac-place-others", ("xeno", xeno.Owner)),
            xeno,
            xeno);
    }
}