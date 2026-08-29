using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Smokeables;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Trigger.Components.Triggers;

namespace Content.Shared.Trigger.Systems;

/// <summary>
/// Handles triggers that require the user to hold an active lighter or blowtorch.
/// </summary>
public sealed class TriggerOnIgniterUseSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnIgniterUseComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<TriggerOnIgniterUseComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<HandsComponent>(args.User, out var hands))
            return;

        foreach (var held in _hands.EnumerateHeld((args.User, hands)))
        {
            if (!HasComp<RMCLighterComponent>(held) && !HasComp<BlowtorchComponent>(held))
                continue;

            if (!TryComp<ItemToggleComponent>(held, out var toggle) || !toggle.Activated)
                continue;

            args.Handled = _trigger.Trigger(ent.Owner, args.User, ent.Comp.KeyOut);
            return;
        }
    }
}
