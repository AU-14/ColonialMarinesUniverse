using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Content.Shared.Stunnable;

namespace Content.Shared._CMU14.Xenonids.ParalyzingSlash;

public sealed partial class CMUXenoParalyzingSlashSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        // No component filter - any xeno with this action can trigger it
        SubscribeLocalEvent<CMUXenoParalyzingSlashActionEvent>(OnAction);
        SubscribeLocalEvent<CMUXenoParalyzingSlashPendingComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<CMUXenoParalyzingSlashPendingComponent, ComponentShutdown>(OnPendingShutdown);
    }

    private void OnAction(CMUXenoParalyzingSlashActionEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        // Toggle off if already armed
        if (RemComp<CMUXenoParalyzingSlashPendingComponent>(performer))
        {
            args.Handled = true;
            _popup.PopupClient(Loc.GetString("cmu-xeno-paralyzing-slash-cancel"), performer, performer, PopupType.Small);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        var pending = EnsureComp<CMUXenoParalyzingSlashPendingComponent>(performer);
        pending.SlowDuration = args.SlowDuration;
        pending.SuperSlow = args.SuperSlow;
        Dirty(performer, pending);

        _popup.PopupClient(Loc.GetString("cmu-xeno-paralyzing-slash-ready"), performer, performer, PopupType.MediumCaution);

        foreach (var action in _rmcActions.GetActionsWithEvent<CMUXenoParalyzingSlashActionEvent>(performer))
        {
            _actions.SetToggled(action.AsNullable(), true);
        }
    }

    private void OnPendingShutdown(Entity<CMUXenoParalyzingSlashPendingComponent> xeno, ref ComponentShutdown args)
    {
        foreach (var action in _rmcActions.GetActionsWithEvent<CMUXenoParalyzingSlashActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), false);
        }
    }

    private void OnMeleeHit(Entity<CMUXenoParalyzingSlashPendingComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var duration = xeno.Comp.SlowDuration;
        var superSlow = xeno.Comp.SuperSlow;
        var hitAnyone = false;

        foreach (var target in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, target) || HasComp<XenoComponent>(target))
                continue;

            if (HasComp<SynthComponent>(target))
            {
                var immuneMsg = Loc.GetString("cmu-xeno-paralyzing-slash-immune", ("target", target));
                _popup.PopupEntity(immuneMsg, target, target, PopupType.SmallCaution);
                continue;
            }

            if (superSlow)
                _slow.TrySuperSlowdown(target, duration);
            else
                _slow.TrySlowdown(target, duration);

            hitAnyone = true;

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("cmu-xeno-paralyzing-slash-hit", ("target", target)),
                    target, xeno, PopupType.MediumCaution);
            }
        }

        if (hitAnyone)
            RemCompDeferred<CMUXenoParalyzingSlashPendingComponent>(xeno);
    }
}