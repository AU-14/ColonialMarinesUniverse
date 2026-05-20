using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.Inventory;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server._RMC14.Overwatch;

public sealed partial class OverwatchConsoleSystem : SharedOverwatchConsoleSystem
{
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<OverwatchCameraComponent> _cameraQuery;

    private readonly Dictionary<ICommonSession, ICChatRecipientData> _recipients = new();

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _cameraQuery = GetEntityQuery<OverwatchCameraComponent>();

        SubscribeLocalEvent<OverwatchCameraComponent, ComponentRemove>(OnWatchedRemove);
        SubscribeLocalEvent<OverwatchCameraComponent, EntityTerminatingEvent>(OnWatchedRemove);
        SubscribeLocalEvent<OverwatchWatchingComponent, ComponentRemove>(OnWatchingRemove);
        SubscribeLocalEvent<OverwatchWatchingComponent, EntityTerminatingEvent>(OnWatchingRemove);

        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        if (!_inventory.TryGetInventoryEntity<OverwatchCameraComponent>(ev.Source, out var camera))
            return;

        if (!_cameraQuery.TryComp(camera, out var cameraComp) || cameraComp.Watching.Count == 0)
            return;

        var sourceTransform = Transform(ev.Source);
        var sourcePos = _transform.GetWorldPosition(sourceTransform);

        _recipients.Clear();
        foreach (var watcher in cameraComp.Watching)
        {
            if (!_actorQuery.TryComp(watcher, out var actor))
                continue;

            if (ev.Recipients.ContainsKey(actor.PlayerSession))
                continue;

            var watcherTransform = Transform(watcher);
            float range;
            if (watcherTransform.MapID == sourceTransform.MapID)
                range = (sourcePos - _transform.GetWorldPosition(watcherTransform)).Length();
            else
                range = -1;

            _recipients.TryAdd(actor.PlayerSession, new ICChatRecipientData(range, false, false));
        }

        foreach (var recipient in _recipients)
        {
            ev.Recipients.TryAdd(recipient.Key, recipient.Value);
        }
    }

    private void OnWatchedRemove<T>(Entity<OverwatchCameraComponent> ent, ref T args)
    {
        foreach (var watching in ent.Comp.Watching)
        {
            if (TerminatingOrDeleted(watching))
                continue;

            RemCompDeferred<OverwatchWatchingComponent>(watching);
        }
    }

    private void OnWatchingRemove<T>(Entity<OverwatchWatchingComponent> ent, ref T args)
    {
        RemoveWatcher(ent);
    }

    protected override void Watch(Entity<ActorComponent?, EyeComponent?> watcher, Entity<OverwatchCameraComponent?> toWatch)
    {
        base.Watch(watcher, toWatch);

        if (!Resolve(toWatch, ref toWatch.Comp, false))
            return;

        if (watcher.Owner == toWatch.Owner)
            return;

        if (!Resolve(watcher, ref watcher.Comp1, ref watcher.Comp2) ||
            !Resolve(toWatch, ref toWatch.Comp))
        {
            return;
        }

        _eye.SetTarget(watcher, toWatch, watcher);
        _viewSubscriber.AddViewSubscriber(toWatch, watcher.Comp1.PlayerSession);

        RemoveWatcher(watcher);
        EnsureComp<OverwatchWatchingComponent>(watcher).Watching = toWatch;
        toWatch.Comp.Watching.Add(watcher);
    }

    protected override void Unwatch(Entity<EyeComponent?> watcher, ICommonSession player)
    {
        if (!Resolve(watcher, ref watcher.Comp))
            return;

        var oldTarget = watcher.Comp.Target;

        base.Unwatch(watcher, player);

        if (oldTarget != null && oldTarget != watcher.Owner)
            _viewSubscriber.RemoveViewSubscriber(oldTarget.Value, player);

        RemoveWatcher(watcher);
    }

    private void RemoveWatcher(EntityUid toRemove)
    {
        if (!TryComp(toRemove, out OverwatchWatchingComponent? watching))
            return;

        if (TryComp(watching.Watching, out OverwatchCameraComponent? watched))
            watched.Watching.Remove(toRemove);

        watching.Watching = null;
        RemCompDeferred<OverwatchWatchingComponent>(toRemove);
    }
}
