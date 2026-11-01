using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Storage;

public sealed partial class CMUDespawnWhenEmptySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUDespawnWhenEmptyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUDespawnWhenEmptyComponent, EntInsertedIntoContainerMessage>(OnContentsInserted);
        SubscribeLocalEvent<CMUDespawnWhenEmptyComponent, EntRemovedFromContainerMessage>(OnContentsRemoved);
        SubscribeLocalEvent<CMUDespawnWhenEmptyComponent, EntGotInsertedIntoContainerMessage>(OnGotInserted);
        SubscribeLocalEvent<CMUDespawnWhenEmptyComponent, EntGotRemovedFromContainerMessage>(OnGotRemoved);
    }

    private void OnMapInit(Entity<CMUDespawnWhenEmptyComponent> ent, ref MapInitEvent args)
    {
        Refresh(ent);
    }

    private void OnContentsInserted(Entity<CMUDespawnWhenEmptyComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == StorageComponent.ContainerId)
            Refresh(ent);
    }

    private void OnContentsRemoved(Entity<CMUDespawnWhenEmptyComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == StorageComponent.ContainerId)
            Refresh(ent);
    }

    private void OnGotInserted(Entity<CMUDespawnWhenEmptyComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        Refresh(ent);
    }

    private void OnGotRemoved(Entity<CMUDespawnWhenEmptyComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        Refresh(ent);
    }

    /// <summary>
    /// Starts the timer when the entity becomes empty and loose, and clears it otherwise,
    /// so picking the item up or putting something back in resets the full delay.
    /// </summary>
    private void Refresh(Entity<CMUDespawnWhenEmptyComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || !IsEmptyOnGround(ent))
        {
            ent.Comp.DespawnAt = null;
            return;
        }

        ent.Comp.DespawnAt ??= _timing.CurTime + ent.Comp.Delay;
    }

    private bool IsEmptyOnGround(EntityUid uid)
    {
        if (_container.IsEntityInContainer(uid))
            return false;

        if (Transform(uid).MapUid == null)
            return false;

        if (!_container.TryGetContainer(uid, StorageComponent.ContainerId, out var storage))
            return false;

        return storage.ContainedEntities.Count == 0;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUDespawnWhenEmptyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.DespawnAt is not { } despawnAt || time < despawnAt)
                continue;

            // Re-check in case the state changed through a path that raised no container event.
            if (!IsEmptyOnGround(uid))
            {
                comp.DespawnAt = null;
                continue;
            }

            QueueDel(uid);
        }
    }
}
