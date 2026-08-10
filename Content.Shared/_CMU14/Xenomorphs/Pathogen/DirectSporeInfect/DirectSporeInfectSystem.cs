using Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Inventory;
using Robust.Shared.Random;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.DirectSporeInfect;

public sealed partial class CMUXenoDirectSporeInfectSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMycotoxinSystem _mycotoxin = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoDirectSporeInfectComponent, CMUXenoDirectSporeInfectActionEvent>(OnAction);
        SubscribeLocalEvent<CMUXenoDirectSporeInfectComponent, CMUDirectSporeInfectDoAfterEvent>(OnDoAfter);
    }

    private void OnAction(
        Entity<CMUXenoDirectSporeInfectComponent> xeno,
        ref CMUXenoDirectSporeInfectActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        var target = args.Entity;

        if (target == null || TerminatingOrDeleted(target.Value))
            return;

        args.Handled = true;

        if (!_xeno.CanAbilityAttackTarget(xeno, target.Value))
        {
            _popup.PopupClient(
                Loc.GetString("cmu-xeno-direct-spore-infect-invalid"),
                xeno,
                xeno);
            return;
        }

        if (_mobState.IsDead(target.Value))
        {
            _popup.PopupClient(
                Loc.GetString("cmu-xeno-direct-spore-infect-dead"),
                xeno,
                xeno);
            return;
        }

        if (HasComp<CMUPathogenWalkerComponent>(target.Value))
        {
            _popup.PopupClient(
                Loc.GetString("cmu-xeno-direct-spore-infect-invalid"),
                xeno,
                xeno);
            return;
        }

        if (!HasComp<InfectableComponent>(target.Value))
        {
            _popup.PopupClient(
                Loc.GetString("cmu-xeno-direct-spore-infect-invalid"),
                xeno,
                xeno);
            return;
        }

        if (HasComp<VictimInfectedComponent>(target.Value))
        {
            _popup.PopupClient(
                Loc.GetString("cmu-xeno-direct-spore-infect-already"),
                xeno,
                xeno);
            return;
        }

        var ev = new CMUDirectSporeInfectDoAfterEvent();

        var doAfter = new DoAfterArgs(
            EntityManager,
            xeno,
            xeno.Comp.InfectDelay,
            ev,
            xeno)
        {
            Target = target.Value,
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RangeCheck = true,
        };

        _doAfter.TryStartDoAfter(doAfter);

        _popup.PopupPredicted(
            Loc.GetString("cmu-xeno-direct-spore-infect-hit", ("target", target.Value)),
            Loc.GetString("cmu-xeno-direct-spore-infect-hit", ("target", target.Value)),
            xeno,
            xeno);
    }

    private void OnDoAfter(
        Entity<CMUXenoDirectSporeInfectComponent> xeno,
        ref CMUDirectSporeInfectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not { } target)
            return;

        if (TerminatingOrDeleted(target))
            return;

        if (!_xeno.CanAbilityAttackTarget(xeno, target))
            return;

        if (_mobState.IsDead(target))
            return;

        if (HasComp<VictimInfectedComponent>(target))
            return;

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        var sourceHive = _hive.GetHive(xeno.Owner)?.Owner;

        EntityUid? protItem = null;
        foreach (var slot in new[] { "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(target, slot, out var item))
                continue;
            if (!TryComp(item, out MycotoxinProtectionComponent? _))
                continue;
            protItem = item;
            break;
        }

        if (protItem != null && !_random.Prob(0.1f))
        {
            _popup.PopupClient(
                Loc.GetString("cmu-xeno-direct-spore-infect-blocked"),
                xeno, xeno, PopupType.SmallCaution);
            return;
        }

        // Strip the protective item on success
        if (protItem != null)
        {
            foreach (var slot in new[] { "mask", "head" })
            {
                if (_inventory.TryGetSlotEntity(target, slot, out var slotItem) && slotItem == protItem)
                {
                    _inventory.TryUnequip(target, slot, force: true);
                    break;
                }
            }
        }

        _popup.PopupPredicted(
            Loc.GetString("cmu-xeno-direct-spore-infect-hit", ("target", target)),
            Loc.GetString("cmu-xeno-direct-spore-infect-hit", ("target", target)),
            xeno,
            xeno);

        args.Handled = true;
    }
}