using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Administration.Tickets;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Administration.Tickets;

public enum CMUTicketActionResult : byte
{
    Success,
    NoTicket,
    AlreadyClosed,
    AlreadyClaimed,
    NotClaimed,
    EmptyReason,
    AlreadyAcknowledged,
    AlreadyAnswered,
}

/// <summary>
/// Round-scoped AHelp tickets. A player's first message opens a ticket, later messages append to it,
/// and a message after it was closed opens a new one. Everything lives in memory and is dropped on
/// round restart. Only Adminhelp admins may acknowledge, claim, unclaim or close, checked on every request.
/// </summary>
public sealed partial class CMUTicketSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private BwoinkSystem _bwoink = default!;

    /// <summary>
    /// Lines longer than this are stored as a placeholder instead. Cutting markup could leave an
    /// unclosed tag that the client refuses to render.
    /// </summary>
    public const int MaxStoredTextLength = 4000;

    private static readonly Color EchoColor = Color.LightSkyBlue;
    private static readonly Color AdminNameColor = Color.Red;

    private readonly Dictionary<NetUserId, CMUTicketInfo> _tickets = new();
    private readonly Dictionary<NetUserId, List<CMUTicketMessageEntry>> _history = new();
    private int _nextId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CMUTicketSnapshotRequestEvent>(OnSnapshotRequest);
        SubscribeNetworkEvent<CMUTicketAcknowledgeRequestEvent>(OnAcknowledgeRequest);
        SubscribeNetworkEvent<CMUTicketClaimRequestEvent>(OnClaimRequest);
        SubscribeNetworkEvent<CMUTicketUnclaimRequestEvent>(OnUnclaimRequest);
        SubscribeNetworkEvent<CMUTicketCloseRequestEvent>(OnCloseRequest);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        _adminManager.OnPermsChanged += OnPermsChanged;
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _adminManager.OnPermsChanged -= OnPermsChanged;
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public CMUTicketInfo? GetTicket(NetUserId player)
    {
        return _tickets.GetValueOrDefault(player);
    }

    public IReadOnlyList<CMUTicketMessageEntry> GetHistory(NetUserId player)
    {
        return _history.TryGetValue(player, out var list) ? list : Array.Empty<CMUTicketMessageEntry>();
    }

    /// <summary>
    /// Called by <see cref="BwoinkSystem"/> once a bwoink passed its auth and rate limit checks.
    /// <paramref name="adminMessage"/> is the line exactly as admins receive it.
    /// </summary>
    public void OnMessageAccepted(NetUserId channel, ICommonSession sender, SharedBwoinkSystem.BwoinkTextMessage adminMessage)
    {
        RecordMessage(channel,
            sender.UserId,
            sender.Name,
            adminMessage.Text,
            adminMessage.AdminOnly,
            adminMessage.PlaySound,
            adminMessage.SentAt);
    }

    /// <summary>
    /// Stores one channel line, opening a ticket first when the player writes in their own channel
    /// with no open ticket. An admin's first player-visible reply into an open ticket marks it answered.
    /// Returns the ticket that was opened, if any.
    /// </summary>
    public CMUTicketInfo? RecordMessage(
        NetUserId channel,
        NetUserId sender,
        string senderName,
        string text,
        bool adminOnly,
        bool playSound,
        DateTime sentAt)
    {
        CMUTicketInfo? opened = null;
        if (sender == channel &&
            !adminOnly &&
            (!_tickets.TryGetValue(channel, out var existing) || existing.Status == CMUTicketStatus.Closed))
        {
            opened = OpenTicket(channel, senderName, sentAt);
        }
        else if (sender != channel &&
                 !adminOnly &&
                 sender != SharedBwoinkSystem.SystemUserId &&
                 _tickets.TryGetValue(channel, out var ticket) &&
                 ticket is { Status: CMUTicketStatus.Open, AnsweredBy: null })
        {
            ticket = ticket with
            {
                AnsweredBy = sender,
                AnsweredByName = senderName,
            };
            _tickets[channel] = ticket;
            BroadcastUpdate(ticket);
        }

        AppendHistory(channel, new CMUTicketMessageEntry(sentAt, sender, text, adminOnly, playSound));
        return opened;
    }

    /// <summary>
    /// Stores a system line BwoinkSystem showed admins outside the ticket flow, such as the player
    /// connecting or leaving, so a replayed channel matches the live one. Channels without history are
    /// skipped; the line alone is not worth a channel.
    /// </summary>
    public void RecordSystemLine(NetUserId channel, SharedBwoinkSystem.BwoinkTextMessage message)
    {
        if (!_history.ContainsKey(channel))
            return;

        AppendHistory(channel, new CMUTicketMessageEntry(message.SentAt, message.TrueSender, message.Text, message.AdminOnly, false));
    }

    /// <summary>
    /// Sends the player the canned "seen, will be handled" reply from <paramref name="admin"/> and claims
    /// the ticket for them. Only an open, unclaimed ticket no admin has answered yet can be acknowledged, and only once.
    /// Does not check permissions; network requests are checked before reaching here.
    /// </summary>
    public CMUTicketActionResult TryAcknowledge(NetUserId player, NetUserId admin, string adminName)
    {
        if (!_tickets.TryGetValue(player, out var ticket))
            return CMUTicketActionResult.NoTicket;

        if (ticket.Status == CMUTicketStatus.Closed)
            return CMUTicketActionResult.AlreadyClosed;

        if (ticket.Status == CMUTicketStatus.Claimed)
            return CMUTicketActionResult.AlreadyClaimed;

        if (ticket.AcknowledgedBy != null)
            return CMUTicketActionResult.AlreadyAcknowledged;

        if (ticket.AnsweredBy != null)
            return CMUTicketActionResult.AlreadyAnswered;

        // Acknowledging also claims, so the acknowledging admin owns the follow-up. The canned line
        // already carries their name when names are shown, so no separate claim notice is sent.
        ticket = ticket with
        {
            Status = CMUTicketStatus.Claimed,
            ClaimedBy = admin,
            ClaimedByName = adminName,
            AcknowledgedBy = admin,
            AcknowledgedByName = adminName,
        };
        _tickets[player] = ticket;

        SendAcknowledgeLine(player, admin, adminName);
        AdminEcho(player, Loc.GetString("cmu-ahelp-ticket-admin-acknowledged", ("id", ticket.Id), ("admin", adminName)), adminName);
        _adminLog.Add(LogType.AdminMessage, LogImpact.Low,
            $"{adminName:admin} acknowledged and claimed AHelp ticket #{ticket.Id} of {ticket.PlayerName:player}");
        BroadcastUpdate(ticket);
        return CMUTicketActionResult.Success;
    }

    /// <summary>
    /// Claims the ticket for <paramref name="admin"/>. Claiming a ticket someone else holds takes it over.
    /// An acknowledged ticket stays acknowledged.
    /// Does not check permissions; network requests are checked before reaching here.
    /// </summary>
    public CMUTicketActionResult TryClaim(NetUserId player, NetUserId admin, string adminName)
    {
        if (!_tickets.TryGetValue(player, out var ticket))
            return CMUTicketActionResult.NoTicket;

        if (ticket.Status == CMUTicketStatus.Closed)
            return CMUTicketActionResult.AlreadyClosed;

        if (ticket.ClaimedBy == admin)
            return CMUTicketActionResult.AlreadyClaimed;

        var previous = ticket.ClaimedBy != null ? ticket.ClaimedByName : null;
        ticket = ticket with
        {
            Status = CMUTicketStatus.Claimed,
            ClaimedBy = admin,
            ClaimedByName = adminName,
        };
        _tickets[player] = ticket;

        var plain = previous == null
            ? Loc.GetString("cmu-ahelp-ticket-admin-claimed", ("id", ticket.Id), ("admin", adminName))
            : Loc.GetString("cmu-ahelp-ticket-admin-takeover", ("id", ticket.Id), ("admin", adminName), ("previous", previous));
        AdminEcho(player, plain, adminName);

        SendToPlayer(player, BuildPlayerClaimText(admin, adminName));

        var takenFrom = previous ?? string.Empty;
        _adminLog.Add(LogType.AdminMessage, LogImpact.Low,
            $"{adminName:admin} claimed AHelp ticket #{ticket.Id} of {ticket.PlayerName:player} (previous claimer: {takenFrom})");
        BroadcastUpdate(ticket);
        return CMUTicketActionResult.Success;
    }

    /// <summary>
    /// Returns a claimed ticket to the open queue. Any Adminhelp admin may release any claim.
    /// </summary>
    public CMUTicketActionResult TryUnclaim(NetUserId player, NetUserId admin, string adminName)
    {
        if (!_tickets.TryGetValue(player, out var ticket))
            return CMUTicketActionResult.NoTicket;

        if (ticket.Status == CMUTicketStatus.Closed)
            return CMUTicketActionResult.AlreadyClosed;

        if (ticket.ClaimedBy == null)
            return CMUTicketActionResult.NotClaimed;

        var claimer = ticket.ClaimedByName ?? string.Empty;
        var plain = ticket.ClaimedBy == admin
            ? Loc.GetString("cmu-ahelp-ticket-admin-unclaimed", ("id", ticket.Id), ("admin", adminName))
            : Loc.GetString("cmu-ahelp-ticket-admin-unclaimed-other", ("id", ticket.Id), ("admin", adminName), ("previous", claimer));

        ticket = ReleaseClaim(ticket);
        AdminEcho(player, plain, adminName);
        _adminLog.Add(LogType.AdminMessage, LogImpact.Low,
            $"{adminName:admin} unclaimed AHelp ticket #{ticket.Id} of {ticket.PlayerName:player} (was {claimer})");
        BroadcastUpdate(ticket);
        return CMUTicketActionResult.Success;
    }

    /// <summary>
    /// Closes the ticket. The trimmed reason must not be empty; it is sent to the player and logged.
    /// </summary>
    public CMUTicketActionResult TryClose(NetUserId player, NetUserId admin, string adminName, string reason)
    {
        reason = reason.Trim();
        if (reason.Length == 0)
            return CMUTicketActionResult.EmptyReason;

        if (reason.Length > CMUTicketLimits.MaxCloseReasonLength)
            reason = reason[..CMUTicketLimits.MaxCloseReasonLength];

        if (!_tickets.TryGetValue(player, out var ticket))
            return CMUTicketActionResult.NoTicket;

        if (ticket.Status == CMUTicketStatus.Closed)
            return CMUTicketActionResult.AlreadyClosed;

        ticket = ticket with
        {
            Status = CMUTicketStatus.Closed,
            ClosedAt = DateTime.Now,
            ClosedByName = adminName,
            CloseReason = reason,
        };
        _tickets[player] = ticket;

        AdminEcho(player,
            Loc.GetString("cmu-ahelp-ticket-admin-closed", ("id", ticket.Id), ("admin", adminName), ("reason", reason)),
            adminName);
        SendToPlayer(player, Loc.GetString("cmu-ahelp-ticket-player-closed", ("reason", reason)));

        _adminLog.Add(LogType.AdminMessage, LogImpact.Low,
            $"{adminName:admin} closed AHelp ticket #{ticket.Id} of {ticket.PlayerName:player}: {reason}");
        BroadcastUpdate(ticket);
        return CMUTicketActionResult.Success;
    }

    /// <summary>
    /// Returns every ticket <paramref name="admin"/> holds to the open queue, e.g. when they leave.
    /// </summary>
    public void ReleaseClaimsBy(NetUserId admin, string adminName)
    {
        foreach (var ticket in _tickets.Values.Where(t => t.Status == CMUTicketStatus.Claimed && t.ClaimedBy == admin).ToList())
        {
            var released = ReleaseClaim(ticket);
            AdminEcho(ticket.Player,
                Loc.GetString("cmu-ahelp-ticket-admin-released", ("id", ticket.Id), ("admin", adminName)),
                adminName);
            _adminLog.Add(LogType.AdminMessage, LogImpact.Low,
                $"AHelp ticket #{ticket.Id} of {ticket.PlayerName:player} was released because {adminName:admin} left or lost Adminhelp");
            BroadcastUpdate(released);
        }
    }

    /// <summary>
    /// Drops all tickets and history. Admins are sent an empty snapshot.
    /// </summary>
    public void Reset()
    {
        _tickets.Clear();
        _history.Clear();
        _nextId = 1;

        foreach (var admin in GetTicketAdmins())
        {
            SendSnapshot(admin);
        }
    }

    /// <summary>
    /// Every ticket, plus the history of at most <see cref="CMUTicketLimits.MaxSnapshotChannels"/> ticket
    /// channels: open and claimed tickets first, then the most recently active. Channels that never had a
    /// ticket this round are left out; admins online at the time saw them live.
    /// </summary>
    public CMUTicketSnapshotEvent BuildSnapshot()
    {
        var channels = _tickets.Values
            .Where(t => _history.ContainsKey(t.Player))
            .OrderBy(t => t.Status == CMUTicketStatus.Closed)
            .ThenByDescending(t => LastActivity(t))
            .Take(CMUTicketLimits.MaxSnapshotChannels);

        var history = new Dictionary<NetUserId, List<CMUTicketMessageEntry>>();
        foreach (var ticket in channels)
        {
            history[ticket.Player] = new List<CMUTicketMessageEntry>(_history[ticket.Player]);
        }

        return new CMUTicketSnapshotEvent(_tickets.Values.ToList(), history);
    }

    private DateTime LastActivity(CMUTicketInfo ticket)
    {
        return _history.TryGetValue(ticket.Player, out var lines) && lines.Count > 0
            ? lines[^1].SentAt
            : ticket.OpenedAt;
    }

    private CMUTicketInfo OpenTicket(NetUserId player, string playerName, DateTime openedAt)
    {
        var ticket = new CMUTicketInfo(
            _nextId++,
            player,
            playerName,
            CMUTicketStatus.Open,
            null,
            null,
            null,
            null,
            null,
            null,
            openedAt,
            null,
            null,
            null);
        _tickets[player] = ticket;

        AdminEcho(player, Loc.GetString("cmu-ahelp-ticket-admin-opened", ("id", ticket.Id), ("player", playerName)), null);
        _adminLog.Add(LogType.AdminMessage, LogImpact.Low, $"AHelp ticket #{ticket.Id} opened for {playerName:player}");
        BroadcastUpdate(ticket);
        return ticket;
    }

    private CMUTicketInfo ReleaseClaim(CMUTicketInfo ticket)
    {
        ticket = ticket with
        {
            Status = CMUTicketStatus.Open,
            ClaimedBy = null,
            ClaimedByName = null,
        };
        _tickets[ticket.Player] = ticket;
        return ticket;
    }

    private void AppendHistory(NetUserId channel, CMUTicketMessageEntry entry)
    {
        if (entry.Text.Length > MaxStoredTextLength)
            entry = entry with { Text = FormattedMessage.EscapeText(Loc.GetString("cmu-ahelp-ticket-history-too-long")) };

        var lines = _history.GetOrNew(channel);
        lines.Add(entry);
        if (lines.Count > CMUTicketLimits.MaxHistoryPerChannel)
            lines.RemoveRange(0, lines.Count - CMUTicketLimits.MaxHistoryPerChannel);
    }

    /// <summary>
    /// Shows a ticket status line in the player's channel to every Adminhelp admin, stores it in the
    /// channel history and queues it on the Discord relay.
    /// </summary>
    private void AdminEcho(NetUserId channel, string plainText, string? adminName)
    {
        var text = $"[color={EchoColor.ToHex()}]{FormattedMessage.EscapeText(plainText)}[/color]";
        var message = new SharedBwoinkSystem.BwoinkTextMessage(
            channel,
            SharedBwoinkSystem.SystemUserId,
            text,
            DateTime.Now,
            playSound: false,
            adminOnly: true);

        AppendHistory(channel, new CMUTicketMessageEntry(message.SentAt, message.TrueSender, text, true, false));
        foreach (var admin in GetTicketAdmins())
        {
            RaiseNetworkEvent(message, admin.Channel);
        }

        _bwoink.CMURelayTicketLine(channel, adminName ?? Loc.GetString("cmu-ahelp-ticket-discord-system"), plainText);
    }

    /// <summary>
    /// The claim notice the player sees. Names the claimer only as <see cref="PlayerFacingAdminName"/> allows.
    /// </summary>
    public string BuildPlayerClaimText(NetUserId admin, string adminName)
    {
        return PlayerFacingAdminName(admin, adminName) is { } name
            ? Loc.GetString("cmu-ahelp-ticket-player-claimed-named", ("admin", name))
            : Loc.GetString("cmu-ahelp-ticket-player-claimed");
    }

    /// <summary>
    /// The admin name a player may see for a ticket action, or null when it must stay anonymous: when
    /// <see cref="CCVars.CMUAhelpTicketShowClaimer"/> is off or the admin is stealthed. Otherwise it
    /// respects the AHelp name override like a normal admin reply.
    /// </summary>
    private string? PlayerFacingAdminName(NetUserId admin, string adminName)
    {
        if (!_config.GetCVar(CCVars.CMUAhelpTicketShowClaimer))
            return null;

        if (_playerManager.TryGetSessionById(admin, out var session) &&
            _adminManager.GetAdminData(session, includeDeAdmin: true)?.Stealth == true)
        {
            return null;
        }

        return _config.GetCVar(CCVars.AdminAhelpOverrideClientName) is { Length: > 0 } overrideName
            ? overrideName
            : adminName;
    }

    /// <summary>
    /// Sends the canned acknowledge reply as an admin line from <paramref name="admin"/>. Admins see the
    /// real name and the line is stored in history; the player sees the player-facing name.
    /// </summary>
    private void SendAcknowledgeLine(NetUserId player, NetUserId admin, string adminName)
    {
        var body = FormattedMessage.EscapeText(Loc.GetString("cmu-ahelp-ticket-acknowledge-text"));
        var adminText = $"[color={AdminNameColor.ToHex()}]{FormattedMessage.EscapeText(adminName)}[/color]: {body}";
        var adminMessage = new SharedBwoinkSystem.BwoinkTextMessage(player, admin, adminText, DateTime.Now, playSound: false);
        AppendHistory(player, new CMUTicketMessageEntry(adminMessage.SentAt, admin, adminText, false, false));

        var ticketAdmins = GetTicketAdmins().ToList();
        foreach (var ticketAdmin in ticketAdmins)
        {
            RaiseNetworkEvent(adminMessage, ticketAdmin.Channel);
        }

        // Like a normal reply, a player who is an Adminhelp admin already got the admin copy.
        if (!_playerManager.TryGetSessionById(player, out var session) || ticketAdmins.Contains(session))
            return;

        var playerName = PlayerFacingAdminName(admin, adminName) ?? Loc.GetString("cmu-ahelp-ticket-acknowledge-anonymous");
        var playerText = $"[color={AdminNameColor.ToHex()}]{FormattedMessage.EscapeText(playerName)}[/color]: {body}";
        RaiseNetworkEvent(new SharedBwoinkSystem.BwoinkTextMessage(player, admin, playerText, adminMessage.SentAt),
            session.Channel);
    }

    private void SendToPlayer(NetUserId player, string plainText)
    {
        if (!_playerManager.TryGetSessionById(player, out var session))
            return;

        var text = $"[color={EchoColor.ToHex()}]{FormattedMessage.EscapeText(plainText)}[/color]";
        RaiseNetworkEvent(new SharedBwoinkSystem.BwoinkTextMessage(player, SharedBwoinkSystem.SystemUserId, text),
            session.Channel);
    }

    private void SendFailure(ICommonSession admin, NetUserId player, CMUTicketActionResult result, bool acknowledging = false)
    {
        var locId = result switch
        {
            CMUTicketActionResult.AlreadyClaimed when acknowledging => "cmu-ahelp-ticket-error-acknowledge-claimed",
            CMUTicketActionResult.AlreadyAcknowledged => "cmu-ahelp-ticket-error-already-acknowledged",
            CMUTicketActionResult.AlreadyAnswered => "cmu-ahelp-ticket-error-already-answered",
            CMUTicketActionResult.NoTicket => "cmu-ahelp-ticket-error-no-ticket",
            CMUTicketActionResult.AlreadyClosed => "cmu-ahelp-ticket-error-closed",
            CMUTicketActionResult.AlreadyClaimed => "cmu-ahelp-ticket-error-already-claimed",
            CMUTicketActionResult.NotClaimed => "cmu-ahelp-ticket-error-not-claimed",
            CMUTicketActionResult.EmptyReason => "cmu-ahelp-ticket-error-empty-reason",
            _ => null,
        };

        if (locId == null)
            return;

        var text = $"[color={Color.Orange.ToHex()}]{FormattedMessage.EscapeText(Loc.GetString(locId))}[/color]";
        RaiseNetworkEvent(new SharedBwoinkSystem.BwoinkTextMessage(player, SharedBwoinkSystem.SystemUserId, text, playSound: false),
            admin.Channel);
    }

    private bool IsTicketAdmin(ICommonSession session)
    {
        return _adminManager.GetAdminData(session)?.HasFlag(AdminFlags.Adminhelp) ?? false;
    }

    private IEnumerable<ICommonSession> GetTicketAdmins()
    {
        return _adminManager.ActiveAdmins.Where(IsTicketAdmin);
    }

    private void BroadcastUpdate(CMUTicketInfo ticket)
    {
        var ev = new CMUTicketUpdatedEvent(ticket);
        foreach (var admin in GetTicketAdmins())
        {
            RaiseNetworkEvent(ev, admin.Channel);
        }
    }

    private void SendSnapshot(ICommonSession admin)
    {
        RaiseNetworkEvent(BuildSnapshot(), admin.Channel);
    }

    private void OnSnapshotRequest(CMUTicketSnapshotRequestEvent ev, EntitySessionEventArgs args)
    {
        if (IsTicketAdmin(args.SenderSession))
            SendSnapshot(args.SenderSession);
    }

    private void OnAcknowledgeRequest(CMUTicketAcknowledgeRequestEvent ev, EntitySessionEventArgs args)
    {
        var admin = args.SenderSession;
        if (!IsTicketAdmin(admin))
            return;

        SendFailure(admin, ev.Player, TryAcknowledge(ev.Player, admin.UserId, admin.Name), acknowledging: true);
    }

    private void OnClaimRequest(CMUTicketClaimRequestEvent ev, EntitySessionEventArgs args)
    {
        var admin = args.SenderSession;
        if (!IsTicketAdmin(admin))
            return;

        SendFailure(admin, ev.Player, TryClaim(ev.Player, admin.UserId, admin.Name));
    }

    private void OnUnclaimRequest(CMUTicketUnclaimRequestEvent ev, EntitySessionEventArgs args)
    {
        var admin = args.SenderSession;
        if (!IsTicketAdmin(admin))
            return;

        SendFailure(admin, ev.Player, TryUnclaim(ev.Player, admin.UserId, admin.Name));
    }

    private void OnCloseRequest(CMUTicketCloseRequestEvent ev, EntitySessionEventArgs args)
    {
        var admin = args.SenderSession;
        if (!IsTicketAdmin(admin))
            return;

        SendFailure(admin, ev.Player, TryClose(ev.Player, admin.UserId, admin.Name, ev.Reason));
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        Reset();
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        var hasAdminhelp = args.Flags is { } flags && (flags & AdminFlags.Adminhelp) != 0;
        if (!hasAdminhelp)
        {
            ReleaseClaimsBy(args.Player.UserId, args.Player.Name);
            return;
        }

        // Admins logging in get their snapshot once they reach InGame instead.
        if (args.Player.Status == SessionStatus.InGame)
            SendSnapshot(args.Player);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        switch (args.NewStatus)
        {
            case SessionStatus.InGame when IsTicketAdmin(args.Session):
                SendSnapshot(args.Session);
                break;
            case SessionStatus.Disconnected:
                ReleaseClaimsBy(args.Session.UserId, args.Session.Name);
                break;
        }
    }
}
