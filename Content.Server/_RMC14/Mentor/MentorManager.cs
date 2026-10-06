using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Players.RateLimiting;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Mentor;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Players.RateLimiting;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Mentor;

public sealed partial class MentorManager : IPostInjectInit
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private PlayerRateLimitManager _rateLimit = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserDbDataManager _userDb = default!;

    private const string RateLimitKey = "MentorHelp";
    private static readonly ProtoId<JobPrototype> MentorJob = "AU14JobGOVFORadvisor";

    private GameTicker _ticker = default!;
    private const ushort MessageLengthCap = 3500;
    private const string TooLongText = "... **(msg too long)**";

    private readonly ConcurrentQueue<WebhookPayload> _pendingPayloads = new();
    private string _mentorHelpWebhookUrl = string.Empty;
    private readonly HttpClient _httpClient = new();
    private int _queueProcessorStarted;

    private readonly HashSet<ICommonSession> _activeMentors = new();
    private readonly Dictionary<NetUserId, bool> _mentors = new();
    private readonly Dictionary<NetUserId, (TimeSpan Timestamp, bool Typing)> _typingUpdateTimestamps = new();
    private readonly Dictionary<NetUserId, List<NetUserId>> _destinationClaims = new();
    private readonly Dictionary<NetUserId, HashSet<NetUserId>> _mentorClaims = new();

    private sealed class WebhookPayload
    {
        [JsonPropertyName("username")] public string? Username { get; set; }
        [JsonPropertyName("embeds")] public List<WebhookEmbed>? Embeds { get; set; } = new();
    }

    private sealed class WebhookEmbed
    {
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("color")] public int? Color { get; set; }
        [JsonPropertyName("footer")] public WebhookEmbedFooter? Footer { get; set; }
    }

    private sealed class WebhookEmbedFooter
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private async Task LoadData(ICommonSession player, CancellationToken cancel)
    {
        var userId = player.UserId;
        var isMentor = await _db.IsJobWhitelisted(player.UserId, MentorJob, cancel);

        if (!isMentor)
        {
            var dbData = await _db.GetAdminDataForAsync(userId, cancel);
            var flags = AdminFlags.None;
            if (dbData?.AdminRank?.Flags != null)
            {
                flags |= AdminFlagsHelper.NamesToFlags(dbData.AdminRank.Flags.Select(p => p.Flag));
            }

            if (dbData?.Flags != null)
            {
                flags |= AdminFlagsHelper.NamesToFlags(dbData.Flags.Select(p => p.Flag));
            }

            isMentor = flags.HasFlag(AdminFlags.MentorHelp);
        }

        _mentors[player.UserId] = isMentor;

        if (isMentor)
            _activeMentors.Add(player);
    }

    private void FinishLoad(ICommonSession player)
    {
        SendMentorStatus(player);
    }

    private void ClientDisconnected(ICommonSession player)
    {
        _mentors.Remove(player.UserId);
        _activeMentors.Remove(player);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        var user = args.Session;
        _typingUpdateTimestamps.Remove(user.UserId);

        if (_destinationClaims.TryGetValue(user.UserId, out var mentors))
        {
            foreach (var mentor in mentors.ToArray())
            {
                if (_player.TryGetSessionById(mentor, out var mentorSession))
                    Unclaim(mentorSession.Channel, user.UserId, true);
            }
        }

        if (!_mentorClaims.TryGetValue(user.UserId, out var destinations)) return;
        foreach (var destination in destinations.ToArray())
        {
            Unclaim(user.Channel, destination, true);
        }
    }

    private void OnMentorSendMessage(MentorSendMessageMsg message)
    {
        var destination = new NetUserId(message.To);
        if (!_player.TryGetSessionById(destination, out var destinationSession))
            return;

        var author = message.MsgChannel;
        if (!_player.TryGetSessionById(author.UserId, out var authorSession) ||
            !_activeMentors.Contains(authorSession))
        {
            return;
        }

        SendMentorMessage(
            destination,
            destinationSession.Name,
            authorSession,
            authorSession.Name,
            message.Message,
            destinationSession.Channel
        );
    }

    private void OnMentorHelpClientMessage(MentorHelpClientMsg message)
    {
        if (!_player.TryGetSessionById(message.MsgChannel.UserId, out var author))
            return;

        SendMentorMessage(author.UserId, author.Name, author, author.Name, message.Message, message.MsgChannel);
    }

    private void OnDeMentor(DeMentorMsg message)
    {
        DeMentor(message.MsgChannel);
    }

    private void OnReMentor(ReMentorMsg message)
    {
        ReMentor(message.MsgChannel.UserId);
    }

    private void OnClientTypingUpdated(MentorHelpClientTypingUpdatedMsg msg)
    {
        var author = msg.MsgChannel;
        var authorId = author.UserId;
        if (_typingUpdateTimestamps.TryGetValue(authorId, out var tuple) &&
            tuple.Typing == msg.Typing &&
            tuple.Timestamp + TimeSpan.FromSeconds(1) > _timing.RealTime)
        {
            return;
        }

        _typingUpdateTimestamps[authorId] = (_timing.RealTime, msg.Typing);

        var isMentor = IsMentor(authorId);
        var destination = msg.Destination;
        if (!isMentor && authorId != msg.Destination)
            return;

        SendTypingUpdate(author, destination, msg.Typing);
    }

    private void OnClientClaim(MentorClientClaimMsg message)
    {
        var author = message.MsgChannel;
        var authorId = author.UserId;
        if (!IsMentor(authorId))
            return;

        var destination = new NetUserId(message.Destination);
        if (!_player.TryGetSessionById(destination, out var destinationSession))
            return;

        var claims = _destinationClaims.GetOrNew(destination);
        if (claims.Contains(authorId))
            return;

        claims.Add(authorId);
        _mentorClaims.GetOrNew(authorId).Add(destination);

        var claim = new MentorClaimMsg
        {
            Author = author.UserName,
            Destination = destination,
        };

        var isAdmin = false;
        if (_player.TryGetSessionById(author.UserId, out var authorSession))
            isAdmin = _admin.IsAdmin(authorSession);

        var mentorMsg = new MentorMessage(
            destination,
            destinationSession.Name,
            null,
            null,
            $"SERVER: {author.UserName} has claimed this mentor help",
            DateTime.Now,
            true,
            isAdmin,
            true
        );
        var messages = new List<MentorMessage> { mentorMsg };
        var receive = new MentorMessagesReceivedMsg { Messages = messages };

        foreach (var mentor in _activeMentors)
        {
            if (!mentor.Channel.IsConnected)
                continue;

            try
            {
                _net.ServerSendMessage(claim, mentor.Channel);
                _net.ServerSendMessage(receive, mentor.Channel);
            }
            catch (Exception e)
            {
                _log.RootSawmill.Error($"Error sending mentor help claim:\n{e}");
            }
        }
    }

    private void OnClientUnclaim(MentorClientUnclaimMsg message)
    {
        var author = message.MsgChannel;
        var authorId = author.UserId;
        if (!IsMentor(authorId))
            return;

        var destination = new NetUserId(message.Destination);
        Unclaim(author, destination, false);
    }

    private void OnConnected(object? sender, NetChannelArgs args)
    {
        try
        {
            var msg = $"SERVER: {args.Channel.UserName} has reconnected to the server.";
            SendMentorMessage(args.Channel.UserId, args.Channel.UserName, null, null, msg, args.Channel, false);
        }
        catch (Exception e)
        {
            _log.RootSawmill.Error($"Error sending mentor help client connected message:{e}");
        }
    }

    private void OnDisconnected(object? sender, NetDisconnectedArgs args)
    {
        try
        {
            var msg = $"SERVER: {args.Channel.UserName} has disconnected.";
            SendMentorMessage(args.Channel.UserId, args.Channel.UserName, null, null, msg, args.Channel, false);
        }
        catch (Exception e)
        {
            _log.RootSawmill.Error($"Error sending mentor help client disconnect message:{e}");
        }
    }

    private void OnMentorTeleport(MentorClientTeleportMsg message)
    {
        var author = message.MsgChannel;
        if (!_player.TryGetSessionById(author.UserId, out var authorSession) ||
            !_activeMentors.Contains(authorSession))
        {
            return;
        }

        if (authorSession.AttachedEntity is not { } mentorEntity)
            return;

        var destination = new NetUserId(message.Destination);
        if (!_player.TryGetSessionById(destination, out var targetSession) || targetSession.AttachedEntity is not { } targetEntity)
            return;

        var ev = new MentorFollowEvent(_entMan.GetNetEntity(mentorEntity), _entMan.GetNetEntity(targetEntity));
        _entMan.EventBus.RaiseLocalEvent(mentorEntity, ref ev);
    }

    private void Unclaim(INetChannel author, NetUserId destination, bool disconnect)
    {
        if (!_destinationClaims.TryGetValue(destination, out var claims))
            return;

        if (!_player.TryGetPlayerData(destination, out var destinationData))
            return;

        var userId = author.UserId;
        claims.Remove(userId);
        if (claims.Count == 0)
            _destinationClaims.Remove(destination);

        _mentorClaims.GetValueOrDefault(userId)?.Remove(destination);
        var msg = new MentorUnclaimMsg
        {
            Author = author.UserName,
            Destination = destination,
            Disconnect = disconnect,
        };

        var isAdmin = false;
        if (_player.TryGetSessionById(author.UserId, out var authorSession))
            isAdmin = _admin.IsAdmin(authorSession);

        var mentorMsg = new MentorMessage(
            destination,
            destinationData.UserName,
            null,
            null,
            $"SERVER: {author.UserName} has given up their claim for this mentor help",
            DateTime.Now,
            true,
            isAdmin,
            true
        );
        var messages = new List<MentorMessage> { mentorMsg };
        var receive = new MentorMessagesReceivedMsg { Messages = messages };

        foreach (var mentor in _activeMentors)
        {
            if (!mentor.Channel.IsConnected)
                continue;

            try
            {
                _net.ServerSendMessage(msg, mentor.Channel);
                _net.ServerSendMessage(receive, mentor.Channel);
            }
            catch (Exception e)
            {
                _log.RootSawmill.Error($"Error sending mentor help unclaim:\n{e}");
            }
        }
    }

    private void SendMentorStatus(ICommonSession player)
    {
        if (!player.Channel.IsConnected)
            return;

        var isMentor = _activeMentors.Contains(player);
        var canReMentor = _mentors.TryGetValue(player.UserId, out var mentor) && mentor;
        var msg = new MentorStatusMsg()
        {
            IsMentor = isMentor,
            CanReMentor = canReMentor,
        };

        _net.ServerSendMessage(msg, player.Channel);
    }

    private void SendMentorMessage(
        NetUserId destination,
        string destinationName,
        ICommonSession? author,
        string? authorName,
        string message,
        INetChannel? destinationChannel,
        bool create = true)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var recipients = new HashSet<INetChannel>();
        if (destinationChannel is { IsConnected: true })
            recipients.Add(destinationChannel);

        var isMentor = false;
        foreach (var active in _activeMentors)
        {
            if (author != null && active.UserId == author.UserId)
                isMentor = true;

            if (active.Channel.IsConnected)
                recipients.Add(active.Channel);
        }

        var isAdmin = author != null && _admin.IsAdmin(author);
        var mentorMsg = new MentorMessage(
            destination,
            destinationName,
            author?.UserId,
            authorName,
            message,
            DateTime.Now,
            isMentor,
            isAdmin,
            create
        );
        var messages = new List<MentorMessage> { mentorMsg };
        var receive = new MentorMessagesReceivedMsg { Messages = messages };
        foreach (var recipient in recipients)
        {
            try
            {
                _net.ServerSendMessage(receive, recipient);
            }
            catch (Exception e)
            {
                _log.RootSawmill.Error($"Error sending mentor help message:\n{e}");
            }
        }

        if (author != null)
            SendTypingUpdate(author.Channel, destination, false);

        if (!create || string.IsNullOrWhiteSpace(_mentorHelpWebhookUrl)) return;
        var cappedMessage = message.Length > MessageLengthCap
            ? message[..(MessageLengthCap - TooLongText.Length)] + TooLongText
            : message;
        var payload = GenerateMentorHelpPayload(destinationName, authorName, cappedMessage);
        _pendingPayloads.Enqueue(payload);
        StartQueueProcessor();
    }

    private void SendTypingUpdate(INetChannel author, Guid destination, bool typing)
    {
        var update = new MentorHelpTypingUpdatedMsg
        {
            Author = author.UserName,
            Destination = destination,
            Typing = typing,
        };

        foreach (var admin in GetActiveMentors())
        {
            if (admin.UserId == author.UserId || !admin.Channel.IsConnected)
                continue;

            _net.ServerSendMessage(update, admin.Channel);
        }
    }

    public bool IsMentor(NetUserId player)
    {
        return _mentors.TryGetValue(player, out var mentor) && mentor;
    }

    public IEnumerable<ICommonSession> GetActiveMentors()
    {
        return _activeMentors;
    }

    public void ReMentor(NetUserId user)
    {
        if (!_player.TryGetSessionById(user, out var session) ||
            !_mentors.TryGetValue(session.UserId, out var mentor) ||
            !mentor)
        {
            return;
        }

        _activeMentors.Add(session);
        SendMentorStatus(session);
    }

    public void DeMentor(INetChannel user)
    {
        if (!_player.TryGetSessionByChannel(user, out var session) ||
            !_activeMentors.Contains(session))
        {
            return;
        }

        _activeMentors.Remove(session);
        SendMentorStatus(session);

        if (!_mentorClaims.TryGetValue(user.UserId, out var claims)) return;
        foreach (var claim in claims.ToArray())
        {
            Unclaim(user, claim, true);
        }
    }

    private void OnMentorHelpWebhookChanged(string url) => _mentorHelpWebhookUrl = url;

    void IPostInjectInit.PostInject()
    {
        _net.RegisterNetMessage<MentorStatusMsg>();
        _net.RegisterNetMessage<MentorSendMessageMsg>(OnMentorSendMessage);
        _net.RegisterNetMessage<MentorHelpClientMsg>(OnMentorHelpClientMessage);
        _net.RegisterNetMessage<MentorMessagesReceivedMsg>();
        _net.RegisterNetMessage<DeMentorMsg>(OnDeMentor);
        _net.RegisterNetMessage<ReMentorMsg>(OnReMentor);
        _net.RegisterNetMessage<MentorHelpClientTypingUpdatedMsg>(OnClientTypingUpdated);
        _net.RegisterNetMessage<MentorHelpTypingUpdatedMsg>();
        _net.RegisterNetMessage<MentorClientClaimMsg>(OnClientClaim);
        _net.RegisterNetMessage<MentorClientUnclaimMsg>(OnClientUnclaim);
        _net.RegisterNetMessage<MentorClaimMsg>();
        _net.RegisterNetMessage<MentorUnclaimMsg>();
        _net.RegisterNetMessage<MentorClientTeleportMsg>(OnMentorTeleport);

        _net.Connected += OnConnected;
        _net.Disconnect += OnDisconnected;

        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);

        if (_config.IsCVarRegistered(CCVars.DiscordMentorHelpWebhook.Name))
            _config.OnValueChanged(CCVars.DiscordMentorHelpWebhook, OnMentorHelpWebhookChanged, true);
        else
            OnMentorHelpWebhookChanged(string.Empty);

        if (_config.IsCVarRegistered(RMCCVars.RMCMentorHelpRateLimitPeriod.Name) &&
            _config.IsCVarRegistered(RMCCVars.RMCMentorHelpRateLimitCount.Name))
        {
            _rateLimit.Register(
                RateLimitKey,
                new RateLimitRegistration(
                    RMCCVars.RMCMentorHelpRateLimitPeriod,
                    RMCCVars.RMCMentorHelpRateLimitCount,
                    _ => { }
                )
            );
        }

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private WebhookPayload GenerateMentorHelpPayload(string destinationName, string? authorName, string text)
    {
        var username = authorName != null ? $"{authorName} → {destinationName}" : $"System → {destinationName}";
        var color = 0xFFA500;

        _ticker ??= _entMan.System<GameTicker>();
        var roundId = _ticker.RoundId;
        var roundState = _ticker.RunLevel switch
        {
            GameRunLevel.PreRoundLobby => "Lobby",
            GameRunLevel.InRound => $"Round {roundId}",
            GameRunLevel.PostRound => $"Post-round {roundId}",
            _ => "Unknown"
        };
        var footerText = $"Mentor Help – {roundState}";

        return new WebhookPayload
        {
            Username = username,
            Embeds =
            [
                new()
                {
                    Description = text,
                    Color = color,
                    Footer = new WebhookEmbedFooter { Text = footerText }
                }
            ]
        };
    }

    private async Task PostWebhook(WebhookPayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            var response = await _httpClient.PostAsync(_mentorHelpWebhookUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _log.RootSawmill.Error($"MentorHelp webhook failed: {(int)response.StatusCode} {response.StatusCode}\n{body}");
            }
        }
        catch (Exception e)
        {
            _log.RootSawmill.Error($"MentorHelp webhook error: {e}");
        }
    }

    private void StartQueueProcessor()
    {
        if (Interlocked.Exchange(ref _queueProcessorStarted, 1) == 1)
            return;
        Task.Run(ProcessQueueAsync);
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            while (_pendingPayloads.TryDequeue(out var payload))
            {
                await PostWebhook(payload);
                await Task.Delay(1200);
            }
            _queueProcessorStarted = 0;

            if (_pendingPayloads.IsEmpty || Interlocked.Exchange(ref _queueProcessorStarted, 1) == 1)
                return;
        }
    }
}
