using Content.Shared.Actions;

namespace Content.Shared.CMU14.Radio;

/// <summary>Grants the phone button to whoever wears an ANPRC set. The button's behaviour lives on the server.</summary>
public sealed partial class ANPRCPhoneActionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ANPRCPhoneComponent, GetItemActionsEvent>(OnGetItemActions);
    }

    private void OnGetItemActions(Entity<ANPRCPhoneComponent> ent, ref GetItemActionsEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slot) == 0)
            return;

        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId, ent);
    }
}
