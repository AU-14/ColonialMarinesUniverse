using System.Linq;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaMarkSystem : EntitySystem
{
    private const int MaxReasonLength = 120;
    private const int HistoryPageSize = 50;
    private static readonly TimeSpan ObservationInterval = TimeSpan.FromSeconds(0.5);

    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private YautjaPowerSystem _power = default!;

    private TimeSpan _nextObservation;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaOpenMarkPanelActionEvent>(OnOpenMarkPanel);
        SubscribeLocalEvent<YautjaBracerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<YautjaComponent, ComponentRemove>(OnYautjaRemoved);
        SubscribeLocalEvent<YautjaMarkComponent, MobStateChangedEvent>(OnMarkedMobStateChanged);
        SubscribeLocalEvent<NewXenoEvolvedEvent>(OnNewXenoEvolved);
        SubscribeLocalEvent<XenoDevolvedEvent>(OnXenoDevolved);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);

        Subs.BuiEvents<YautjaBracerComponent>(YautjaMarkUIKey.Key, subs =>
        {
            subs.Event<YautjaMarkPanelRefreshMsg>(OnRefreshMsg);
            subs.Event<YautjaMarkPanelMarkMsg>(OnMarkMsg);
            subs.Event<YautjaMarkPanelUnmarkMsg>(OnUnmarkMsg);
            subs.Event<YautjaMarkPanelChangeMsg>(OnChangeMsg);
        });
    }

    private void OnNewXenoEvolved(ref NewXenoEvolvedEvent args)
    {
        TransferXenoIdentity(args.OldXeno, args.NewXeno);
    }

    private void OnXenoDevolved(ref XenoDevolvedEvent args)
    {
        TransferXenoIdentity(args.OldXeno, args.NewXeno);
    }

    private void TransferXenoIdentity(EntityUid oldXeno, EntityUid newXeno)
    {
        if (_net.IsClient || oldXeno == newXeno)
            return;

        if (TryComp(oldXeno, out YautjaMarkComponent? oldMarks))
        {
            var newMarks = EnsureComp<YautjaMarkComponent>(newXeno);
            foreach (var (kind, hunter) in oldMarks.Marks)
                newMarks.Marks[kind] = hunter;

            Dirty(newXeno, newMarks);
            EnsureComp<StatusIconComponent>(newXeno);
            RemComp<YautjaMarkComponent>(oldXeno);
        }

        var query = EntityQueryEnumerator<YautjaHuntJournalComponent>();
        while (query.MoveNext(out var hunter, out var journal))
        {
            if (!journal.Targets.Remove(oldXeno, out var recordId) ||
                !journal.Records.TryGetValue(recordId, out var record))
                continue;

            journal.Targets[newXeno] = recordId;
            record.Target = newXeno;
            record.Name = Name(newXeno);
            if (journal.Visible.Remove(oldXeno))
                journal.Visible.Add(newXeno);
            SnapshotMarks(hunter, newXeno, record);
            journal.Revision++;
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient || _timing.CurTime < _nextObservation)
            return;

        _nextObservation = _timing.CurTime + ObservationInterval;
        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var hunter, out _))
        {
            if (_mob.IsDead(hunter))
                continue;

            var journal = EnsureComp<YautjaHuntJournalComponent>(hunter);
            if (!Observe(hunter, journal))
                continue;

            if (_power.TryGetWornBracer(hunter, out var bracer) &&
                _ui.IsUiOpen(bracer.Owner, YautjaMarkUIKey.Key, hunter))
                UpdateUi(bracer, hunter);
        }
    }

    private void OnYautjaRemoved(Entity<YautjaComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsClient)
            return;
        ClearHunterMarks(ent.Owner);
        RemCompDeferred<YautjaHuntJournalComponent>(ent.Owner);
    }

    private void OnMarkedMobStateChanged(Entity<YautjaMarkComponent> ent, ref MobStateChangedEvent args)
    {
        if (_net.IsClient || args.NewMobState != MobState.Dead)
            return;
        foreach (var (kind, hunter) in ent.Comp.Marks.ToArray())
            TryClearMark(ent.Owner, kind, hunter);
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        if (_net.IsClient)
            return;
        var uid = args.Entity.Owner;
        var query = EntityQueryEnumerator<YautjaHuntJournalComponent>();
        while (query.MoveNext(out _, out var journal))
        {
            if (!journal.Targets.Remove(uid, out var recordId) ||
                !journal.Records.TryGetValue(recordId, out var record))
                continue;
            record.Target = null;
            journal.Visible.Remove(uid);
            journal.Revision++;
        }
    }

    private void OnOpenMarkPanel(Entity<YautjaBracerComponent> ent, ref YautjaOpenMarkPanelActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args) || !TryOpenMarkPanel(ent, args.Performer))
            return;
        args.Handled = true;
    }

    public bool TryOpenMarkPanel(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanUsePanel(bracer, user))
            return false;
        var journal = EnsureComp<YautjaHuntJournalComponent>(user);
        Observe(user, journal);
        _ui.TryOpenUi(bracer.Owner, YautjaMarkUIKey.Key, user);
        UpdateUi(bracer, user);
        return true;
    }

    private void OnUiOpened(Entity<YautjaBracerComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (Equals(args.UiKey, YautjaMarkUIKey.Key))
            UpdateUi(ent, args.Actor);
    }

    private void OnRefreshMsg(Entity<YautjaBracerComponent> ent, ref YautjaMarkPanelRefreshMsg args)
    {
        if (!CanUsePanel(ent, args.Actor))
            return;
        var journal = EnsureComp<YautjaHuntJournalComponent>(args.Actor);
        journal.History = args.History;
        journal.Page = Math.Max(0, args.Page);
        Observe(args.Actor, journal);
        UpdateUi(ent, args.Actor);
    }

    private void OnMarkMsg(Entity<YautjaBracerComponent> ent, ref YautjaMarkPanelMarkMsg args)
    {
        if (_net.IsClient || !TryResolveRecord(args.Actor, args.RecordId, args.Revision, out var target))
            return;
        if (TryMark(ent, args.Actor, target, args.Kind, args.Reason))
            UpdateUi(ent, args.Actor);
    }

    private void OnUnmarkMsg(Entity<YautjaBracerComponent> ent, ref YautjaMarkPanelUnmarkMsg args)
    {
        if (_net.IsClient || !CanUsePanel(ent, args.Actor) ||
            !TryResolveRecord(args.Actor, args.RecordId, args.Revision, out var target))
            return;
        if (TryClearMark(target, args.Kind, args.Actor))
            UpdateUi(ent, args.Actor);
    }

    private void OnChangeMsg(Entity<YautjaBracerComponent> ent, ref YautjaMarkPanelChangeMsg args)
    {
        if (_net.IsClient || !TryResolveRecord(args.Actor, args.RecordId, args.Revision, out var target))
            return;
        if (TryChangeMark(ent, args.Actor, target, args.OldKind, args.NewKind, args.Reason))
            UpdateUi(ent, args.Actor);
    }

    public bool TryMark(Entity<YautjaBracerComponent> bracer, EntityUid hunter, EntityUid target,
        YautjaMarkKind kind, string? reason)
    {
        if (_net.IsClient || !CanUsePanel(bracer, hunter) || !IsDefined(kind) || Deleted(target))
            return false;

        var journal = EnsureComp<YautjaHuntJournalComponent>(hunter);
        var record = EnsureVisibleRecord(hunter, target, journal);
        var hasOwnedMark = HasOwnedMark(target, hunter);
        if (record == null || !journal.Recent.Contains(record.Id) && !hasOwnedMark)
            return false;
        if (!CanMarkTarget(target, kind) ||
            TryComp(target, out YautjaMarkComponent? existing) && existing.Marks.ContainsKey(kind) ||
            kind == YautjaMarkKind.Prey && HunterHasPrey(hunter, target))
            return false;

        var mark = EnsureComp<YautjaMarkComponent>(target);
        var trimmed = TrimReason(reason);
        var attempt = new YautjaMarkAttemptEvent(hunter, target, kind, trimmed);
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled)
        {
            if (mark.Marks.Count == 0)
                RemCompDeferred<YautjaMarkComponent>(target);
            return false;
        }

        mark.Marks.Add(kind, hunter);
        Dirty(target, mark);
        if (!HasComp<YautjaComponent>(target))
            EnsureComp<StatusIconComponent>(target);
        RecordMutation(hunter, target, record, journal);
        LogApply(hunter, target, kind, trimmed);
        if (!IsRelationshipMark(kind))
            NotifyApplied(hunter, target, kind);
        var applied = new YautjaMarkAppliedEvent(hunter, target, kind, trimmed);
        RaiseLocalEvent(target, ref applied);
        return true;
    }

    public bool TryChangeMark(Entity<YautjaBracerComponent> bracer, EntityUid hunter, EntityUid target,
        YautjaMarkKind oldKind, YautjaMarkKind newKind, string? reason)
    {
        if (_net.IsClient || !CanUsePanel(bracer, hunter) || !IsDefined(oldKind) || !IsDefined(newKind) ||
            oldKind == newKind || Deleted(target) || !CanMarkTarget(target, newKind) ||
            oldKind == YautjaMarkKind.Thrall && newKind == YautjaMarkKind.Blooded ||
            !TryComp(target, out YautjaMarkComponent? mark) ||
            !mark.Marks.TryGetValue(oldKind, out var owner) || owner != hunter ||
            mark.Marks.ContainsKey(newKind) ||
            newKind == YautjaMarkKind.Prey && HunterHasPrey(hunter, target))
            return false;

        var journal = EnsureComp<YautjaHuntJournalComponent>(hunter);
        if (!journal.Targets.TryGetValue(target, out var recordId) ||
            !journal.Records.TryGetValue(recordId, out var record))
            return false;

        var removals = GetRemovalKinds(mark, hunter, oldKind);
        foreach (var removal in removals)
        {
            var removeAttempt = new YautjaMarkRemoveAttemptEvent(hunter, target, removal);
            RaiseLocalEvent(target, ref removeAttempt);
            if (removeAttempt.Cancelled)
                return false;
        }

        var trimmed = TrimReason(reason);
        var addAttempt = new YautjaMarkAttemptEvent(hunter, target, newKind, trimmed);
        RaiseLocalEvent(target, ref addAttempt);
        if (addAttempt.Cancelled)
            return false;

        foreach (var removal in removals)
            mark.Marks.Remove(removal);
        mark.Marks.Add(newKind, hunter);
        Dirty(target, mark);
        foreach (var removal in removals)
        {
            var removed = new YautjaMarkRemovedEvent(hunter, target, removal);
            RaiseLocalEvent(target, ref removed);
        }
        var applied = new YautjaMarkAppliedEvent(hunter, target, newKind, trimmed);
        RaiseLocalEvent(target, ref applied);
        RecordMutation(hunter, target, record, journal);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter):actor} changed Yautja mark {oldKind} to {newKind} on {ToPrettyString(target):target}{ReasonSuffix(trimmed)}");
        if (oldKind != YautjaMarkKind.Thrall && !IsRelationshipMark(newKind))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-changed", ("target", target),
                ("old", Loc.GetString(GetMarkName(oldKind))), ("new", Loc.GetString(GetMarkName(newKind)))), hunter, hunter);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-changed-target",
                ("old", Loc.GetString(GetMarkName(oldKind))), ("new", Loc.GetString(GetMarkName(newKind)))), target, target);
        }
        return true;
    }

    public void ForceMark(EntityUid hunter, EntityUid target, YautjaMarkKind kind, bool addStatusIcon = true)
    {
        if (_net.IsClient || !IsDefined(kind))
            return;
        var mark = EnsureComp<YautjaMarkComponent>(target);
        mark.Marks[kind] = hunter;
        Dirty(target, mark);
        if (addStatusIcon)
            EnsureComp<StatusIconComponent>(target);
        var journal = EnsureComp<YautjaHuntJournalComponent>(hunter);
        var record = EnsureRecord(target, journal, true);
        RecordMutation(hunter, target, record, journal);
    }

    private bool CanUsePanel(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!HasComp<YautjaComponent>(user))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
            return false;
        }
        return bracer.Comp.User == user && _inventory.InSlotWithFlags((bracer, null, null), bracer.Comp.Slots);
    }

    private bool CanMarkTarget(EntityUid target, YautjaMarkKind kind)
    {
        if (_mob.IsDead(target))
            return false;
        var humanoid = HasComp<HumanoidProfileComponent>(target);
        var xeno = HasComp<XenoComponent>(target);
        return kind switch
        {
            YautjaMarkKind.Thrall or YautjaMarkKind.Blooded => humanoid && !HasComp<YautjaComponent>(target),
            YautjaMarkKind.Honored or YautjaMarkKind.GearCarrier => humanoid,
            YautjaMarkKind.Student => HasComp<YautjaComponent>(target),
            YautjaMarkKind.Prey or YautjaMarkKind.Dishonored => humanoid || xeno,
            _ => false,
        };
    }

    private bool HunterHasPrey(EntityUid hunter, EntityUid allowedTarget)
    {
        var query = EntityQueryEnumerator<YautjaMarkComponent>();
        while (query.MoveNext(out var uid, out var mark))
        {
            if (uid != allowedTarget && mark.Marks.TryGetValue(YautjaMarkKind.Prey, out var owner) && owner == hunter)
                return true;
        }
        return false;
    }

    private bool HasOwnedMark(EntityUid target, EntityUid hunter)
    {
        return TryComp(target, out YautjaMarkComponent? mark) && mark.Marks.Values.Any(owner => owner == hunter);
    }

    private void ClearHunterMarks(EntityUid hunter)
    {
        var owned = new List<(EntityUid Target, YautjaMarkKind Kind)>();
        var query = EntityQueryEnumerator<YautjaMarkComponent>();
        while (query.MoveNext(out var target, out var mark))
        {
            foreach (var (kind, owner) in mark.Marks)
            {
                if (owner == hunter)
                    owned.Add((target, kind));
            }
        }
        foreach (var (target, kind) in owned)
            TryClearMark(target, kind, hunter);
    }

    public bool IsMarkedBy(EntityUid target, YautjaMarkKind kind, EntityUid hunter)
    {
        return TryComp(target, out YautjaMarkComponent? mark) &&
               mark.Marks.TryGetValue(kind, out var owner) && owner == hunter;
    }

    public bool TryClearMark(EntityUid target, YautjaMarkKind kind, EntityUid? hunter = null)
    {
        if (_net.IsClient || !IsDefined(kind) ||
            !TryComp(target, out YautjaMarkComponent? mark) ||
            !mark.Marks.TryGetValue(kind, out var owner) ||
            hunter is { } required && owner != required)
            return false;

        var removals = GetRemovalKinds(mark, owner, kind);
        foreach (var removal in removals)
        {
            var attempt = new YautjaMarkRemoveAttemptEvent(owner, target, removal);
            RaiseLocalEvent(target, ref attempt);
            if (attempt.Cancelled)
                return false;
        }
        foreach (var removal in removals)
            mark.Marks.Remove(removal);
        if (mark.Marks.Count == 0)
            RemCompDeferred<YautjaMarkComponent>(target);
        else
            Dirty(target, mark);
        foreach (var removal in removals)
        {
            var removed = new YautjaMarkRemovedEvent(owner, target, removal);
            RaiseLocalEvent(target, ref removed);
        }
        if (TryComp(owner, out YautjaHuntJournalComponent? journal) &&
            journal.Targets.TryGetValue(target, out var recordId) && journal.Records.TryGetValue(recordId, out var record))
            RecordMutation(owner, target, record, journal);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(owner):actor} removed Yautja mark {kind} from {ToPrettyString(target):target}");
        if (kind != YautjaMarkKind.Thrall)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-removed", ("target", target),
                ("kind", Loc.GetString(GetMarkName(kind)))), owner, owner);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-removed-target",
                ("kind", Loc.GetString(GetMarkName(kind)))), target, target);
        }
        return true;
    }

    private static List<YautjaMarkKind> GetRemovalKinds(YautjaMarkComponent mark, EntityUid hunter,
        YautjaMarkKind requested)
    {
        var result = new List<YautjaMarkKind>(2);
        if (requested == YautjaMarkKind.Thrall &&
            mark.Marks.TryGetValue(YautjaMarkKind.Blooded, out var bloodedOwner) && bloodedOwner == hunter)
            result.Add(YautjaMarkKind.Blooded);
        result.Add(requested);
        return result;
    }

    private bool Observe(EntityUid hunter, YautjaHuntJournalComponent journal)
    {
        var visible = new HashSet<EntityUid>();
        var coords = _transform.GetMapCoordinates(hunter);
        var candidates = _lookup.GetEntitiesInRange(coords, journal.ObservationRange)
            .Where(target => target != hunter && IsSentient(target) && !_mob.IsDead(target))
            .OrderBy(target => target.Id);
        var changed = false;
        foreach (var target in candidates)
        {
            if (!_examine.CanExamine(hunter, target) ||
                !_containers.IsInSameOrTransparentContainer(hunter, target, userSeeInsideSelf: true))
                continue;
            visible.Add(target);
            var newlyVisible = !journal.Visible.Contains(target);
            var record = EnsureRecord(target, journal, true);
            record.LastSeen = _timing.CurTime;
            changed |= SnapshotMarks(hunter, target, record);
            if (newlyVisible)
            {
                journal.Recent.Remove(record.Id);
                journal.Recent.Insert(0, record.Id);
                changed = true;
            }
        }
        if (!journal.Visible.SetEquals(visible))
        {
            journal.Visible.Clear();
            journal.Visible.UnionWith(visible);
        }
        while (journal.Recent.Count > journal.RecentLimit)
        {
            var evictedId = journal.Recent[^1];
            journal.Recent.RemoveAt(journal.Recent.Count - 1);
            if (journal.Records.TryGetValue(evictedId, out var evicted) && !evicted.WasMarked)
            {
                if (evicted.Target is { } target)
                    journal.Targets.Remove(target);
                journal.Records.Remove(evictedId);
            }
            changed = true;
        }
        if (changed)
            journal.Revision++;
        return changed;
    }

    private bool IsSentient(EntityUid target)
    {
        if (HasComp<EntityActiveInvisibleComponent>(target))
            return false;
        return HasComp<HumanoidProfileComponent>(target) || HasComp<XenoComponent>(target) ||
               TryComp(target, out MindContainerComponent? mind) && mind.HasMind;
    }

    private YautjaHuntRecord? EnsureVisibleRecord(EntityUid hunter, EntityUid target,
        YautjaHuntJournalComponent journal)
    {
        if (journal.Targets.TryGetValue(target, out var id) && journal.Records.TryGetValue(id, out var existing))
            return existing;
        if (!IsSentient(target) || !_examine.CanExamine(hunter, target) ||
            !_containers.IsInSameOrTransparentContainer(hunter, target, userSeeInsideSelf: true))
            return null;
        var record = EnsureRecord(target, journal, true);
        record.LastSeen = _timing.CurTime;
        journal.Recent.Insert(0, record.Id);
        journal.Visible.Add(target);
        journal.Revision++;
        return record;
    }

    private YautjaHuntRecord EnsureRecord(EntityUid target, YautjaHuntJournalComponent journal, bool seen)
    {
        if (journal.Targets.TryGetValue(target, out var id) && journal.Records.TryGetValue(id, out var record))
            return record;
        record = new YautjaHuntRecord(journal.NextId++, target, Name(target), HasComp<XenoComponent>(target));
        if (seen)
            record.LastSeen = _timing.CurTime;
        journal.Records.Add(record.Id, record);
        journal.Targets.Add(target, record.Id);
        return record;
    }

    private void RecordMutation(EntityUid hunter, EntityUid target, YautjaHuntRecord record,
        YautjaHuntJournalComponent journal)
    {
        record.WasMarked = true;
        SnapshotMarks(hunter, target, record);
        journal.Revision++;
    }

    private bool SnapshotMarks(EntityUid hunter, EntityUid target, YautjaHuntRecord record)
    {
        var known = new List<YautjaMarkKind>();
        var owned = new List<YautjaMarkKind>();
        if (TryComp(target, out YautjaMarkComponent? mark))
        {
            known.AddRange(mark.Marks.Keys.OrderBy(kind => kind));
            owned.AddRange(mark.Marks.Where(pair => pair.Value == hunter).Select(pair => pair.Key).OrderBy(kind => kind));
        }
        if (record.LastKnownMarks.SequenceEqual(known) && record.LastOwnedMarks.SequenceEqual(owned))
            return false;
        record.LastKnownMarks.Clear();
        record.LastKnownMarks.AddRange(known);
        record.LastOwnedMarks.Clear();
        record.LastOwnedMarks.AddRange(owned);
        return true;
    }

    private bool TryResolveRecord(EntityUid hunter, int recordId, uint revision, out EntityUid target)
    {
        target = default;
        if (!TryComp(hunter, out YautjaHuntJournalComponent? journal) || journal.Revision != revision ||
            !journal.Records.TryGetValue(recordId, out var record) || record.Target is not { } live ||
            Deleted(live) || Terminating(live))
            return false;
        target = live;
        return true;
    }

    private void UpdateUi(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (_net.IsClient || !CanUsePanel(bracer, user))
            return;
        var journal = EnsureComp<YautjaHuntJournalComponent>(user);
        IEnumerable<YautjaHuntRecord> selected;
        var pages = 1;
        if (journal.History)
        {
            var history = journal.Records.Values.Where(record => record.WasMarked)
                .OrderByDescending(record => record.LastSeen).ThenBy(record => record.Id).ToList();
            pages = Math.Max(1, (int) Math.Ceiling(history.Count / (double) HistoryPageSize));
            journal.Page = Math.Clamp(journal.Page, 0, pages - 1);
            selected = history.Skip(journal.Page * HistoryPageSize).Take(HistoryPageSize);
        }
        else
        {
            journal.Page = 0;
            selected = journal.Recent.Where(journal.Records.ContainsKey).Select(id => journal.Records[id]);
        }
        var entries = selected.Select(record => new YautjaMarkPanelEntry(record.Id, record.Name, record.IsXeno,
            new List<YautjaMarkKind>(record.LastKnownMarks), new List<YautjaMarkKind>(record.LastOwnedMarks),
            record.Target is { } target && !Deleted(target) && !_mob.IsDead(target))).ToList();
        _ui.SetUiState(bracer.Owner, YautjaMarkUIKey.Key,
            new YautjaMarkPanelState(entries, journal.Revision, journal.History, journal.Page, pages));
    }

    private static bool IsDefined(YautjaMarkKind kind) => Enum.IsDefined(kind);
    private static bool IsRelationshipMark(YautjaMarkKind kind) =>
        kind is YautjaMarkKind.Thrall or YautjaMarkKind.Blooded;
    private static string? TrimReason(string? reason)
    {
        var trimmed = reason?.Trim();
        return trimmed is { Length: > MaxReasonLength } ? trimmed[..MaxReasonLength] : trimmed;
    }
    private static string ReasonSuffix(string? reason) => string.IsNullOrWhiteSpace(reason) ? string.Empty : $" reason=\"{reason}\"";

    private void LogApply(EntityUid hunter, EntityUid target, YautjaMarkKind kind, string? reason)
    {
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter):actor} applied Yautja mark {kind} to {ToPrettyString(target):target}{ReasonSuffix(reason)}");
    }

    private void NotifyApplied(EntityUid hunter, EntityUid target, YautjaMarkKind kind)
    {
        var name = Loc.GetString(GetMarkName(kind));
        _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-applied", ("target", target), ("kind", name)), hunter, hunter);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-applied-target", ("kind", name)), target, target);
    }

    public static string GetMarkName(YautjaMarkKind kind)
    {
        return kind switch
        {
            YautjaMarkKind.Prey => "cmu-yautja-mark-prey",
            YautjaMarkKind.Honored => "cmu-yautja-mark-honored",
            YautjaMarkKind.Dishonored => "cmu-yautja-mark-dishonored",
            YautjaMarkKind.GearCarrier => "cmu-yautja-mark-gear-carrier",
            YautjaMarkKind.Thrall => "cmu-yautja-mark-thrall",
            YautjaMarkKind.Student => "cmu-yautja-mark-student",
            YautjaMarkKind.Blooded => "cmu-yautja-mark-blooded",
            _ => "cmu-yautja-mark-unknown",
        };
    }
}
