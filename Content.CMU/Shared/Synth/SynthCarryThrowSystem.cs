using Content.Shared._RMC14.Fireman;
using Content.Shared.Hands;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Throwing;
using Content.Shared._RMC14.Synth;

namespace Content.Shared.CMU14.Synth;

public sealed partial class CMUSynthThrowSystem : EntitySystem
{
[Dependency] private FiremanCarrySystem _fireman = default!;

    private readonly HashSet<EntityUid> _restore = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<SynthComponent, BeforeThrowEvent>(OnBeforeThrow, before: [typeof(FiremanCarrySystem)]);
        SubscribeLocalEvent<SynthComponent, DidEquipHandEvent>(OnDidEquipHand);
    }

    private void OnDidEquipHand(Entity<SynthComponent> ent, ref DidEquipHandEvent args)
    {
        // ThrowHeldItem clamps to the held thing's ItemThrowRange before anyone else sees the throw, so the cap rides
        // on the carry's virtual item. 
        if (!TryComp(args.Equipped, out VirtualItemComponent? virtualItem) ||
            !HasComp<FiremanCarriableComponent>(virtualItem.BlockingEntity))
            return;

        EnsureComp<ItemThrowRangeComponent>(args.Equipped).Range = ent.Comp.CarriedThrowRange;
    }

    private void OnBeforeThrow(Entity<SynthComponent> ent, ref BeforeThrowEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.ThrowSpeedMultiplier > 1f)
            args.ThrowSpeed *= ent.Comp.ThrowSpeedMultiplier;

        // the carried mob is held as a virtual item, same unwrap fireman carry does
        var carried = TryComp(args.ItemUid, out VirtualItemComponent? virtualItem)
            ? virtualItem.BlockingEntity
            : args.ItemUid;

        // only for this one throw, a marine picking the same guy up later still can't toss him
        if (_fireman.TryAllowCarriedThrow(ent, carried))
            _restore.Add(carried);
    }

    public override void Update(float frameTime)
    {
        if (_restore.Count == 0)
            return;

        foreach (var carried in _restore)
        {
            if (!TerminatingOrDeleted(carried))
                _fireman.SetCarriedThrowable(carried, false);
        }

        _restore.Clear();
    }
}