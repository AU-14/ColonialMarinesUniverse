using Content.Server.Actions;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.BuildOrders;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.Chat;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Text.RegularExpressions;
using Content.Shared.Mobs.Components;

namespace Content.Server.CMU14.BuildOrders;

/// <summary>
///     The commander's build orders: a menu to pick what to build, an action to mark where, and a see-through
///     blueprint everyone on their side can see. A blueprint goes away when the thing is built next to it, when
///     it times out, or when the commander clears them.
/// </summary>
public sealed class CMUBuildOrderSystem : EntitySystem
{
    private static readonly EntProtoId MarkerProto = "CMUBuildOrderMarker";
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(2);

    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ForceOnForceSystem _factions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    // "push north", "PUSH N", and so on, said anywhere in the sentence.
    private static readonly Regex PushOrder = new(@"\bpush\s+(north|south|east|west|n|s|e|w)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HashSet<EntityUid> _nearby = new();
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBuildOrderAbilityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUBuildOrderAbilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUBuildOrderAbilityComponent, CMUBuildMenuActionEvent>(OnMenuAction);
        SubscribeLocalEvent<CMUBuildOrderAbilityComponent, CMUBuildPlaceActionEvent>(OnPlaceAction);
        SubscribeLocalEvent<CMUBuildOrderAbilityComponent, CMUMarkActionEvent>(OnMarkAction);
        SubscribeLocalEvent<CMUBuildOrderAbilityComponent, EntitySpokeEvent>(OnSpoke);

        SubscribeNetworkEvent<CMUBuildSelectEvent>(OnSelect);
        SubscribeNetworkEvent<CMUBuildClearEvent>(OnClear);
        SubscribeNetworkEvent<CMUBuildGhostPlacedEvent>(OnGhostPlaced);
    }

    private void OnStartup(Entity<CMUBuildOrderAbilityComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.MenuActionEntity, ent.Comp.MenuAction);
        _actions.AddAction(ent, ref ent.Comp.PlaceActionEntity, ent.Comp.PlaceAction);
        _actions.AddAction(ent, ref ent.Comp.MarkActionEntity, ent.Comp.MarkAction);
    }

    private void OnShutdown(Entity<CMUBuildOrderAbilityComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.MenuActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.PlaceActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.MarkActionEntity);
    }

    private void OnMenuAction(Entity<CMUBuildOrderAbilityComponent> ent, ref CMUBuildMenuActionEvent args)
    {
        if (args.Handled || !TryComp(ent, out ActorComponent? actor))
            return;

        args.Handled = true;

        var options = new List<string>();
        foreach (var option in ent.Comp.Options)
        {
            options.Add(option.Id);
        }

        // Also step to the next option, so something can be picked even if the window does not show.
        if (ent.Comp.Options.Count > 0)
        {
            var index = ent.Comp.Selected is { } current ? ent.Comp.Options.IndexOf(current) : -1;
            ent.Comp.Selected = ent.Comp.Options[(index + 1) % ent.Comp.Options.Count];
            Dirty(ent);

            var picked = _prototypes.TryIndex<EntityPrototype>(ent.Comp.Selected.Value, out var pickedProto)
                ? pickedProto.Name
                : ent.Comp.Selected.Value.Id;
            _popup.PopupEntity(Loc.GetString("cmu-build-order-selected", ("name", picked)), ent, ent);
        }

        Log.Info($"Build menu requested by {ToPrettyString(ent)} with {options.Count} options");
        RaiseNetworkEvent(new CMUBuildMenuOpenEvent(options, ent.Comp.Selected?.Id), actor.PlayerSession);
    }

    private void OnSelect(CMUBuildSelectEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryComp(user, out CMUBuildOrderAbilityComponent? ability) ||
            !ability.Options.Contains(msg.Proto))
        {
            return;
        }

        ability.Selected = msg.Proto;
        Dirty(user, ability);

        var name = _prototypes.TryIndex<EntityPrototype>(msg.Proto, out var proto) ? proto.Name : msg.Proto;
        _popup.PopupEntity(Loc.GetString("cmu-build-order-selected", ("name", name)), user, user);
    }

    private void OnClear(CMUBuildClearEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !HasComp<CMUBuildOrderAbilityComponent>(user))
            return;

        var markers = EntityQueryEnumerator<CMUBuildOrderMarkerComponent>();
        while (markers.MoveNext(out var uid, out var marker))
        {
            if (marker.Owner == user)
                QueueDel(uid);
        }

        _popup.PopupEntity(Loc.GetString("cmu-build-order-cleared"), user, user);
    }

    private void OnPlaceAction(Entity<CMUBuildOrderAbilityComponent> ent, ref CMUBuildPlaceActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = ent.Owner;

        if (_mobState.IsIncapacitated(user))
            return;

        if (ent.Comp.Selected is not { } selected)
        {
            _popup.PopupEntity(Loc.GetString("cmu-build-order-nothing-selected"), user, user);
            return;
        }

        PlaceBlueprint(ent, selected, args.Target);
    }

    // The CO placed a ghost from the game's own construction menu: show it to their side as a blueprint.
    private void OnGhostPlaced(CMUBuildGhostPlacedEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryComp(user, out CMUBuildOrderAbilityComponent? ability) ||
            _mobState.IsIncapacitated(user) ||
            !_prototypes.HasIndex<EntityPrototype>(msg.Proto))
        {
            return;
        }

        PlaceBlueprint((user, ability), msg.Proto, GetCoordinates(msg.Coordinates));
    }

    private void PlaceBlueprint(Entity<CMUBuildOrderAbilityComponent> ent, EntProtoId selected, EntityCoordinates target)
    {
        var user = ent.Owner;
        if (!target.IsValid(EntityManager) ||
            !_transform.InRange(Transform(user).Coordinates, target, ent.Comp.Range))
        {
            _popup.PopupEntity(Loc.GetString("cmu-build-order-too-far"), user, user);
            return;
        }

        // One blueprint per tile, so they do not pile up on top of each other.
        var snapped = SnapToTile(target);
        var markers = EntityQueryEnumerator<CMUBuildOrderMarkerComponent, TransformComponent>();
        var mine = new List<(EntityUid Uid, TimeSpan ExpireAt)>();
        while (markers.MoveNext(out var uid, out var existing, out var xform))
        {
            if (existing.Owner != user)
                continue;

            if (xform.Coordinates.TryDistance(EntityManager, snapped, out var distance) && distance < 0.5f)
            {
                QueueDel(uid);
                continue;
            }

            mine.Add((uid, existing.ExpireAt));
        }

        if (mine.Count >= ent.Comp.MaxOrders)
        {
            mine.Sort((a, b) => a.ExpireAt.CompareTo(b.ExpireAt));
            QueueDel(mine[0].Uid);
        }

        var faction = _factions.GetFaction(user);
        var marker = Spawn(MarkerProto, snapped);
        var comp = EnsureComp<CMUBuildOrderMarkerComponent>(marker);
        comp.Order = selected;
        comp.Faction = faction;
        comp.Owner = user;
        comp.ExpireAt = _timing.CurTime + ent.Comp.Lifetime;
        Dirty(marker, comp);

        var name = _prototypes.TryIndex<EntityPrototype>(selected, out var proto) ? proto.Name : selected.Id;
        var notice = Loc.GetString("cmu-build-order-notice", ("name", name));
        var color = new Color(0.45f, 0.8f, 1f, 1f);
        var marines = EntityQueryEnumerator<MarineComponent, ActorComponent>();
        while (marines.MoveNext(out var marineUid, out _, out _))
        {
            if (_factions.GetFaction(marineUid) == faction)
                _cmChat.ChatMessageToOne(notice, marineUid, ChatChannel.Local, colorOverride: color);
        }
    }

    // Highlight someone for a few seconds, for everyone on the commander's side.
    private void OnMarkAction(Entity<CMUBuildOrderAbilityComponent> ent, ref CMUMarkActionEvent args)
    {
        if (args.Handled || args.Target == ent.Owner || !HasComp<MobStateComponent>(args.Target))
            return;

        args.Handled = true;

        if (!_transform.InRange(ent.Owner, args.Target, ent.Comp.MarkRange))
        {
            _popup.PopupEntity(Loc.GetString("cmu-build-order-too-far"), ent, ent);
            return;
        }

        var marked = EnsureComp<CMUMarkedComponent>(args.Target);
        marked.Faction = _factions.GetFaction(ent);
        marked.ExpireAt = _timing.CurTime + ent.Comp.MarkDuration;
        Dirty(args.Target, marked);

        var notice = Loc.GetString("cmu-mark-notice", ("name", Name(args.Target)));
        SendToSide(ent.Owner, marked.Faction, ent.Comp.MarkRange, notice, new Color(1f, 0.35f, 0.35f, 1f));
    }

    // When the commander says PUSH and a cardinal direction, everyone on their side gets an arrow pointing that way.
    private void OnSpoke(EntityUid uid, CMUBuildOrderAbilityComponent ability, EntitySpokeEvent args)
    {
        var match = PushOrder.Match(args.Message);
        if (!match.Success)
            return;

        var direction = match.Groups[1].Value.ToLowerInvariant() switch
        {
            "north" or "n" => CMUPushDirection.North,
            "east" or "e" => CMUPushDirection.East,
            "south" or "s" => CMUPushDirection.South,
            _ => CMUPushDirection.West,
        };

        var faction = _factions.GetFaction(uid);
        var expire = _timing.CurTime + ability.PushDuration;
        var marines = EntityQueryEnumerator<MarineComponent, ActorComponent>();
        while (marines.MoveNext(out var marineUid, out _, out _))
        {
            if (_factions.GetFaction(marineUid) != faction ||
                !_transform.InRange(uid, marineUid, ability.PushRange))
            {
                continue;
            }

            var arrow = EnsureComp<CMUPushArrowComponent>(marineUid);
            arrow.Direction = direction;
            arrow.ExpireAt = expire;
            Dirty(marineUid, arrow);
        }

        SendToSide(uid, faction, ability.PushRange, Loc.GetString("cmu-push-notice", ("direction", direction.ToString().ToUpperInvariant())),
            new Color(1f, 0.8f, 0.2f, 1f));
    }

    private void SendToSide(EntityUid origin, string? faction, float range, string message, Color color)
    {
        var marines = EntityQueryEnumerator<MarineComponent, ActorComponent>();
        while (marines.MoveNext(out var marineUid, out _, out _))
        {
            if (_factions.GetFaction(marineUid) == faction && _transform.InRange(origin, marineUid, range))
                _cmChat.ChatMessageToOne(message, marineUid, ChatChannel.Local, colorOverride: color);
        }
    }

    private void ExpireTemporary(TimeSpan time)
    {
        var marked = EntityQueryEnumerator<CMUMarkedComponent>();
        while (marked.MoveNext(out var uid, out var comp))
        {
            if (time > comp.ExpireAt)
                RemCompDeferred<CMUMarkedComponent>(uid);
        }

        var arrows = EntityQueryEnumerator<CMUPushArrowComponent>();
        while (arrows.MoveNext(out var uid, out var comp))
        {
            if (time > comp.ExpireAt)
                RemCompDeferred<CMUPushArrowComponent>(uid);
        }
    }

    private EntityCoordinates SnapToTile(EntityCoordinates coordinates)
    {
        var position = coordinates.Position;
        return new EntityCoordinates(coordinates.EntityId, MathF.Floor(position.X) + 0.5f, MathF.Floor(position.Y) + 0.5f);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        ExpireTemporary(time);

        if (time < _nextCheck)
            return;

        _nextCheck = time + CheckEvery;

        var markers = EntityQueryEnumerator<CMUBuildOrderMarkerComponent, TransformComponent>();
        while (markers.MoveNext(out var uid, out var marker, out var xform))
        {
            if (time > marker.ExpireAt || marker.Order is not { } order || IsBuilt(uid, xform, order))
                QueueDel(uid);
        }
    }

    // It counts as built once something of the ordered kind is standing on the spot.
    private bool IsBuilt(EntityUid marker, TransformComponent xform, EntProtoId order)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, 0.6f, _nearby);

        foreach (var found in _nearby)
        {
            if (found != marker && MetaData(found).EntityPrototype?.ID == order.Id)
                return true;
        }

        return false;
    }
}
