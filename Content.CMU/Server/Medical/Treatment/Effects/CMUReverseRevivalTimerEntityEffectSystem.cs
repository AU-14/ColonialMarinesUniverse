using Content.Shared.CMU14.Medical.Treatment.Effects;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;

namespace Content.Server.CMU14.Medical.Treatment.Effects;

public sealed partial class CMUReverseRevivalTimerEntityEffectSystem
    : EntityEffectSystem<RMCRevivableComponent, CMUReverseRevivalTimer>
{
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;

    protected override void Effect(Entity<RMCRevivableComponent> entity, ref EntityEffectEvent<CMUReverseRevivalTimer> args)
    {
        if (args.ReagentContext is not { Origin: ReagentEffectOrigin.Metabolism } context
            || context.Quantity.Quantity <= FixedPoint2.Zero)
            return;

        var seconds = args.Effect.SecondsPerUnit * (float) context.Quantity.Quantity;
        _unrevivable.ReverseRevivalTimer(entity.Owner, TimeSpan.FromSeconds(seconds));
    }
}
