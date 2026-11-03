using System.Diagnostics.CodeAnalysis;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.CMU14.VoiceChat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.VoiceChat;

/// <summary>
/// Voice over headsets. Who hears a transmission follows the same rules as text radio:
/// channel keys, map range, telecom servers and every send/receive attempt hook (jammers, EMP, comms tower, deafness).
/// </summary>
public sealed partial class CMUVoiceChatSystem
{
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>
    /// Receiver lists are rebuilt this often during a transmission rather than for every 20 ms frame.
    /// </summary>
    private static readonly TimeSpan RadioAudienceRefresh = TimeSpan.FromMilliseconds(500);

    private EntityQuery<WearingHeadsetComponent> _wearingHeadsetQuery;
    private EntityQuery<EncryptionKeyHolderComponent> _keyHolderQuery;
    private EntityQuery<IntercomComponent> _intercomQuery;
    private EntityQuery<TelecomExemptComponent> _telecomExemptQuery;
    private EntityQuery<IntrinsicRadioReceiverComponent> _intrinsicReceiverQuery;
    private EntityQuery<ActorComponent> _actorQuery;

    private readonly HashSet<INetChannel> _radioDedupe = new();
    private readonly List<RadioChannelPrototype> _radioChannels = new();
    private bool _radioEnabled;
    private bool _radioCommon;

    private void InitializeRadio()
    {
        _wearingHeadsetQuery = GetEntityQuery<WearingHeadsetComponent>();
        _keyHolderQuery = GetEntityQuery<EncryptionKeyHolderComponent>();
        _intercomQuery = GetEntityQuery<IntercomComponent>();
        _telecomExemptQuery = GetEntityQuery<TelecomExemptComponent>();
        _intrinsicReceiverQuery = GetEntityQuery<IntrinsicRadioReceiverComponent>();
        _actorQuery = GetEntityQuery<ActorComponent>();

        SubscribeNetworkEvent<CMUVoiceRadioSelectEvent>(OnRadioSelect);

        Subs.CVar(_cfg, CCVars.CMUVoiceChatRadioEnabled, value => _radioEnabled = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatRadioCommon, value => _radioCommon = value, true);
    }

    /// <summary>
    /// Resolves the headset and channel a voice transmission from <paramref name="uid"/> would use:
    /// the channel picked with the cycle key if the headset still has it, otherwise the headset's
    /// default channel as with the ":h" chat prefix, falling back to marine common.
    /// </summary>
    public bool TryGetVoiceRadioChannel(EntityUid uid, out EntityUid headset, out RadioChannelPrototype channel)
    {
        headset = default;
        channel = default!;

        if (!_radioEnabled ||
            !_wearingHeadsetQuery.TryComp(uid, out var wearing) ||
            !_keyHolderQuery.TryComp(wearing.Headset, out var keys))
        {
            return false;
        }

        ProtoId<RadioChannelPrototype>? channelId;
        if (TryComp<CMUVoiceRadioComponent>(uid, out var selection) &&
            selection.Channel is { } selected &&
            CanTransmitOn(keys, selected))
        {
            channelId = selected;
        }
        else
        {
            var ev = new GetDefaultRadioChannelEvent();
            RaiseLocalEvent(uid, ev);
            channelId = ev.Channel is { } defaultChannel
                ? new ProtoId<RadioChannelPrototype>(defaultChannel)
                : SharedChatSystem.CommonChannel;
        }

        if (channelId is not { } id ||
            !CanTransmitOn(keys, id) ||
            !_prototype.TryIndex(id, out var proto))
        {
            return false;
        }

        headset = wearing.Headset;
        channel = proto;
        return true;
    }

    private bool CanTransmitOn(EncryptionKeyHolderComponent keys, ProtoId<RadioChannelPrototype> channel)
    {
        return keys.Channels.Contains(channel) &&
               !keys.ReadOnlyChannels.Contains(channel) &&
               (_radioCommon || channel != SharedChatSystem.CommonChannel);
    }

    /// <summary>
    /// Steps the voice radio to the next channel the worn headset can transmit on, in name order.
    /// </summary>
    public bool TryCycleVoiceRadioChannel(EntityUid uid, [NotNullWhen(true)] out RadioChannelPrototype? channel)
    {
        channel = null;
        if (!_radioEnabled ||
            !_wearingHeadsetQuery.TryComp(uid, out var wearing) ||
            !_keyHolderQuery.TryComp(wearing.Headset, out var keys))
        {
            return false;
        }

        _radioChannels.Clear();
        foreach (var id in keys.Channels)
        {
            if (CanTransmitOn(keys, id) && _prototype.TryIndex(id, out var proto))
                _radioChannels.Add(proto);
        }

        if (_radioChannels.Count == 0)
            return false;

        _radioChannels.Sort((a, b) => string.Compare(a.LocalizedName, b.LocalizedName, StringComparison.CurrentCultureIgnoreCase));

        var current = TryGetVoiceRadioChannel(uid, out _, out var active) ? active : null;
        var index = current == null ? -1 : _radioChannels.IndexOf(current);
        channel = _radioChannels[(index + 1) % _radioChannels.Count];
        return TrySelectVoiceRadioChannel(uid, channel.ID);
    }

    /// <summary>
    /// Makes <paramref name="channelId"/> the voice radio channel, if the worn headset can transmit on it.
    /// A transmission in progress moves to it straight away.
    /// </summary>
    public bool TrySelectVoiceRadioChannel(EntityUid uid, ProtoId<RadioChannelPrototype> channelId)
    {
        if (!_radioEnabled ||
            !_wearingHeadsetQuery.TryComp(uid, out var wearing) ||
            !_keyHolderQuery.TryComp(wearing.Headset, out var keys) ||
            !CanTransmitOn(keys, channelId) ||
            !_prototype.TryIndex(channelId, out var channel))
        {
            return false;
        }

        var radio = EnsureComp<CMUVoiceRadioComponent>(uid);
        radio.Channel = channelId;
        Dirty(uid, radio);

        if (_actorQuery.TryComp(uid, out var actor))
        {
            var speaker = GetSpeaker(actor.PlayerSession.UserId);
            if (IsTransmittingRadio(speaker))
            {
                speaker.RadioChannel = channel;
                speaker.RadioAudienceExpires = TimeSpan.Zero;
                speaker.InfoSent.Clear();
            }

            SendState(actor.PlayerSession.UserId, false);
        }

        return true;
    }

    private void OnRadioSelect(CMUVoiceRadioSelectEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid || !IsVoiceCapable(uid))
            return;

        if (TrySelectVoiceRadioChannel(uid, ev.Channel) && _prototype.TryIndex(ev.Channel, out var channel))
            _popup.PopupEntity(Loc.GetString("cmu-voice-radio-channel-selected", ("channel", channel.LocalizedName)), uid, uid);
    }

    private void CycleRadioChannel(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } uid || !IsVoiceCapable(uid))
            return;

        if (!TryCycleVoiceRadioChannel(uid, out var channel))
        {
            _popup.PopupEntity(Loc.GetString("cmu-voice-radio-no-channel"), uid, uid);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-voice-radio-channel-selected", ("channel", channel.LocalizedName)), uid, uid);
    }

    private void SetRadioPushToTalk(ICommonSession? session, bool active)
    {
        if (session == null)
            return;

        var speaker = GetSpeaker(session.UserId);
        if (speaker.RadioPushToTalk == active)
            return;

        speaker.RadioPushToTalk = active;
        if (!active)
            speaker.TalkKeyReleased = _timing.RealTime;
        if (active)
            StartRadio(session, speaker);
        else
            StopRadio(session, speaker);

        SendState(session.UserId, false);
    }

    private void StartRadio(ICommonSession session, Speaker speaker)
    {
        if (session.AttachedEntity is not { } uid ||
            !_voice.IsWebConnected(session.UserId) ||
            IsMuted(session.UserId) ||
            !IsVoiceCapable(uid))
        {
            return;
        }

        if (!TryGetVoiceRadioChannel(uid, out var headset, out var channel))
        {
            _popup.PopupEntity(Loc.GetString("cmu-voice-radio-no-channel"), uid, uid);
            return;
        }

        speaker.RadioSource = headset;
        speaker.RadioChannel = channel;
        speaker.RadioAudienceExpires = TimeSpan.Zero;
        speaker.InfoSent.Clear();
        _net.ServerSendMessage(new CMUMsgVoiceRadioKey { Transmitting = true }, session.Channel);
    }

    private void StopRadio(ICommonSession session, Speaker speaker)
    {
        if (speaker.RadioChannel == null)
            return;

        speaker.RadioChannel = null;
        speaker.RadioSource = null;
        speaker.RadioAudience.Clear();
        _net.ServerSendMessage(new CMUMsgVoiceRadioKey { Transmitting = false }, session.Channel);
    }

    private bool IsTransmittingRadio(Speaker speaker)
    {
        return speaker.RadioPushToTalk && speaker.RadioChannel != null;
    }

    private void SendRadioFrame(ICommonSession session, Speaker speaker, EntityUid uid, CMUVoiceWebFrame frame, NetEntity source, TimeSpan now)
    {
        if (!IsTransmittingRadio(speaker))
            return;

        // The headset can come off mid-transmission.
        if (!_wearingHeadsetQuery.TryComp(uid, out var wearing) || wearing.Headset != speaker.RadioSource)
        {
            StopRadio(session, speaker);
            return;
        }

        if (now >= speaker.RadioAudienceExpires)
        {
            speaker.RadioAudienceExpires = now + RadioAudienceRefresh;
            RefreshRadioAudience(session, speaker, uid);
        }

        if (speaker.RadioAudience.Count == 0)
            return;

        SendSpeakerInfo(speaker, uid, speaker.RadioAudience);
        _net.ServerSendToMany(new CMUMsgVoiceFrame
        {
            Source = source,
            Speaker = speaker.Id,
            Sequence = frame.Sequence,
            Flags = frame.Flags,
            Payload = frame.Payload,
            Route = CMUVoiceRoute.Radio,
        }, speaker.RadioAudience);
    }

    private void RefreshRadioAudience(ICommonSession speakerSession, Speaker speaker, EntityUid speakerUid)
    {
        speaker.RadioAudience.Clear();
        if (speaker.RadioChannel is not { } channel || speaker.RadioSource is not { } radioSource)
            return;

        // Mirrors RadioSystem.SendRadioMessage so voice and text radio agree on who can hear.
        var chat = new MsgChatMessage
        {
            Message = new ChatMessage(ChatChannel.Radio, string.Empty, string.Empty, GetNetEntity(speakerUid), null),
        };
        var send = new RadioSendAttemptEvent(channel, radioSource, speakerUid, string.Empty, chat);
        RaiseLocalEvent(ref send);
        RaiseLocalEvent(radioSource, ref send);
        if (send.Cancelled)
            return;

        var sourceMap = Transform(radioSource).MapID;
        var needServer = !channel.LongRange && !_telecomExemptQuery.HasComp(radioSource);
        if (needServer && !_radio.HasActiveServer(sourceMap, channel.ID))
            return;

        _radioDedupe.Clear();
        var query = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (query.MoveNext(out var receiver, out var radio, out var xform))
        {
            if (!radio.ReceiveAllChannels &&
                (!radio.Channels.Contains(channel.ID) ||
                 _intercomQuery.TryComp(receiver, out var intercom) && !intercom.SupportedChannels.Contains(channel.ID)))
            {
                continue;
            }

            if (!channel.LongRange && xform.MapID != sourceMap && !radio.GlobalReceive)
                continue;

            if (ResolveRadioListener(receiver) is not { } listener ||
                !_actorQuery.TryComp(listener, out var actor))
            {
                continue;
            }

            var session = actor.PlayerSession;
            if (session.Status != SessionStatus.InGame ||
                !_voice.Receives(session.UserId) ||
                _voice.IsRadioChannelMuted(session.UserId, channel.ID))
            {
                continue;
            }

            if (session == speakerSession)
            {
                if (!_voice.HearsSelf(session.UserId))
                    continue;
            }
            else if (!IsVoiceCapable(listener))
            {
                continue;
            }

            var receive = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref receive);
            RaiseLocalEvent(receiver, ref receive);
            if (receive.Cancelled)
                continue;

            if (_radioDedupe.Add(session.Channel))
                speaker.RadioAudience.Add(session.Channel);
        }
    }

    /// <summary>
    /// Matches RadioSystem: intrinsic receivers hear for themselves, headsets for whoever wears them.
    /// </summary>
    private EntityUid? ResolveRadioListener(EntityUid receiver)
    {
        if (_intrinsicReceiverQuery.HasComp(receiver))
            return receiver;

        var wearer = Transform(receiver).ParentUid;
        if (wearer.IsValid() &&
            _wearingHeadsetQuery.TryComp(wearer, out var wearing) &&
            wearing.Headset == receiver)
        {
            return wearer;
        }

        return null;
    }
}
