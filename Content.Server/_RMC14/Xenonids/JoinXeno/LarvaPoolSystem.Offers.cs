using System.Linq;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Xenonids.JoinXeno;

public sealed partial class LarvaPoolSystem
{
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private static readonly TimeSpan OfferDuration = TimeSpan.FromSeconds(30);

    private readonly List<NetUserId> _expiredOffers = [];
    private readonly Dictionary<EntityUid, Dictionary<NetUserId, TimeSpan>> _offerCooldowns = [];
    private readonly Dictionary<NetUserId, PendingLarvaPoolOffer> _pendingOffers = [];
    private readonly Dictionary<EntityUid, NetUserId> _pendingOfferHives = [];
    private readonly Dictionary<EntityUid, NetUserId> _pendingOfferTargets = [];
    private int _nextOfferId;

    private void InitializeOffers()
    {
        SubscribeLocalEvent<LarvaPoolClaimConfirmEvent>(OnLarvaPoolClaimConfirm);
        SubscribeLocalEvent<LarvaPoolClaimDeclineEvent>(OnLarvaPoolClaimDecline);
    }

    private void CleanupOffers()
    {
        _expiredOffers.Clear();
        _offerCooldowns.Clear();
        _pendingOffers.Clear();
        _pendingOfferHives.Clear();
        _pendingOfferTargets.Clear();
        _nextOfferId = 0;
    }

    private void ExpirePendingOffers(TimeSpan time)
    {
        _expiredOffers.Clear();
        foreach (var (userId, pending) in _pendingOffers)
        {
            if (pending.ExpiresAt <= time)
                _expiredOffers.Add(userId);
        }

        foreach (var userId in _expiredOffers)
        {
            CancelPendingOffer(userId, LarvaPoolOfferCancellation.TimedOut);
        }
    }

    private void TryOfferForHive(Entity<HiveComponent> hive)
    {
        if (_pendingOfferHives.ContainsKey(hive.Owner) ||
            !TryGetOfferTarget(hive, out var target, out var xenoName))
        {
            return;
        }

        var candidates = GetCandidatesForHive(hive.Owner);
        PrioritizeStrandedXenos(hive.Owner, candidates);
        foreach (var candidate in candidates)
        {
            var userId = candidate.Session.UserId;
            if (_pendingOffers.ContainsKey(userId) ||
                HasComp<DialogComponent>(candidate.OfferEntity) ||
                IsOfferOnCooldown(hive.Owner, userId) ||
                !MeetsTargetRoleRequirements(candidate.Session, target))
            {
                continue;
            }

            OpenPendingOffer(candidate, hive.Owner, target, xenoName);
            return;
        }
    }

    private bool TryGetOfferTarget(Entity<HiveComponent> hive, out EntityUid? target, out string xenoName)
    {
        if (TryGetAvailableLarva(hive, out var larva))
        {
            target = larva;
            xenoName = Name(larva);
            return true;
        }

        if (TryGetAbandonedXeno(hive, out var abandoned))
        {
            target = abandoned;
            xenoName = Name(abandoned);
            return true;
        }

        if (hive.Comp.BurrowedLarva > 0 && _hive.HasBurrowedLarvaSpawnPoint(hive))
        {
            target = null;
            xenoName = Loc.GetString("rmc-xeno-larva-pool-offer-burrowed-larva");
            return true;
        }

        target = null;
        xenoName = string.Empty;
        return false;
    }

    private void OpenPendingOffer(
        LarvaPoolCandidate candidate,
        EntityUid hive,
        EntityUid? target,
        string xenoName)
    {
        var userId = candidate.Session.UserId;
        var offerId = ++_nextOfferId;
        var pending = new PendingLarvaPoolOffer(
            offerId,
            hive,
            candidate.OfferEntity,
            target,
            _timing.CurTime + OfferDuration);

        _pendingOffers[userId] = pending;
        _pendingOfferHives[hive] = userId;
        if (target is { } targetId)
        {
            _pendingOfferTargets[targetId] = userId;
            var offer = EnsureComp<LarvaPoolOfferComponent>(targetId);
            offer.ExpiresAt = pending.ExpiresAt;
            Dirty(targetId, offer);
        }

        var options = new List<DialogOption>
        {
            new(
                Loc.GetString("rmc-xeno-larva-pool-offer-accept"),
                new LarvaPoolClaimConfirmEvent(userId, offerId)),
            new(
                Loc.GetString("rmc-xeno-larva-pool-offer-decline"),
                new LarvaPoolClaimDeclineEvent(userId, offerId)),
        };

        _dialog.OpenOptions(
            candidate.OfferEntity,
            candidate.OfferEntity,
            Loc.GetString("rmc-xeno-larva-pool-offer-title"),
            options,
            Loc.GetString(
                "rmc-xeno-larva-pool-offer-message",
                ("xeno", xenoName)),
            pending.ExpiresAt);
    }

    private void OnLarvaPoolClaimConfirm(LarvaPoolClaimConfirmEvent ev)
    {
        if (!_pendingOffers.TryGetValue(ev.UserId, out var pending) || pending.OfferId != ev.ClaimId)
            return;

        if (pending.ExpiresAt <= _timing.CurTime)
        {
            CancelPendingOffer(ev.UserId, LarvaPoolOfferCancellation.TimedOut);
            return;
        }

        ReleasePendingOffer(ev.UserId, pending);
        ClosePendingOfferDialog(pending);

        if (!TryGetEligibleCandidate(ev.UserId, pending.Hive, out var session))
        {
            TryOfferNextForHive(pending.Hive);
            return;
        }

        var claimed = TryClaimPendingOffer(pending, session);
        if (claimed)
        {
            _offerCooldowns.Remove(pending.Hive);
            NotifyPoolPositions();
        }
        else if (session.AttachedEntity is { } attached)
        {
            _popup.PopupEntity(
                Loc.GetString("rmc-xeno-larva-pool-offer-invalid"),
                attached,
                attached,
                PopupType.MediumCaution);
        }

        TryOfferNextForHive(pending.Hive);
    }

    private void OnLarvaPoolClaimDecline(LarvaPoolClaimDeclineEvent ev)
    {
        if (!_pendingOffers.TryGetValue(ev.UserId, out var pending) || pending.OfferId != ev.ClaimId)
            return;

        CancelPendingOffer(ev.UserId, LarvaPoolOfferCancellation.Declined);
    }

    private bool TryGetEligibleCandidate(NetUserId userId, EntityUid hive, out ICommonSession session)
    {
        if (!_player.TryGetSessionById(userId, out session!) ||
            session.Status is SessionStatus.Disconnected or SessionStatus.Zombie ||
            session.AttachedEntity is not { } attached ||
            !(_ghostQuery.TryComp(attached, out var ghost) || IsActivePoolCandidate(attached)))
        {
            return false;
        }

        return GetEligibility(session, attached, ghost).Eligibility == LarvaPoolEligibility.Eligible &&
               _larvaPoolPreferences.TryGetOptedIn(userId, GetHivePreferenceId(hive), out var optedIn) &&
               optedIn;
    }

    private bool TryClaimPendingOffer(PendingLarvaPoolOffer pending, ICommonSession session)
    {
        if (!_hiveQuery.TryComp(pending.Hive, out var hive) ||
            !MeetsTargetRoleRequirements(session, pending.Target))
            return false;

        if (pending.Target is { } target)
        {
            if (!TryComp(target, out HiveMemberComponent? member) ||
                !(CanAssignLarva(target, member, (pending.Hive, hive)) ||
                  CanAssignAbandonedXeno(target, member, (pending.Hive, hive))))
            {
                return false;
            }

            return AssignXeno(target, session);
        }

        return hive.BurrowedLarva > 0 &&
               _hive.HasBurrowedLarvaSpawnPoint((pending.Hive, hive)) &&
               _hive.JoinBurrowedLarva((pending.Hive, hive), session);
    }

    private bool MeetsTargetRoleRequirements(ICommonSession session, EntityUid? target)
    {
        return target is not { } targetId ||
               !TryComp(targetId, out XenoComponent? xeno) ||
               xeno.Role != QueenRole ||
               _playTime.IsAllowed(session, QueenRole);
    }

    private void CancelPendingOffer(
        NetUserId userId,
        LarvaPoolOfferCancellation cancellation,
        bool tryNext = true)
    {
        if (!_pendingOffers.TryGetValue(userId, out var pending))
            return;

        ReleasePendingOffer(userId, pending);
        ClosePendingOfferDialog(pending);

        if (cancellation is LarvaPoolOfferCancellation.Declined or
            LarvaPoolOfferCancellation.Disconnected or
            LarvaPoolOfferCancellation.TimedOut)
        {
            AddOfferCooldown(pending.Hive, userId);
        }

        if (_player.TryGetSessionById(userId, out var session) &&
            session.AttachedEntity is { } attached)
        {
            if (cancellation == LarvaPoolOfferCancellation.Declined)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-larva-pool-offer-declined"),
                    attached,
                    attached,
                    PopupType.MediumCaution);
            }
            else if (cancellation == LarvaPoolOfferCancellation.TimedOut)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-larva-pool-offer-timeout"),
                    attached,
                    attached,
                    PopupType.MediumCaution);
            }
        }

        if (tryNext)
            TryOfferNextForHive(pending.Hive);
    }

    private void ReleasePendingOffer(NetUserId userId, PendingLarvaPoolOffer pending)
    {
        _pendingOffers.Remove(userId);
        _pendingOfferHives.Remove(pending.Hive);
        if (pending.Target is { } target)
        {
            _pendingOfferTargets.Remove(target);
            RemComp<LarvaPoolOfferComponent>(target);
        }
    }

    private void ClosePendingOfferDialog(PendingLarvaPoolOffer pending)
    {
        if (!TryComp(pending.OfferEntity, out DialogComponent? dialog))
            return;

        var matchesPendingOffer = dialog.Options.Any(option =>
            option.Event is LarvaPoolClaimConfirmEvent ev && ev.ClaimId == pending.OfferId);
        if (!matchesPendingOffer)
            return;

        _ui.CloseUi(pending.OfferEntity, DialogUiKey.Key);
        RemComp<DialogComponent>(pending.OfferEntity);
    }

    private void TryOfferNextForHive(EntityUid hiveId)
    {
        if (_hiveQuery.TryComp(hiveId, out var hive))
            TryOfferForHive((hiveId, hive));
    }

    private void AddOfferCooldown(EntityUid hive, NetUserId userId)
    {
        if (!_offerCooldowns.TryGetValue(hive, out var cooldowns))
        {
            cooldowns = [];
            _offerCooldowns[hive] = cooldowns;
        }

        cooldowns[userId] = _timing.CurTime + OfferDuration;
    }

    private bool IsOfferOnCooldown(EntityUid hive, NetUserId userId)
    {
        if (!_offerCooldowns.TryGetValue(hive, out var cooldowns) ||
            !cooldowns.TryGetValue(userId, out var expiresAt))
        {
            return false;
        }

        if (expiresAt > _timing.CurTime)
            return true;

        cooldowns.Remove(userId);
        if (cooldowns.Count == 0)
            _offerCooldowns.Remove(hive);
        return false;
    }

    private sealed record PendingLarvaPoolOffer(
        int OfferId,
        EntityUid Hive,
        EntityUid OfferEntity,
        EntityUid? Target,
        TimeSpan ExpiresAt);

    private enum LarvaPoolOfferCancellation : byte
    {
        Invalid,
        OptedOut,
        Declined,
        Disconnected,
        TimedOut,
    }
}
