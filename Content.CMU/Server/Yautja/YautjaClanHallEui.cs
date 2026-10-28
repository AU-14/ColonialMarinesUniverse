using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.Players.JobWhitelist;
using Content.Shared.Administration;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Server.CMU14.Yautja;

// the one clan window: clan pages, boards, hunter dossiers, and the staff whitelist + round pages.
// replaces the old clan info, clan admin and predator round editor euis
public sealed partial class YautjaClanHallEui : BaseEui
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private YautjaClanManager _clans = default!;
    [Dependency] private YautjaRankManager _ranks = default!;

    private const YautjaWhitelistFlags KnownWhitelistFlags =
        YautjaWhitelistFlags.Yautja |
        YautjaWhitelistFlags.Legacy |
        YautjaWhitelistFlags.Council |
        YautjaWhitelistFlags.CouncilLegacy |
        YautjaWhitelistFlags.Leader;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private YautjaClanHallEuiState _state = new();
    private YautjaClanHallPage _page;
    private bool _navigate = true;
    private int? _clanId;
    private NetUserId? _hunterId;
    private string _status = string.Empty;
    private bool _statusIsError;
    private int _revision;
    private bool _closed;
    private bool _refreshQueued;

    public YautjaClanHallEui(YautjaClanHallPage page = YautjaClanHallPage.Clan)
    {
        IoCManager.InjectDependencies(this);
        _page = page;
    }

    public override void Opened()
    {
        base.Opened();
        _clans.Changed += OnClansChanged;
        _admin.OnPermsChanged += OnAdminPermsChanged;
        _ = Gated(async () =>
        {
            if (!await YautjaClanHallAccess.CanOpen(Player, _admin, _db, _entities))
            {
                Close();
                return;
            }

            await RefreshState();
        });
    }

    public override void Closed()
    {
        base.Closed();
        _closed = true;
        _clans.Changed -= OnClansChanged;
        _admin.OnPermsChanged -= OnAdminPermsChanged;
    }

    public override EuiStateBase GetNewState()
    {
        return _state;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is CloseEuiMessage || _closed)
            return;

        // round settings are plain entity system calls, no db, so do them right now on the tick
        // they arrived instead of queueing behind whatever db work holds the gate
        if (HandleRound(msg))
        {
            _ = Gated(RefreshState);
            return;
        }

        _ = Gated(async () =>
        {
            try
            {
                await Handle(msg);
            }
            catch (Exception e)
            {
                Report(false, "cmu-yautja-hall-error");
                Logger.GetSawmill("cmu.yautja.clan_hall").Error($"Clan hall action failed:\n{e}");
            }

            await RefreshState();
        });
    }

    private async Task Gated(Func<Task> work)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_closed && !IsShutDown)
                await work();
        }
        finally
        {
            _gate.Release();
        }
    }

    // someone else changed a clan, redraw. one queued refresh is enough however many land
    private void OnClansChanged()
    {
        if (_closed || _refreshQueued)
            return;

        _refreshQueued = true;
        _ = Gated(async () =>
        {
            _refreshQueued = false;
            await RefreshState();
        });
    }

    // lost staff and isn't a hunter either: out. otherwise just redraw with the new powers
    private void OnAdminPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player != Player || _closed)
            return;

        _ = Gated(async () =>
        {
            if (!await YautjaClanHallAccess.CanOpen(Player, _admin, _db, _entities))
            {
                Close();
                return;
            }

            await RefreshState();
        });
    }

    private bool IsStaff => _admin.HasAdminFlag(Player, AdminFlags.Clans);
    private bool IsHost => _admin.HasAdminFlag(Player, AdminFlags.Host);

    private async Task<YautjaClanHallActor> Actor()
    {
        var actor = await _clans.ResolveActor(Player.UserId, IsStaff, IsHost);

        // a youngblood in a round is a youngblood here too, whatever their profile says
        if (!actor.Staff &&
            Player.AttachedEntity is { } body &&
            _entities.HasComponent<YautjaYoungbloodComponent>(body) &&
            actor.Member.ClanId == null &&
            actor.Rank < YautjaRank.Elite)
        {
            actor = actor with { Member = actor.Member with { Rank = YautjaRank.YoungBlood } };
        }

        return actor;
    }

    private void Report(bool ok, string locId, params (string, object)[] args)
    {
        _status = Loc.GetString(locId, args);
        _statusIsError = !ok;
    }

    private void Report(YautjaClanMutationResult result, string successLocId)
    {
        Report(result.Succeeded, result.Succeeded ? successLocId : result.Error ?? "cmu-yautja-hall-error");
    }

    private async Task Handle(EuiMessageBase msg)
    {
        var actor = await Actor();
        switch (msg)
        {
            case YautjaClanHallShowMessage show:
                _page = show.Page;
                if (show.ClanId != null)
                    _clanId = show.ClanId;
                if (show.Hunter != null)
                    _hunterId = show.Hunter;
                _status = string.Empty;
                return;

            case YautjaClanHallFindMessage find:
                await Find(actor, find.Query);
                return;

            case YautjaClanHallSetRankMessage setRank:
            {
                var result = await _clans.HallSetRank(actor, setRank.Target, setRank.Rank);
                await AfterMemberChange(result, $"set the Yautja rank of {setRank.Target} to {setRank.Rank}");
                Report(result, "cmu-yautja-hall-done-rank");
                return;
            }

            case YautjaClanHallExileMessage exile:
            {
                var result = await _clans.HallExile(actor, exile.Target);
                await AfterMemberChange(result, $"exiled Yautja {exile.Target} from their clan");
                Report(result, "cmu-yautja-hall-done-exile");
                return;
            }

            case YautjaClanHallMoveMessage move:
            {
                var result = await _clans.HallMove(actor, move.Target, move.ClanId);
                await AfterMemberChange(result, $"moved Yautja {move.Target} to clan {move.ClanId?.ToString() ?? "none"}");
                Report(result, "cmu-yautja-hall-done-move");
                return;
            }

            case YautjaClanHallSetAncientMessage ancient:
            {
                var result = await _clans.HallSetAncient(actor, ancient.Target, ancient.Enabled);
                await AfterMemberChange(result, $"{(ancient.Enabled ? "made" : "removed")} Yautja Ancient {ancient.Target}");
                Report(result, ancient.Enabled ? "cmu-yautja-hall-done-ancient" : "cmu-yautja-hall-done-unancient");
                return;
            }

            case YautjaClanHallPurgeMessage purge:
            {
                var result = await _clans.HallPurge(actor, purge.Target);
                await AfterMemberChange(result, $"purged the Yautja clan profile of {purge.Target}");
                Report(result, "cmu-yautja-hall-done-purge");
                return;
            }

            case YautjaClanHallEditTextMessage text:
            {
                var result = await _clans.HallEditText(actor, text.ClanId, text.Field, text.Text);
                Log(result, LogImpact.Low, $"edited the {text.Field} of Yautja clan {text.ClanId}");
                Report(result, "cmu-yautja-hall-done-saved");
                return;
            }

            case YautjaClanHallEditLookMessage look:
            {
                var result = await _clans.HallEditLook(actor, look.ClanId, look.Name, look.Color, look.Sigil);
                Log(result, LogImpact.Low, $"changed the look of Yautja clan {look.ClanId} ({look.Name}, {look.Color}, {look.Sigil})");
                Report(result, "cmu-yautja-hall-done-saved");
                return;
            }

            case YautjaClanHallSetHonorMessage honor:
            {
                var result = await _clans.HallSetHonor(actor, honor.ClanId, honor.Honor);
                Log(result, LogImpact.Medium, $"set the honor of Yautja clan {honor.ClanId} to {honor.Honor}");
                Report(result, "cmu-yautja-hall-done-honor");
                return;
            }

            case YautjaClanHallFoundClanMessage found:
            {
                var (result, id) = await _clans.HallFoundClan(actor, found.Name, found.Color);
                Log(result, LogImpact.Medium, $"founded Yautja clan {id} ({found.Name})");
                Report(result, "cmu-yautja-hall-done-found");
                if (result.Succeeded)
                    Navigate(YautjaClanHallPage.Clan, id);
                return;
            }

            case YautjaClanHallDisbandMessage disband:
            {
                var result = await _clans.HallDisband(actor, disband.ClanId);
                await AfterMemberChange(result, $"disbanded Yautja clan {disband.ClanId}");
                Report(result, "cmu-yautja-hall-done-disband");
                if (result.Succeeded)
                    Navigate(YautjaClanHallPage.Leaderboard);
                return;
            }

            case YautjaClanHallPetitionMessage petition:
            {
                var result = await _clans.HallPetition(actor, petition.ClanId);
                Report(result, petition.ClanId == null ? "cmu-yautja-hall-done-withdrawn" : "cmu-yautja-hall-done-petition");
                return;
            }

            case YautjaClanHallAnswerPetitionMessage answer:
            {
                var result = await _clans.HallAnswerPetition(actor, answer.Target, answer.Accept);
                if (answer.Accept)
                    await AfterMemberChange(result, $"accepted the Yautja clan petition of {answer.Target}");
                Report(result, answer.Accept ? "cmu-yautja-hall-done-accepted" : "cmu-yautja-hall-done-refused");
                return;
            }

            case YautjaClanHallSetWhitelistMessage whitelist:
                await SetWhitelist(actor, whitelist);
                return;

        }
    }

    private bool HandleRound(EuiMessageBase msg)
    {
        switch (msg)
        {
            case YautjaClanHallRoundInitializeMessage:
                RoundAction((YautjaPredatorRoundSystem round, out string status) =>
                    round.TryInitializePredatorRound(out status));
                return true;

            case YautjaClanHallRoundSlotsMessage slots:
                RoundAction((YautjaPredatorRoundSystem round, out string status) =>
                    round.TrySetHunterSlots(slots.Slots, out status));
                return true;

            case YautjaClanHallRoundRandomMessage random:
                RoundAction((YautjaPredatorRoundSystem round, out string status) =>
                    round.TryConfigureRandom(random.Enabled, random.MinimumRounds, random.MaximumRounds, out status));
                return true;

            default:
                return false;
        }
    }

    private void Navigate(YautjaClanHallPage page, int? clanId = null, NetUserId? hunter = null)
    {
        _page = page;
        _navigate = true;
        if (clanId != null)
            _clanId = clanId;
        if (hunter != null)
            _hunterId = hunter;
    }

    private void Log(YautjaClanMutationResult result, LogImpact impact, string what)
    {
        if (result.Succeeded)
            _adminLog.Add(LogType.Action, impact, $"{Player.Name} {what} through the clan hall.");
    }

    // rank, clan and whitelist all feed job eligibility and spawn rank, so refresh both caches
    private async Task AfterMemberChange(YautjaClanMutationResult result, string what)
    {
        if (!result.Succeeded)
            return;

        foreach (var player in result.AffectedPlayers ?? [])
        {
            await _ranks.Refresh(player);
            await _jobWhitelist.RefreshYautjaWhitelist(player);
        }

        Log(result, LogImpact.Medium, what);
    }

    private delegate bool RoundTry(YautjaPredatorRoundSystem round, out string status);

    private void RoundAction(RoundTry action)
    {
        if (!IsHost)
        {
            Report(false, "cmu-yautja-hall-denied-round");
            return;
        }

        var ok = action(_systems.GetEntitySystem<YautjaPredatorRoundSystem>(), out var status);
        _status = status;
        _statusIsError = !ok;
        if (ok)
            _adminLog.Add(LogType.AdminCommands, LogImpact.Medium, $"{Player.Name} changed the predator round settings: {status}");
    }

    private async Task Find(YautjaClanHallActor actor, string query)
    {
        query = query.Trim();
        if (query.Length == 0)
            return;

        var located = await _playerLocator.LookupIdByNameOrIdAsync(query);
        if (located == null)
        {
            Report(false, "cmu-yautja-hall-not-found", ("player", query));
            return;
        }

        // staff can open anyone, for whitelisting. everyone else only finds hunters on the board
        if (!actor.Staff)
        {
            var board = await _db.GetYautjaHunterBoardAsync();
            if (board.All(row => row.PlayerUserId != located.UserId.UserId))
            {
                Report(false, "cmu-yautja-hall-not-found", ("player", query));
                return;
            }
        }

        _status = string.Empty;
        Navigate(YautjaClanHallPage.Hunter, hunter: located.UserId);
    }

    private async Task SetWhitelist(YautjaClanHallActor actor, YautjaClanHallSetWhitelistMessage message)
    {
        if (!YautjaClanHallPolicy.CanManageWhitelist(actor))
        {
            Report(false, "cmu-yautja-hall-denied-whitelist");
            return;
        }

        if ((message.Flags & ~KnownWhitelistFlags) != YautjaWhitelistFlags.None)
        {
            Report(false, "cmu-yautja-hall-denied-whitelist-flags");
            return;
        }

        var player = await _playerLocator.LookupIdByNameOrIdAsync(message.Player.Trim());
        if (player == null)
        {
            Report(false, "cmu-yautja-hall-not-found", ("player", message.Player));
            return;
        }

        if (_players.TryGetSessionById(player.UserId, out var session) && !_userDb.IsLoadComplete(session))
            await _userDb.WaitLoadComplete(session);

        await _db.SetYautjaWhitelistFlagsAsync(player.UserId.UserId, (int) message.Flags);
        _clans.InvalidateCache(player.UserId);
        await _ranks.Refresh(player.UserId);
        await _jobWhitelist.RefreshYautjaWhitelist(player.UserId);
        _adminLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{Player.Name} set Yautja whitelist flags for {player.Username} ({player.UserId}) to {message.Flags}.");
        Report(true, "cmu-yautja-hall-done-whitelist", ("player", player.Username));
        _clans.NotifyChanged();
    }

    private async Task RefreshState()
    {
        if (_closed || IsShutDown)
            return;

        try
        {
            _state = await BuildState();
        }
        catch (Exception e)
        {
            Logger.GetSawmill("cmu.yautja.clan_hall").Error($"Clan hall refresh failed:\n{e}");
            _state.Status = Loc.GetString("cmu-yautja-hall-error");
            _state.StatusIsError = true;
            _state.Revision = ++_revision;
        }

        if (!_closed && !IsShutDown)
            StateDirty();
    }

    private string NameOf(NetUserId id, string fallback)
    {
        if (_players.TryGetSessionById(id, out var session))
            return session.Name;

        return string.IsNullOrEmpty(fallback) ? id.ToString() : fallback;
    }

    private async Task<YautjaClanHallEuiState> BuildState()
    {
        var actor = await Actor();
        var board = await _db.GetYautjaHunterBoardAsync();
        var clans = await _db.GetYautjaClansAsync();
        var petitions = await _db.GetYautjaClanPetitionsAsync();

        var snapshots = new Dictionary<NetUserId, (YautjaClanMemberSnapshot Snapshot, YautjaHunterBoardRecord Row)>();
        foreach (var row in board)
            snapshots[new NetUserId(row.PlayerUserId)] = (YautjaClanManager.FromBoard(row), row);

        var hunters = snapshots.Values
            .Select(entry => Row(entry.Snapshot, entry.Row.Name))
            .OrderByDescending(row => row.Honor)
            .ThenByDescending(row => row.Rank)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var standings = clans
            .OrderByDescending(clan => clan.Honor)
            .ThenBy(clan => clan.Name, StringComparer.OrdinalIgnoreCase)
            .Select((clan, index) => (clan.Id, Standing: index + 1))
            .ToDictionary(entry => entry.Id, entry => entry.Standing);

        var clanRows = clans
            .Select(clan =>
            {
                var members = hunters.Where(hunter => hunter.ClanId == clan.Id).ToList();
                return new YautjaClanHallClanRow
                {
                    Id = clan.Id,
                    Name = clan.Name,
                    Color = clan.Color,
                    Sigil = YautjaClanSigils.OrDefault(clan.Sigil),
                    Honor = clan.Honor,
                    Members = members.Count,
                    LeaderName = members
                        .Where(member => member.Rank == YautjaRank.Leader)
                        .OrderByDescending(member => member.Honor)
                        .FirstOrDefault()?.Name,
                    CanView = YautjaClanHallPolicy.CanViewClanPage(actor, clan.Id),
                };
            })
            .OrderBy(row => standings[row.Id])
            .ToList();

        var viewerRow = snapshots.TryGetValue(Player.UserId, out var self) ? self.Row : null;
        var state = new YautjaClanHallEuiState
        {
            Page = _page,
            Navigate = _navigate,
            Viewer = new YautjaClanHallViewer
            {
                PlayerId = Player.UserId,
                Name = Player.Name,
                Rank = actor.Rank,
                ClanId = actor.ClanId,
                Honor = viewerRow?.Honor ?? actor.Member.Honor,
                Staff = actor.Staff,
                Host = actor.Host,
                Council = YautjaClanHallPolicy.IsCouncil(actor),
                CanPetition = YautjaClanHallPolicy.CanPetition(actor),
                PetitionClanId = petitions.FirstOrDefault(entry => entry.PlayerUserId == Player.UserId.UserId)?.ClanId,
            },
            Clans = clanRows,
            Status = _status,
            StatusIsError = _statusIsError,
            Revision = ++_revision,
        };
        _navigate = false;

        // the clan page: whatever was opened last, else your own, else the top clan you're allowed to see
        var clanId = _clanId ?? actor.ClanId ?? clanRows.FirstOrDefault(row => row.CanView)?.Id;
        if (clanId is { } shownClanId &&
            clans.FirstOrDefault(clan => clan.Id == shownClanId) is { } shown &&
            YautjaClanHallPolicy.CanViewClanPage(actor, shownClanId))
        {
            _clanId = shownClanId;
            state.Clan = ClanPage(actor, shown, standings[shown.Id], hunters, petitions, snapshots);
        }
        else
        {
            _clanId = null;
        }

        state.Hunter = await Dossier(actor, _hunterId ?? Player.UserId, snapshots);

        if (actor.Staff)
        {
            state.Whitelist = board
                .Where(row => row.WhitelistFlags != (int) YautjaWhitelistFlags.None)
                .Select(row =>
                {
                    var id = new NetUserId(row.PlayerUserId);
                    return new YautjaClanHallWhitelistRow
                    {
                        PlayerId = id,
                        Name = NameOf(id, row.Name),
                        Flags = (YautjaWhitelistFlags) row.WhitelistFlags,
                        Online = _players.TryGetSessionById(id, out _),
                    };
                })
                .OrderByDescending(row => row.Flags)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (actor.Host)
            state.Round = RoundState();

        return state;
    }

    private YautjaClanHallHunterRow Row(YautjaClanMemberSnapshot snapshot, string name)
    {
        return new YautjaClanHallHunterRow
        {
            PlayerId = snapshot.PlayerId,
            Name = NameOf(snapshot.PlayerId, name),
            ClanId = snapshot.ClanId,
            Rank = snapshot.Rank,
            Honor = snapshot.Honor,
            Online = _players.TryGetSessionById(snapshot.PlayerId, out _),
        };
    }

    private YautjaClanHallClanPage ClanPage(
        YautjaClanHallActor actor,
        YautjaClanRecord clan,
        int standing,
        List<YautjaClanHallHunterRow> hunters,
        List<YautjaClanPetitionRecord> petitions,
        Dictionary<NetUserId, (YautjaClanMemberSnapshot Snapshot, YautjaHunterBoardRecord Row)> snapshots)
    {
        var page = new YautjaClanHallClanPage
        {
            Id = clan.Id,
            Name = clan.Name,
            Description = clan.Description,
            Notice = clan.Notice,
            EliteNotes = YautjaClanHallPolicy.CanSeeEliteNotes(actor, clan.Id) ? clan.EliteNotes : null,
            Color = clan.Color,
            Sigil = YautjaClanSigils.OrDefault(clan.Sigil),
            Honor = clan.Honor,
            Standing = standing,
            Members = hunters
                .Where(hunter => hunter.ClanId == clan.Id)
                .OrderByDescending(hunter => hunter.Rank)
                .ThenByDescending(hunter => hunter.Honor)
                .ThenBy(hunter => hunter.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CanEditNotice = YautjaClanHallPolicy.CanEditNotice(actor, clan.Id),
            CanEditEliteNotes = YautjaClanHallPolicy.CanEditEliteNotes(actor, clan.Id),
            CanEditPresentation = YautjaClanHallPolicy.CanEditPresentation(actor, clan.Id),
            CanRename = YautjaClanHallPolicy.CanRename(actor),
            CanSetHonor = YautjaClanHallPolicy.CanSetHonor(actor),
            CanDisband = YautjaClanHallPolicy.CanFoundOrDisband(actor),
        };

        if (YautjaClanHallPolicy.CanAnswerPetitions(actor, clan.Id))
        {
            page.Petitions = petitions
                .Where(petition => petition.ClanId == clan.Id)
                .Select(petition =>
                {
                    var id = new NetUserId(petition.PlayerUserId);
                    return snapshots.TryGetValue(id, out var entry)
                        ? Row(entry.Snapshot, entry.Row.Name)
                        : new YautjaClanHallHunterRow { PlayerId = id, Name = NameOf(id, string.Empty) };
                })
                .ToList();
        }

        return page;
    }

    private async Task<YautjaClanHallDossier?> Dossier(
        YautjaClanHallActor actor,
        NetUserId id,
        Dictionary<NetUserId, (YautjaClanMemberSnapshot Snapshot, YautjaHunterBoardRecord Row)> snapshots)
    {
        YautjaClanMemberSnapshot target;
        string name;
        YautjaWhitelistFlags flags;
        if (snapshots.TryGetValue(id, out var entry))
        {
            target = entry.Snapshot;
            name = entry.Row.Name;
            flags = (YautjaWhitelistFlags) entry.Row.WhitelistFlags;
        }
        else if (actor.Staff || id == Player.UserId)
        {
            // not on the board yet, staff opened them to whitelist, or it's a fresh hunter's own page
            target = await _clans.ResolveSnapshot(id);
            name = (await _db.GetPlayerRecordByUserId(id))?.LastSeenUserName ?? string.Empty;
            flags = (YautjaWhitelistFlags) await _db.GetYautjaWhitelistFlagsAsync(id.UserId);
        }
        else
        {
            return null;
        }

        if (id == Player.UserId)
            target = actor.Member;

        return new YautjaClanHallDossier
        {
            PlayerId = id,
            Name = NameOf(id, name),
            Rank = target.Rank,
            ClanId = target.ClanId,
            Honor = target.Honor,
            Online = _players.TryGetSessionById(id, out _),
            IsSelf = id == Player.UserId,
            Whitelist = actor.Staff ? flags : null,
            AssignableRanks = YautjaClanHallPolicy.AssignableRanks(actor, target).ToList(),
            CanExile = YautjaClanHallPolicy.CanExile(actor, target),
            CanMove = YautjaClanHallPolicy.CanMove(actor, target),
            CanSetAncient = YautjaClanHallPolicy.CanSetAncient(actor, target),
            IsAncient = target.Rank == YautjaRank.Ancient,
            CanPurge = YautjaClanHallPolicy.CanPurge(actor, target),
        };
    }

    private YautjaClanHallRoundState RoundState()
    {
        var round = _systems.GetEntitySystem<YautjaPredatorRoundSystem>();
        var initialized = round.TryGetActiveHunterSlots(out var activeSlots);
        var slots = round.ConfiguredHunterSlots;
        if (slots <= 0)
            slots = activeSlots > 0 ? activeSlots : 2;

        return new YautjaClanHallRoundState
        {
            RoundId = round.CurrentRoundId,
            RoundActive = round.RoundActive,
            Initialized = initialized,
            ActiveHunterSlots = activeSlots,
            HunterSlots = slots,
            RandomEnabled = round.RandomEnabled,
            RandomMinimumRounds = round.RandomMinimumRounds,
            RandomMaximumRounds = round.RandomMaximumRounds,
            RandomRoundsRemaining = round.RandomRoundsRemaining,
        };
    }
}
