// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Linq;
using Content.Shared._AU14.SavedBuilds;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._AU14.SavedBuilds;

/// <summary>
/// Tracks round-scoped, one-directional "build partner" grants. If owner O adds player P as a
/// partner, then P is allowed to include O's player-built entities in P's saved builds.
/// Grants are intentionally not persisted across rounds (the entities they refer to are not
/// either) and are cleared on round restart.
/// </summary>
public sealed partial class BuildPartnerSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    // owner -> set of users the owner has granted to include the owner's builds.
    private readonly Dictionary<NetUserId, HashSet<NetUserId>> _grants = new();

    // ============================================
    // 🔧 TUNABLE: max build partners one player may grant
    // ============================================
    private const int MaxPartners = 32;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearGrants());
        SubscribeNetworkEvent<RequestBuildPartnerListEvent>(OnRequestList);
        SubscribeNetworkEvent<SetBuildPartnerEvent>(OnSetPartner);
        SubscribeNetworkEvent<ClearBuildPartnersEvent>(OnClearPartners);
    }

    /// <summary>Notifies a player (if online) that their build-partner status with the owner changed.</summary>
    private void NotifyPartner(NetUserId partner, ICommonSession owner, bool added)
    {
        if (!_playerManager.TryGetSessionById(partner, out var session) || session.AttachedEntity is not { } ent)
            return;

        _popup.PopupEntity(
            Loc.GetString(added ? "build-partner-granted-to-you" : "build-partner-revoked-from-you",
                ("name", owner.Name)),
            ent, ent);
    }

    /// <summary>Sends the requester the list of other online players and whether each is currently their partner.</summary>
    private void OnRequestList(RequestBuildPartnerListEvent ev, EntitySessionEventArgs args)
    {
        var owner = args.SenderSession.UserId;
        _grants.TryGetValue(owner, out var partners);

        var list = new BuildPartnerListEvent();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.UserId == owner)
                continue;

            list.Players.Add(new BuildPartnerInfo
            {
                User = session.UserId,
                Name = session.Name,
                IsPartner = partners != null && partners.Contains(session.UserId),
            });
        }

        list.Players = list.Players.OrderBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase).ToList();
        RaiseNetworkEvent(list, args.SenderSession);
    }

    /// <summary>The requester grants or revokes a player's access to the requester's own builds, then gets a fresh list.</summary>
    private void OnSetPartner(SetBuildPartnerEvent ev, EntitySessionEventArgs args)
    {
        var owner = args.SenderSession.UserId;

        // Grants may only target a real, connected player: without this a client could spam arbitrary
        // GUIDs and grow its grant set without bound (and grant users the UI never offered).
        if (ev.Add && !_playerManager.TryGetSessionById(ev.Partner, out _))
            return;

        if (ev.Add)
            AddPartner(owner, ev.Partner);
        else
            RemovePartner(owner, ev.Partner);

        NotifyPartner(ev.Partner, args.SenderSession, ev.Add);
        OnRequestList(new RequestBuildPartnerListEvent(), args);
    }

    /// <summary>The requester revokes every one of their build partners at once, notifying each, then gets a fresh list.</summary>
    private void OnClearPartners(ClearBuildPartnersEvent ev, EntitySessionEventArgs args)
    {
        var owner = args.SenderSession.UserId;
        if (_grants.TryGetValue(owner, out var partners) && partners.Count > 0)
        {
            foreach (var partner in partners.ToList())
                NotifyPartner(partner, args.SenderSession, added: false);
            partners.Clear();
        }

        OnRequestList(new RequestBuildPartnerListEvent(), args);
    }

    /// <summary>True if <paramref name="saver"/> may capture an entity built by <paramref name="builder"/>.</summary>
    public bool CanInclude(NetUserId saver, NetUserId builder)
    {
        return saver == builder
            || (_grants.TryGetValue(builder, out var partners) && partners.Contains(saver));
    }

    public void AddPartner(NetUserId owner, NetUserId partner)
    {
        if (owner == partner)
            return;

        var partners = _grants.GetOrNew(owner);
        if (partners.Count >= MaxPartners)
            return;

        partners.Add(partner);
    }

    public void RemovePartner(NetUserId owner, NetUserId partner)
    {
        if (_grants.TryGetValue(owner, out var partners))
            partners.Remove(partner);
    }

    public void ClearGrants()
    {
        _grants.Clear();
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        // Only offer the verb between two distinct players.
        if (!_playerManager.TryGetSessionByEntity(args.User, out var ownerSession))
            return;
        if (!_playerManager.TryGetSessionByEntity(args.Target, out var targetSession))
            return;
        if (ownerSession.UserId == targetSession.UserId)
            return;

        var owner = ownerSession.UserId;
        var partner = targetSession.UserId;
        var isPartner = _grants.TryGetValue(owner, out var partners) && partners.Contains(partner);
        var targetName = Name(args.Target);

        if (!isPartner)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("build-partner-add-verb"),
                Act = () =>
                {
                    AddPartner(owner, partner);
                    _popup.PopupEntity(
                        Loc.GetString("build-partner-added", ("name", targetName)), args.User, args.User);
                },
            });
        }
        else
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("build-partner-remove-verb"),
                Act = () =>
                {
                    RemovePartner(owner, partner);
                    _popup.PopupEntity(
                        Loc.GetString("build-partner-removed", ("name", targetName)), args.User, args.User);
                },
            });
        }
    }
}
