using Content.Shared.Throwing;

namespace Content.Server.CMU14.Throwing;

/// <summary>
/// Clears thrower references before deleted entities can reach component-state serialization.
/// </summary>
public sealed class CMUThrownItemReferenceSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _itemsByThrower = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThrownItemComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ThrownItemComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ThrownItemComponent, ThrownItemThrowerChangedEvent>(OnThrowerChanged);
        EntityManager.EntityDeleted += OnEntityDeleted;
    }

    public override void Shutdown()
    {
        EntityManager.EntityDeleted -= OnEntityDeleted;
        _itemsByThrower.Clear();
        base.Shutdown();
    }

    private void OnInit(Entity<ThrownItemComponent> ent, ref ComponentInit args) => Track(ent);

    private void OnShutdown(Entity<ThrownItemComponent> ent, ref ComponentShutdown args) => Untrack(ent, ent.Comp.Thrower);

    private void OnThrowerChanged(Entity<ThrownItemComponent> ent, ref ThrownItemThrowerChangedEvent args)
    {
        Untrack(ent, args.PreviousThrower);
        Track(ent);
    }

    private void Track(Entity<ThrownItemComponent> ent)
    {
        if (ent.Comp.Thrower is not { } thrower)
            return;

        if (TerminatingOrDeleted(thrower))
        {
            ent.Comp.Thrower = null;
            Dirty(ent);
            return;
        }

        if (!_itemsByThrower.TryGetValue(thrower, out var items))
            _itemsByThrower.Add(thrower, items = new HashSet<EntityUid>());
        items.Add(ent);
    }

    private void Untrack(EntityUid item, EntityUid? thrower)
    {
        if (thrower is not { } owner || !_itemsByThrower.TryGetValue(owner, out var items))
            return;

        items.Remove(item);
        if (items.Count == 0)
            _itemsByThrower.Remove(owner);
    }

    private void OnEntityDeleted(Entity<MetaDataComponent> ent)
    {
        if (!_itemsByThrower.Remove(ent, out var items))
            return;

        foreach (var uid in items)
        {
            if (!TryComp<ThrownItemComponent>(uid, out var thrown) || thrown.Thrower != ent.Owner)
                continue;

            thrown.Thrower = null;
            Dirty(uid, thrown);
        }
    }
}
