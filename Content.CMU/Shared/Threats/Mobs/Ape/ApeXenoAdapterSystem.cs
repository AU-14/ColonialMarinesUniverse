using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared._RMC14.Xenonids.Fling;
using Content.Shared._RMC14.Xenonids.Headbite;

namespace Content.Shared._CMU14.Threats.Mobs.Ape;


public sealed class ApeXenoAdapterSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<Threats.Mobs.Ape.ApeChargeActionEvent>(OnApeChargeFromAction);
        SubscribeLocalEvent<Threats.Mobs.Ape.ApeRamActionEvent>(OnApeRamFromAction);
        SubscribeLocalEvent<Threats.Mobs.Ape.ApeXenoHeadbiteActionEvent>(OnApeHeadbiteFromAction);
    }

    private void OnApeChargeFromAction(Threats.Mobs.Ape.ApeChargeActionEvent args)
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

    private void OnApeRamFromAction(Threats.Mobs.Ape.ApeRamActionEvent args)
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

    private void OnApeHeadbiteFromAction(Threats.Mobs.Ape.ApeXenoHeadbiteActionEvent args)
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



