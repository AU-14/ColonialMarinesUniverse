using System.Linq;
using Content.Shared._RMC14.Areas;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaMarkSystem
{
    private static readonly ProtoId<NpcFactionPrototype> YautjaBadBloodFaction = "CMUYautjaBadBlood";
    [Dependency] private AreaSystem _areas = default!;

    private void InitializeTransitions()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        if (_net.IsClient)
            return;
        var query = EntityQueryEnumerator<YautjaMarkComponent>();
        while (query.MoveNext(out var uid, out var marks))
            ClearTargetMarks((uid, marks), cleanupOnly: true);
    }

    private void ClearTargetMarks(Entity<YautjaMarkComponent> target, bool targetDestroyed = false,
        bool cleanupOnly = false)
    {
        foreach (var kind in target.Comp.Marks.Keys.ToArray())
            RemoveMark(target, kind, targetDestroyed: targetDestroyed, cleanupOnly: cleanupOnly);
    }

    private bool RemoveMark(EntityUid target, YautjaMarkKind kind, EntityUid? hunter = null,
        bool showPreyRemoved = false, bool targetDestroyed = false, bool cleanupOnly = false, EntityUid? actor = null)
    {
        if (_net.IsClient || !IsDefined(kind) ||
            !TryComp(target, out YautjaMarkComponent? mark) ||
            !mark.Marks.TryGetValue(kind, out var owner) || hunter is { } required && owner != required)
            return false;

        if (!targetDestroyed && !cleanupOnly)
        {
            var attempt = new YautjaMarkRemoveAttemptEvent(actor ?? owner, target, kind);
            RaiseLocalEvent(target, ref attempt);
            if (attempt.Cancelled)
                return false;
        }

        var removals = GetRemovalKinds(mark, owner, kind);
        foreach (var removal in removals)
        {
            mark.Marks.Remove(removal);
            mark.Reasons.Remove(removal);
        }
        if (!targetDestroyed)
        {
            if (mark.Marks.Count == 0)
                RemCompDeferred<YautjaMarkComponent>(target);
            else
                Dirty(target, mark);
        }
        foreach (var removal in removals)
        {
            var removed = new YautjaMarkRemovedEvent(owner, target, removal, cleanupOnly, targetDestroyed);
            RaiseLocalEvent(target, ref removed);
        }
        if (TryComp(owner, out YautjaHuntJournalComponent? journal) &&
            journal.Targets.TryGetValue(target, out var recordId) && journal.Records.TryGetValue(recordId, out var record))
            RecordMutation(owner, target, record, journal);

        if (cleanupOnly || Deleted(owner))
            return true;
        if (targetDestroyed)
        {
            if (kind == YautjaMarkKind.Prey)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-prey-destroyed"), owner, owner, PopupType.MediumCaution);
            return true;
        }
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor ?? owner):actor} removed Yautja mark {kind} from {ToPrettyString(target):target}");
        BroadcastTransition(actor ?? owner, target, kind, true, null);
        if (showPreyRemoved && kind == YautjaMarkKind.Prey)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-prey-removed", ("target", target)), owner, owner, PopupType.Medium);
        return true;
    }

    private void BroadcastTransition(EntityUid hunter, EntityUid target, YautjaMarkKind kind, bool removed, string? reason)
    {
        if (kind == YautjaMarkKind.Prey && !removed)
            BroadcastPreyMark(hunter, target);
        else if (IsHonorOrDishonorMark(kind))
            BroadcastHonorTransition(hunter, target, kind, removed, reason);
        else if (kind == YautjaMarkKind.GearCarrier)
            BroadcastGearCarrierTransition(hunter, target, removed);
    }

    private void BroadcastPreyMark(EntityUid hunter, EntityUid target)
    {
        var message = Loc.GetString(
            "cmu-yautja-mark-prey-broadcast",
            ("hunter", Name(hunter)),
            ("target", Name(target)),
            ("honor", YautjaHonorWorth.Get(target, EntityManager)),
            ("area", _areas.GetAreaName(target)));

        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var yautja, out _))
        {
            if (!Deleted(yautja))
                _popup.PopupEntity(message, yautja, yautja, PopupType.Medium);
        }
    }

    private static bool RequiresReason(YautjaMarkKind kind)
    {
        return kind is YautjaMarkKind.Thrall or YautjaMarkKind.Blooded;
    }

    private static string GetAlreadyMarkedText(YautjaMarkKind kind)
    {
        return kind switch
        {
            YautjaMarkKind.Prey => "cmu-yautja-mark-prey-claimed",
            YautjaMarkKind.Honored => "cmu-yautja-mark-already-honored",
            YautjaMarkKind.Dishonored => "cmu-yautja-mark-already-dishonored",
            YautjaMarkKind.GearCarrier => "cmu-yautja-mark-already-gear-carrier",
            YautjaMarkKind.Student => "cmu-yautja-youngblood-already-claimed",
            _ => "cmu-yautja-mark-already-marked",
        };
    }

    private bool IsBadBloodHonorRestricted(EntityUid hunter, YautjaMarkKind kind, bool popup)
    {
        if (kind is not (YautjaMarkKind.Honored or YautjaMarkKind.Dishonored or YautjaMarkKind.Thrall or YautjaMarkKind.Blooded))
            return false;

        if (!TryComp(hunter, out NpcFactionMemberComponent? faction) ||
            !faction.Factions.Contains(YautjaBadBloodFaction))
        {
            return false;
        }

        if (popup)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-badblood-no-honor"), hunter, hunter, PopupType.SmallCaution);

        return true;
    }

    private static bool IsHonorOrDishonorMark(YautjaMarkKind kind)
    {
        return kind is YautjaMarkKind.Honored or YautjaMarkKind.Dishonored;
    }

    private void BroadcastHonorTransition(
        EntityUid hunter,
        EntityUid target,
        YautjaMarkKind kind,
        bool removed,
        string? reason)
    {
        var key = (kind, removed) switch
        {
            (YautjaMarkKind.Honored, false) => "cmu-yautja-mark-honored-broadcast",
            (YautjaMarkKind.Honored, true) => "cmu-yautja-unmark-honored-broadcast",
            (YautjaMarkKind.Dishonored, false) => "cmu-yautja-mark-dishonored-broadcast",
            (YautjaMarkKind.Dishonored, true) => "cmu-yautja-unmark-dishonored-broadcast",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(key))
            return;

        var message = Loc.GetString(
            key,
            ("hunter", Name(hunter)),
            ("target", Name(target)),
            ("reason", reason ?? string.Empty));

        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var yautja, out _))
        {
            if (!Deleted(yautja))
                _popup.PopupEntity(message, yautja, yautja, PopupType.Medium);
        }
    }

    private void BroadcastGearCarrierTransition(EntityUid hunter, EntityUid target, bool removed)
    {
        var message = Loc.GetString(
            removed ? "cmu-yautja-unmark-gear-carrier-broadcast" : "cmu-yautja-mark-gear-carrier-broadcast",
            ("hunter", Name(hunter)),
            ("target", Name(target)));

        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var yautja, out _))
        {
            if (!Deleted(yautja))
                _popup.PopupEntity(message, yautja, yautja, PopupType.Medium);
        }
    }
}
