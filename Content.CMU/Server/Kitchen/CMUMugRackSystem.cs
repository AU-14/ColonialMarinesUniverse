using Content.Shared.CMU14.Kitchen;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Kitchen;

public sealed partial class CMUMugRackSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUMugRackComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUMugRackComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CMUMugRackComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnMapInit(Entity<CMUMugRackComponent> rack, ref MapInitEvent args)
    {
        UpdateAppearance(rack);
    }

    private void OnInteractHand(Entity<CMUMugRackComponent> rack, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (rack.Comp.Amount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-mug-rack-empty", ("rack", rack)), rack, args.User);
            return;
        }

        rack.Comp.Amount--;
        Dirty(rack);
        var mug = Spawn(rack.Comp.Mug, Transform(args.User).Coordinates);
        _hands.PickupOrDrop(args.User, mug);
        _popup.PopupEntity(Loc.GetString("cmu-mug-rack-take", ("mug", mug), ("rack", rack)), rack, args.User);
        UpdateAppearance(rack);
    }

    private void OnInteractUsing(Entity<CMUMugRackComponent> rack, ref InteractUsingEvent args)
    {
        if (args.Handled || MetaData(args.Used).EntityPrototype?.ID is not { } used || used != rack.Comp.Mug.Id)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-mug-rack-return", ("mug", args.Used), ("rack", rack)), rack, args.User);
        QueueDel(args.Used);
        rack.Comp.Amount++;
        Dirty(rack);
        UpdateAppearance(rack);
    }

    private void UpdateAppearance(Entity<CMUMugRackComponent> rack)
    {
        var amount = rack.Comp.Amount;
        var capacity = Math.Max(1, rack.Comp.Capacity);
        var fill = amount <= 0 ? CMUMugRackFill.Empty
            : amount < capacity / 3f ? CMUMugRackFill.Low
            : amount < 2 * capacity / 3f ? CMUMugRackFill.Half
            : CMUMugRackFill.Full;
        _appearance.SetData(rack, CMUMugRackVisuals.Fill, fill);
    }
}
