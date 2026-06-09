using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Rules;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server._RMC14.Overwatch;

public sealed partial class OverwatchConsoleSystem : SharedOverwatchConsoleSystem
{
    [Dependency] private CommunicationsTowerSystem _communicationsTower = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OverwatchCameraComponent, ComponentRemove>(OnWatchedRemove);
        SubscribeLocalEvent<OverwatchCameraComponent, EntityTerminatingEvent>(OnWatchedRemove);
        SubscribeLocalEvent<OverwatchWatchingComponent, ComponentRemove>(OnWatchingRemove);
        SubscribeLocalEvent<OverwatchWatchingComponent, EntityTerminatingEvent>(OnWatchingRemove);

        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        var sourceCoordinates = Transform(ev.Source).Coordinates;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } watcher)
                continue;

            if (!TryComp(watcher, out OverwatchWatchingComponent? overwatch) ||
                overwatch.Watching is not { } target)
                continue;

            var targetCoordinates = _transform.GetMoverCoordinates(target);
            var targetMap = _transform.GetMap(targetCoordinates);
            var watcherMap = _transform.GetMap(_transform.GetMoverCoordinates(watcher));
            if (HasComp<RMCPlanetComponent>(targetMap) &&
                targetMap != watcherMap &&
                !_communicationsTower.CanTransmit())
            {
                continue;
            }

            if (!targetCoordinates.TryDistance(EntityManager, sourceCoordinates, out var distance))
                continue;

            if (distance > ev.VoiceRange)
                continue;

            ev.Recipients.TryAdd(session, new ICChatRecipientData(distance, false));
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
