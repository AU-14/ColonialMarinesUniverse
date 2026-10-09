using Content.Shared._RMC14.Fireman;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Throwing;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaCarryThrowSystem : EntitySystem
{
    [Dependency] private FiremanCarrySystem _fireman = default!;

    private readonly HashSet<EntityUid> _restore = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, BeforeThrowEvent>(OnBeforeThrow, before: [typeof(FiremanCarrySystem)]);
    }

    private void OnBeforeThrow(Entity<YautjaComponent> ent, ref BeforeThrowEvent args)
    {
        if (args.Cancelled)
            return;

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
