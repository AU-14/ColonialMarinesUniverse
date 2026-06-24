using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared._RMC14.Xenonids.Fling;
using Content.Shared._RMC14.Xenonids.Headbite;

namespace Content.Shared._CMU14.Ape;


public sealed class ApeXenoAdapterSystem : EntitySystem
{
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

        var performer = args.Performer;
        if (performer == default)
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

        var performer = args.Performer;
        if (performer == default)
            return;

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

        var performer = args.Performer;
        if (performer == default)
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



