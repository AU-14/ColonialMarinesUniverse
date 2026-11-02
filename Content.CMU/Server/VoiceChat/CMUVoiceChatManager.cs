using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Content.Shared.CMU14.VoiceChat;
using Robust.Server.ServerStatus;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server.CMU14.VoiceChat;

public readonly record struct CMUVoiceWebState(string Name, bool InGame, bool CanSpeak, bool Muted, bool PushToTalk, bool Broadcasting, bool VoiceChanger, string? Radio);

public sealed partial class CMUVoiceChatManager : IPostInjectInit
{
    public event Action<NetUserId, bool, bool>? PushToTalkReceived;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IStatusHost _statusHost = default!;
    [Dependency] private ILogManager _logManager = default!;

    private const string PagePath = "/voice";
    private const string ConfigPlaceholder = "/*VOICE_CONFIG*/";
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ConcurrentDictionary<string, NetUserId> _tokens = new();
    private readonly Dictionary<NetUserId, string> _userTokens = new();
    private readonly HashSet<NetUserId> _hearSelf = new();
    private readonly HashSet<NetUserId> _notReceiving = new();
    private readonly Dictionary<NetUserId, HashSet<string>> _mutedChannels = new();
    private readonly Dictionary<string, (byte[] Data, string ContentType)> _files = new();

    private ISawmill _sawmill = default!;
    private CMUVoiceWebServer? _server;
    private string _indexTemplate = string.Empty;
    private volatile bool _enabled;
    private volatile string _webSocketUrl = string.Empty;
    private volatile int _webSocketPort;

    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill("voice");

        _net.RegisterNetMessage<CMUMsgVoiceFrame>();
        _net.RegisterNetMessage<CMUMsgVoiceLink>();
        _net.RegisterNetMessage<CMUMsgVoiceLinkRequest>(OnLinkRequest);
        _net.RegisterNetMessage<CMUMsgVoiceSettings>(OnSettings);
        _net.RegisterNetMessage<CMUMsgVoiceRadioKey>();
        _net.RegisterNetMessage<CMUMsgVoiceSelf>();
        _net.RegisterNetMessage<CMUMsgVoiceSpeakerInfo>();
        _net.RegisterNetMessage<CMUMsgVoicePushToTalk>(message =>
            PushToTalkReceived?.Invoke(message.MsgChannel.UserId, message.Pressed, message.Radio));
        _net.Disconnect += OnDisconnect;

        LoadWebFiles();
        _statusHost.AddHandler(HandleHttpRequestAsync);

        _cfg.OnValueChanged(CCVars.CMUVoiceChatWebSocketUrl, url => _webSocketUrl = url.Trim(), true);
        _cfg.OnValueChanged(CCVars.CMUVoiceChatWebSocketBind, _ => RestartServer());
        _cfg.OnValueChanged(CCVars.CMUVoiceChatTrustedProxies, _ => ApplyLimits());
        _cfg.OnValueChanged(CCVars.CMUVoiceChatMaxConnectionsPerIp, _ => ApplyLimits());
        _cfg.OnValueChanged(CCVars.CMUVoiceChatEnabled, OnEnabledChanged, true);
    }

    public bool TryDequeueEvent(out CMUVoiceWebEvent ev)
    {
        if (_server != null)
            return _server.TryDequeue(out ev);

        ev = default!;
        return false;
    }

    public bool IsWebConnected(NetUserId user)
    {
        return _server?.IsConnected(user) ?? false;
    }

    public bool HearsSelf(NetUserId user)
    {
        return _hearSelf.Contains(user);
    }

    public bool Receives(NetUserId user)
    {
        return !_notReceiving.Contains(user);
    }

    /// <summary>
    /// Whether the player muted this radio channel for voice in their headset window.
    /// </summary>
    public bool IsRadioChannelMuted(NetUserId user, string channel)
    {
        return _mutedChannels.TryGetValue(user, out var muted) && muted.Contains(channel);
    }

    public void SendState(NetUserId user, CMUVoiceWebState state)
    {
        _server?.Send(user, JsonSerializer.Serialize(new
        {
            t = "state",
            name = state.Name,
            inGame = state.InGame,
            canSpeak = state.CanSpeak,
            muted = state.Muted,
            pushToTalk = state.PushToTalk,
            broadcasting = state.Broadcasting,
            voiceChanger = state.VoiceChanger,
            radio = state.Radio,
        }, JsonOptions));
    }

    public void SetEffect(NetUserId user, CMUVoiceEffectSettings settings)
    {
        _server?.SetEffect(user, settings);
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
            StartServer();
        else
            StopServer();
    }

    private void RestartServer()
    {
        if (!_enabled)
            return;

        StopServer();
        StartServer();
    }

    private void StartServer()
    {
        if (_server != null)
            return;

        var bind = _cfg.GetCVar(CCVars.CMUVoiceChatWebSocketBind);
        if (!TryParseEndpoint(bind, out var endpoint))
        {
            _sawmill.Error($"Invalid voice.ws_bind value '{bind}', expected host:port.");
            return;
        }

        try
        {
            var server = new CMUVoiceWebServer(endpoint, ValidateToken, _sawmill);
            server.Start();
            _server = server;
            _webSocketPort = server.LocalEndpoint.Port;
            _sawmill.Info($"Voice chat WebSocket listening on {server.LocalEndpoint}.");
            ApplyLimits();
        }
        catch (SocketException e)
        {
            _sawmill.Error($"Failed to start voice chat WebSocket on {endpoint}: {e.Message}");
        }
    }

    private void ApplyLimits()
    {
        if (_server == null)
            return;

        _server.MaxConnectionsPerAddress = _cfg.GetCVar(CCVars.CMUVoiceChatMaxConnectionsPerIp);

        var proxies = new List<IPAddress>();
        foreach (var entry in _cfg.GetCVar(CCVars.CMUVoiceChatTrustedProxies).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(entry, out var address))
                proxies.Add(address);
            else
                _sawmill.Warning($"Ignoring invalid cmu.voice.trusted_proxies entry '{entry}'.");
        }

        _server.SetTrustedProxies(proxies);
    }

    private void StopServer()
    {
        _server?.Dispose();
        _server = null;
    }

    private NetUserId? ValidateToken(string token)
    {
        return _tokens.TryGetValue(NormalizeCode(token), out var user) ? user : null;
    }

    private static string NormalizeCode(string code)
    {
        var builder = new StringBuilder(CodeLength);
        foreach (var character in code)
        {
            if (char.IsAsciiLetterOrDigit(character))
                builder.Append(char.ToUpperInvariant(character));

            if (builder.Length > CodeLength)
                break;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Short codes players can type into the page by hand. A code stays valid for the whole connection
    /// so the page can reconnect on its own; it is dropped when the player leaves the server.
    /// </summary>
    private string CreateCode()
    {
        while (true)
        {
            var characters = new char[CodeLength];
            for (var i = 0; i < CodeLength; i++)
            {
                characters[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }

            var code = new string(characters);
            if (!_tokens.ContainsKey(code))
                return code;
        }
    }

    private void OnLinkRequest(CMUMsgVoiceLinkRequest message)
    {
        if (!_enabled || _server == null)
            return;

        var user = message.MsgChannel.UserId;
        if (!_userTokens.TryGetValue(user, out var token))
        {
            token = CreateCode();
            _tokens[token] = user;
            _userTokens[user] = token;
        }

        _net.ServerSendMessage(new CMUMsgVoiceLink
        {
            Url = GetPublicUrl(),
            Token = token,
            StatusPort = GetStatusPort(),
        }, message.MsgChannel);
    }

    private void OnSettings(CMUMsgVoiceSettings message)
    {
        var user = message.MsgChannel.UserId;

        if (message.HearSelf)
            _hearSelf.Add(user);
        else
            _hearSelf.Remove(user);

        if (message.Receive)
            _notReceiving.Remove(user);
        else
            _notReceiving.Add(user);

        if (message.MutedChannels.Count == 0)
            _mutedChannels.Remove(user);
        else
            _mutedChannels[user] = new HashSet<string>(message.MutedChannels);
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs args)
    {
        if (_userTokens.Remove(args.Channel.UserId, out var code))
            _tokens.TryRemove(code, out _);

        _server?.Disconnect(args.Channel.UserId);

        _hearSelf.Remove(args.Channel.UserId);
        _notReceiving.Remove(args.Channel.UserId);
        _mutedChannels.Remove(args.Channel.UserId);
    }

    private string GetPublicUrl()
    {
        var configured = _cfg.GetCVar(CCVars.CMUVoiceChatPublicUrl).Trim();
        if (configured.Length > 0)
            return configured.EndsWith('/') ? configured : configured + "/";

        return TryConvertHubUrl(_cfg.GetCVar(CVars.HubServerUrl), out var url) ? url : string.Empty;
    }

    private int GetStatusPort()
    {
        var bind = _cfg.GetCVar(CVars.StatusBind);
        var separator = bind.LastIndexOf(':');
        if (separator >= 0 && int.TryParse(bind[(separator + 1)..], out var port))
            return port;

        return _cfg.GetCVar(CVars.NetPort);
    }

    private static bool TryConvertHubUrl(string hubUrl, out string url)
    {
        url = string.Empty;
        if (!Uri.TryCreate(hubUrl.Trim(), UriKind.Absolute, out var uri))
            return false;

        var (scheme, defaultPort) = uri.Scheme switch
        {
            "ss14s" => ("https", 443),
            "https" => ("https", 443),
            "ss14" => ("http", 1212),
            "http" => ("http", 80),
            _ => (string.Empty, 0),
        };

        if (scheme.Length == 0)
            return false;

        var port = uri.IsDefaultPort || uri.Port < 0 ? defaultPort : uri.Port;
        url = new UriBuilder(scheme, uri.Host, port, uri.AbsolutePath.TrimEnd('/') + PagePath + "/").Uri.ToString();
        return true;
    }

    private static bool TryParseEndpoint(string value, out IPEndPoint endpoint)
    {
        endpoint = default!;
        var separator = value.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(value[(separator + 1)..], out var port) || port is < 0 or > 65535)
            return false;

        var host = value[..separator].Trim('[', ']');
        IPAddress? address = host switch
        {
            "*" or "0.0.0.0" => IPAddress.Any,
            "localhost" => IPAddress.Loopback,
            _ => IPAddress.TryParse(host, out var parsed) ? parsed : null,
        };

        if (address == null)
            return false;

        endpoint = new IPEndPoint(address, port);
        return true;
    }

    private void LoadWebFiles()
    {
        var assembly = typeof(CMUVoiceChatManager).Assembly;
        foreach (var (name, contentType) in new[]
                 {
                     ("index.html", "text/html; charset=utf-8"),
                     ("voice.js", "text/javascript; charset=utf-8"),
                     ("voice-worklet.js", "text/javascript; charset=utf-8"),
                     ("voice-worker.js", "text/javascript; charset=utf-8"),
                     ("noto-sans-regular.woff2", "font/woff2"),
                     ("noto-sans-bold.woff2", "font/woff2"),
                     ("boxfont-round.woff2", "font/woff2"),
                 })
        {
            using var stream = assembly.GetManifestResourceStream($"CMUVoiceChat.Web.{name}");
            if (stream == null)
            {
                _sawmill.Error($"Missing embedded voice chat web file {name}.");
                continue;
            }

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _files[name] = (memory.ToArray(), contentType);
        }

        if (_files.TryGetValue("index.html", out var index))
            _indexTemplate = Encoding.UTF8.GetString(index.Data);
    }

    private async Task<bool> HandleHttpRequestAsync(IStatusHandlerContext context)
    {
        var path = context.Url.AbsolutePath;
        if (!path.StartsWith(PagePath, StringComparison.OrdinalIgnoreCase) ||
            path.Length > PagePath.Length && path[PagePath.Length] != '/')
        {
            return false;
        }

        if (!context.IsGetLike)
        {
            await context.RespondErrorAsync(HttpStatusCode.MethodNotAllowed);
            return true;
        }

        if (!_enabled)
        {
            await context.RespondAsync("Voice chat is not enabled on this server.", HttpStatusCode.NotFound);
            return true;
        }

        if (path.Length == PagePath.Length)
        {
            context.ResponseHeaders["Location"] = "voice/" + context.Url.Query;
            await context.RespondAsync(string.Empty, HttpStatusCode.MovedPermanently);
            return true;
        }

        var file = path[(PagePath.Length + 1)..];
        if (file.Length == 0)
            file = "index.html";

        context.ResponseHeaders["Cache-Control"] = "no-cache";
        context.ResponseHeaders["X-Content-Type-Options"] = "nosniff";
        context.ResponseHeaders["Referrer-Policy"] = "no-referrer";

        if (file == "index.html")
        {
            var config = JsonSerializer.Serialize(new { wsUrl = _webSocketUrl, wsPort = _webSocketPort });
            await context.RespondAsync(_indexTemplate.Replace(ConfigPlaceholder, config), HttpStatusCode.OK, "text/html; charset=utf-8");
            return true;
        }

        if (_files.TryGetValue(file, out var entry))
        {
            await context.RespondAsync(entry.Data, HttpStatusCode.OK, entry.ContentType);
            return true;
        }

        if (file == "ws")
        {
            await context.RespondAsync("The voice chat WebSocket is not proxied here. Forward /voice/ws to voice.ws_bind in your reverse proxy.", HttpStatusCode.NotFound);
            return true;
        }

        await context.RespondAsync("Not Found", HttpStatusCode.NotFound);
        return true;
    }
}
