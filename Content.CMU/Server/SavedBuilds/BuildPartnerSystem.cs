// SPDX-License-Identifier: LicenseRef-AdvancedAtkinsonatorv2-Proprietary
// Copyright (c) 2026 wray-git. All rights reserved.
// Proprietary - reuse only with the Author's prior written authorization. See LICENSE-AdvancedAtkinsonatorv2.md.
using System.Collections.Generic;
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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearGrants());
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

        _grants.GetOrNew(owner).Add(partner);
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
