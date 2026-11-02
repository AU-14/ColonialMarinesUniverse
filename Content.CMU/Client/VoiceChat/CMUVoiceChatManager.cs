using System.Linq;
using System.Net;
using System.Net.Sockets;
using Content.Shared.CCVar;
using Content.Shared.CMU14.VoiceChat;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Client.CMU14.VoiceChat;

public sealed partial class CMUVoiceChatManager : IPostInjectInit
{
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IUriOpener _uriOpener = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;
    private bool _receiveDisabled;

    public event Action<CMUMsgVoiceFrame>? FrameReceived;
    public event Action<bool>? RadioKeyReceived;
    public event Action<CMUMsgVoiceSelf>? SelfReceived;
    public event Action<CMUMsgVoiceSpeakerInfo>? SpeakerInfoReceived;

    /// <summary>
    /// The code the server issued for this connection, for typing into the page by hand.
    /// </summary>
    public string? Code { get; private set; }

    public event Action? CodeChanged;

    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill("voice");

        _net.RegisterNetMessage<CMUMsgVoiceFrame>(message => FrameReceived?.Invoke(message));
        _net.RegisterNetMessage<CMUMsgVoiceLinkRequest>();
        _net.RegisterNetMessage<CMUMsgVoiceLink>(OnLink);
        _net.RegisterNetMessage<CMUMsgVoiceSettings>();
        _net.RegisterNetMessage<CMUMsgVoicePushToTalk>();
        _net.RegisterNetMessage<CMUMsgVoiceSelf>(message => SelfReceived?.Invoke(message));
        _net.RegisterNetMessage<CMUMsgVoiceSpeakerInfo>(message => SpeakerInfoReceived?.Invoke(message));
        _net.RegisterNetMessage<CMUMsgVoiceRadioKey>(message => RadioKeyReceived?.Invoke(message.Transmitting));

        _net.Connected += OnConnected;
        _cfg.OnValueChanged(CCVars.CMUVoiceChatHearSelf, OnHearSelfChanged);
        _cfg.OnValueChanged(CCVars.CMUVoiceChatVolume, OnVolumeChanged);
        _cfg.OnValueChanged(CCVars.CMUVoiceChatRadioMuted, _ => SendSettings());
    }

    /// <summary>
    /// Radio channel IDs muted for voice in the headset window.
    /// </summary>
    public HashSet<string> GetMutedChannels()
    {
        return _cfg.GetCVar(CCVars.CMUVoiceChatRadioMuted)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet();
    }

    public void SetChannelMuted(string channel, bool muted)
    {
        var channels = GetMutedChannels();
        if (muted ? !channels.Add(channel) : !channels.Remove(channel))
            return;

        _cfg.SetCVar(CCVars.CMUVoiceChatRadioMuted, string.Join(',', channels.Order()));
        _cfg.SaveToFile();
    }

    public void SendPushToTalk(bool pressed, bool radio)
    {
        if (_net.IsConnected && _cfg.GetCVar(CCVars.CMUVoiceChatEnabled))
            _net.ClientSendMessage(new CMUMsgVoicePushToTalk { Pressed = pressed, Radio = radio });
    }

    public bool RequestLink()
    {
        if (!_net.IsConnected || !_cfg.GetCVar(CCVars.CMUVoiceChatEnabled))
            return false;

        _net.ClientSendMessage(new CMUMsgVoiceLinkRequest());
        return true;
    }

    private void OnConnected(object? sender, NetChannelArgs args)
    {
        SendSettings();
    }

    private void OnHearSelfChanged(bool hearSelf)
    {
        SendSettings();
    }

    private void OnVolumeChanged(float volume)
    {
        if (volume <= 0f != _receiveDisabled)
            SendSettings();
    }

    private void SendSettings()
    {
        if (!_net.IsConnected)
            return;

        _receiveDisabled = _cfg.GetCVar(CCVars.CMUVoiceChatVolume) <= 0f;
        _net.ClientSendMessage(new CMUMsgVoiceSettings
        {
            HearSelf = _cfg.GetCVar(CCVars.CMUVoiceChatHearSelf),
            Receive = !_receiveDisabled,
            MutedChannels = GetMutedChannels().ToList(),
        });
    }

    private void OnLink(CMUMsgVoiceLink message)
    {
        Code = message.Token;
        CodeChanged?.Invoke();

        var baseUrl = message.Url;
        if (string.IsNullOrEmpty(baseUrl))
        {
            if (_net.ServerChannel is not { } channel)
                return;

            var address = channel.RemoteEndPoint.Address;
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            var host = IPAddress.IsLoopback(address)
                ? "localhost"
                : address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();

            baseUrl = $"http://{host}:{message.StatusPort}/voice/";
        }

        try
        {
            _uriOpener.OpenUri($"{baseUrl}#{message.Token}");
        }
        catch (ArgumentException e)
        {
            _sawmill.Error($"Could not open voice chat page {baseUrl}: {e.Message}");
        }
    }
}
