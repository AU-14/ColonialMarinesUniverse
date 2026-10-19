using Content.Shared._RMC14.Tracker;
using Content.Shared.Alert;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Scent;
using Content.Shared.Hands;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Threats.Mobs.Wendigo.Scent;

/// <summary>
/// Wendigo scent tracking. Catch Scent on a living mob, or on an item someone held, tracks that mob for a while:
/// a tracker arrow alert points at it and its recent positions are synced to the Wendigo for the client smell trail.
/// Also records the last hand that picked up any item, which is what an item's scent leads to.
/// </summary>
public sealed partial class CMUWendigoScentSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TrackerSystem _tracker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Raised on the item; every pickup-able item has ItemComponent, so this covers ordinary items without prototype changes.
        SubscribeLocalEvent<ItemComponent, GotEquippedHandEvent>(OnItemEquippedHand);

        SubscribeLocalEvent<CMUWendigoScentTrackerComponent, CMUWendigoScentActionEvent>(OnScentAction);
        SubscribeLocalEvent<CMUWendigoScentTrackerComponent, ComponentShutdown>(OnTrackerShutdown);
    }

    private void OnItemEquippedHand(Entity<ItemComponent> ent, ref GotEquippedHandEvent args)
    {
        if (HasComp<VirtualItemComponent>(ent))
            return;

        var holder = EnsureComp<CMULastHolderComponent>(ent);
        holder.Holder = args.User;
        holder.At = _timing.CurTime;
    }

    private void OnScentAction(Entity<CMUWendigoScentTrackerComponent> ent, ref CMUWendigoScentActionEvent args)
    {
        if (args.Handled)
            return;

        // Only a caught scent spends the cooldown.
        if (TryTrack(ent, args.Target))
            args.Handled = true;
    }

    private void OnTrackerShutdown(Entity<CMUWendigoScentTrackerComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _alerts.ClearAlertCategory(ent.Owner, ent.Comp.AlertCategory);
    }

    /// <summary>
    /// The mob whose scent <paramref name="sniffed"/> carries: a living mob is its own scent, an item leads to
    /// the last entity that held it. Null when there is no scent to follow.
    /// </summary>
    public EntityUid? GetScentSource(EntityUid sniffed)
    {
        if (TerminatingOrDeleted(sniffed))
            return null;

        if (HasComp<MobStateComponent>(sniffed))
            return _mobState.IsDead(sniffed) ? null : sniffed;

        if (TryComp(sniffed, out CMULastHolderComponent? lastHolder) &&
            lastHolder.Holder is { } holder &&
            !TerminatingOrDeleted(holder))
        {
            return holder;
        }

        return null;
    }

    /// <summary>
    /// Makes <paramref name="wendigo"/> track the scent on <paramref name="sniffed"/>, replacing any previous target.
    /// Adds the tracker component if missing. Returns false, with a popup, when there is no scent to follow.
    /// </summary>
    public bool TryTrack(EntityUid wendigo, EntityUid sniffed)
    {
        var target = GetScentSource(sniffed);
        if (target == null || target == wendigo)
        {
            _popup.PopupEntity(Loc.GetString("cmu-wendigo-scent-none",
                    ("target", Identity.Entity(sniffed, EntityManager, wendigo))),
                wendigo,
                wendigo,
                PopupType.SmallCaution);
            return false;
        }

        var tracker = EnsureComp<CMUWendigoScentTrackerComponent>(wendigo);
        var time = _timing.CurTime;
        tracker.Target = target;
        tracker.TrackUntil = time + tracker.Duration;
        tracker.NextUpdate = time; // sample and show the arrow on the next update
        tracker.Trail.Clear();
        Dirty(wendigo, tracker);

        _popup.PopupEntity(Loc.GetString("cmu-wendigo-scent-caught",
                ("target", Identity.Entity(target.Value, EntityManager, wendigo))),
            wendigo,
            wendigo,
            PopupType.Medium);
        return true;
    }

    /// <summary>Ends tracking: clears the target, the trail and the arrow alert.</summary>
    public void StopTracking(Entity<CMUWendigoScentTrackerComponent> ent, bool popup = false)
    {
        if (ent.Comp.Target == null && ent.Comp.Trail.Count == 0)
            return;

        ent.Comp.Target = null;
        ent.Comp.Trail.Clear();
        Dirty(ent);

        _alerts.ClearAlertCategory(ent.Owner, ent.Comp.AlertCategory);

        if (popup)
            _popup.PopupEntity(Loc.GetString("cmu-wendigo-scent-lost"), ent, ent, PopupType.SmallCaution);
    }

    private void AddTrailPoint(CMUWendigoScentTrackerComponent tracker, MapCoordinates coordinates, TimeSpan time)
    {
        var trail = tracker.Trail;
        if (trail.Count > 0)
        {
            var last = trail[^1].Coordinates;
            if (last.MapId == coordinates.MapId &&
                (last.Position - coordinates.Position).Length() < tracker.MinPointSpacing)
            {
                // Standing still: keep the newest point fresh instead of stacking duplicates.
                trail[^1] = new CMUWendigoScentPoint(last, time);
                return;
            }
        }

        trail.Add(new CMUWendigoScentPoint(coordinates, time));
        var excess = trail.Count - Math.Max(1, tracker.MaxTrailPoints);
        if (excess > 0)
            trail.RemoveRange(0, excess);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUWendigoScentTrackerComponent>();
        while (query.MoveNext(out var uid, out var tracker))
        {
            if (tracker.Target is not { } target || time < tracker.NextUpdate)
                continue;

            tracker.NextUpdate = time + tracker.UpdateEvery;

            if (TerminatingOrDeleted(target) || time >= tracker.TrackUntil)
            {
                StopTracking((uid, tracker), popup: true);
                continue;
            }

            var coordinates = _transform.GetMapCoordinates(target);
            if (coordinates.MapId == MapId.Nullspace)
                continue;

            AddTrailPoint(tracker, coordinates, time);
            Dirty(uid, tracker);

            _alerts.ShowAlert(uid, tracker.Alert, _tracker.GetAlertSeverity(uid, coordinates));
        }
    }
}
