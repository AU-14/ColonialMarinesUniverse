using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared._RMC14.Xenonids.Fling;
using Content.Shared._RMC14.Xenonids.Headbite;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.Threats.Mobs.Ape;

public sealed class ApeXenoAdapterSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ApeChargeActionEvent>(OnApeChargeFromAction);
        SubscribeLocalEvent<ApeRamActionEvent>(OnApeRamFromAction);
        SubscribeLocalEvent<ApeXenoHeadbiteActionEvent>(OnApeHeadbiteFromAction);
    }

    private void OnApeChargeFromAction(ApeChargeActionEvent args)
    {
        if (args.Handled)
            return;

        EntityUid performer = args.Performer;
        if (performer == default(EntityUid))
            return;

        if (TryComp<XenoChargeComponent>(performer, out _))
        {
            var ev = new XenoChargeActionEvent
            {
                Action = args.Action,
                Performer = args.Performer,
                Target = args.Target,
                Entity = args.Entity,
                Toggle = args.Toggle
            };

            RaiseLocalEvent(performer, ev);
            args.Handled = ev.Handled;
        }
    }

    private void OnApeRamFromAction(ApeRamActionEvent args)
    {
        if (args.Handled)
            return;

        EntityUid performer = args.Performer;
        if (performer == default(EntityUid))
            return;

        if (_mobState.IsDead(args.Target) && TryComp<XenoFlingComponent>(performer, out var fling))
        {
            args.Handled = true;
            _rmcMelee.DoLunge(performer, args.Target);

            if (_net.IsServer)
            {
                _size.KnockBack(args.Target,
                    _transform.GetMapCoordinates(performer),
                    fling.Range,
                    fling.Range,
                    fling.ThrowSpeed);
            }

            return;
        }

        if (TryComp<XenoFlingComponent>(performer, out _))
        {
            var ev = new XenoFlingActionEvent
            {
                Action = args.Action,
                Performer = args.Performer,
                Target = args.Target,
                Toggle = args.Toggle
            };

            RaiseLocalEvent(performer, ev);
            args.Handled = ev.Handled;
        }
    }

    private void OnApeHeadbiteFromAction(ApeXenoHeadbiteActionEvent args)
    {
        if (args.Handled)
            return;

        EntityUid performer = args.Performer;
        if (performer == default(EntityUid))
            return;

        if (TryComp<XenoHeadbiteComponent>(performer, out _))
        {
            var ev = new XenoHeadbiteActionEvent
            {
                Action = args.Action,
                Performer = args.Performer,
                Target = args.Target
            };

            RaiseLocalEvent(performer, ev);
            args.Handled = ev.Handled;
        }
    }
}
