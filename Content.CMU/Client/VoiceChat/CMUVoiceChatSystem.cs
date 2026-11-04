using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Goobstation.Client.Audio;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Input;
using Content.Shared.CMU14.VoiceChat;
using Content.Shared.GameTicking;
using Content.Shared.Radio;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.VoiceChat;

public sealed partial class CMUVoiceChatSystem : EntitySystem
{
    [Dependency] private CMUVoiceChatManager _manager = default!;
    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private AreaEchoSystem _areaEcho = default!;
    [Dependency] private AudioEffectSystem _audioEffect = default!;

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How often a speaker's room size is re-measured for echo. It casts rays, so not every frame.
    /// </summary>
    private static readonly TimeSpan EchoCheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// A radio transmission counts as closed once no frame has arrived for this long.
    /// </summary>
    private static readonly TimeSpan RadioCloseAfter = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Scales engine occlusion for voices. One wall tile alone barely dulls the engine's low-pass,
    /// so voices through walls are pushed further towards a muffled thud.
    /// </summary>
    private const float WallMuffle = 2.5f;

    private const float ClickVolume = 0.6f;
    private const int RouteStreamShift = 16;

    private static readonly Color AnnouncementColor = Color.FromHex("#FFC844");
    private static readonly Color RadioColor = Color.FromHex("#2CDB2C");
    private static readonly Color BlockedColor = Color.FromHex("#E04545");
    private static readonly Color ShoutColor = Color.FromHex("#FF9F43");
    private static readonly Color WhisperColor = Color.FromHex("#A7B6D6");
    private static readonly Color TelephoneColor = Color.FromHex("#9956D3");

    private readonly Dictionary<int, CMUVoicePlaybackStream> _streams = new();
    private readonly Dictionary<ushort, (string Name, string Channel)> _speakerInfo = new();

    public IReadOnlyDictionary<int, CMUVoicePlaybackStream> Streams => _streams;

    /// <summary>
    /// The local player's own talking: speaker id, mic level and how they are being heard.
    /// </summary>
    public (ushort Speaker, float Level, float Activity, CMUVoiceSelfFlags Flags) Self;

    private TimeSpan _lastSelfFrame;
    private readonly List<int> _removeQueue = new();
    private readonly List<(AudioStream Stream, IAudioSource Source)> _clicks = new();

    private float _range;
    private float _volume;
    private bool _echo;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;

        _manager.FrameReceived += OnFrameReceived;
        _manager.RadioKeyReceived += OnRadioKey;
        _manager.SelfReceived += OnSelf;
        _manager.SpeakerInfoReceived += OnSpeakerInfo;

        Subs.CVar(_cfg, CCVars.CMUVoiceChatRange, value => _range = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatVolume, value => _volume = Math.Clamp(value, 0f, 2f), true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatEcho, value => _echo = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatEnabled, enabled =>
        {
            if (!enabled)
                ClearStreams();
        });

        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => ClearStreams());

        _overlays.AddOverlay(new CMUVoiceSpeakingOverlay(EntityManager, _timing));

        // Talk keys are read here and sent straight to the server, as Goob does.
        CommandBinds.Builder
            .Bind(CMUKeyFunctions.CMUVoicePushToTalk,
                InputCmdHandler.FromDelegate(
                    _ => _manager.SendPushToTalk(true, false),
                    _ => _manager.SendPushToTalk(false, false),
                    handle: false))
            .Bind(CMUKeyFunctions.CMUVoiceRadioPushToTalk,
                InputCmdHandler.FromDelegate(
                    _ => _manager.SendPushToTalk(true, true),
                    _ => _manager.SendPushToTalk(false, true),
                    handle: false))
            .Register<CMUVoiceChatSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _manager.FrameReceived -= OnFrameReceived;
        _manager.RadioKeyReceived -= OnRadioKey;
        _manager.SelfReceived -= OnSelf;
        _manager.SpeakerInfoReceived -= OnSpeakerInfo;
        CommandBinds.Unregister<CMUVoiceChatSystem>();
        _overlays.RemoveOverlay<CMUVoiceSpeakingOverlay>();
        ClearStreams();

        foreach (var (audio, source) in _clicks)
        {
            source.Dispose();
            audio.Dispose();
        }

        _clicks.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        RetireClicks();

        var now = _timing.RealTime;
        if (now - _lastSelfFrame > TimeSpan.FromMilliseconds(150))
            Self.Activity = MathF.Max(0f, Self.Activity - frameTime / 0.3f);

        if (_streams.Count == 0)
            return;

        var listener = _audio.GetListenerCoordinates();

        foreach (var (id, stream) in _streams)
        {
            if (stream.RadioOpen && now - stream.LastActivity > RadioCloseAfter)
            {
                stream.RadioOpen = false;
                if (!IsOwnVoice(stream))
                    PlayClick(CMUVoiceRadioFilter.CreateCloseClick(ClickVolume));
            }

            UpdateEcho(stream, now);
            stream.Update(now, GetParams(stream, listener));
            stream.UpdateLevels(frameTime);

            if (stream.IsIdle && now - stream.LastActivity > IdleTimeout)
                _removeQueue.Add(id);
        }

        foreach (var id in _removeQueue)
        {
            if (_streams.Remove(id, out var stream))
                stream.Dispose();
        }

        _removeQueue.Clear();
    }

    private void OnFrameReceived(CMUMsgVoiceFrame message)
    {
        var pcm = new short[CMUVoiceCodec.FrameSamples];
        if (!CMUVoiceCodec.Decode(message.Payload, pcm))
            return;

        // Proximity, radio and announcement copies of one speaker are separate streams: all can be heard at once.
        var key = message.Speaker | (int) message.Route << RouteStreamShift;
        if (!_streams.TryGetValue(key, out var stream))
        {
            stream = new CMUVoicePlaybackStream(_audioManager) { Route = message.Route };
            stream.RadioFilter = message.Route switch
            {
                CMUVoiceRoute.Radio => new CMUVoiceRadioFilter(message.Speaker),
                CMUVoiceRoute.Announcement => CMUVoiceRadioFilter.PublicAddress(message.Speaker),
                CMUVoiceRoute.Telephone => CMUVoiceRadioFilter.Telephone(message.Speaker),
                _ => null,
            };

            _streams[key] = stream;
        }

        stream.Source = message.Source;
        stream.Speaker = message.Speaker;
        stream.Range = message.Range;

        if (stream.RadioFilter != null)
        {
            stream.RadioFilter.Process(pcm);

            // The transmitter hears their own clicks from the key, not from their echoed voice.
            // Announcements open with the console's own chime instead of clicks.
            if (stream.Route == CMUVoiceRoute.Radio && !stream.RadioOpen)
            {
                stream.RadioOpen = true;
                if (!IsOwnVoice(stream))
                    PlayClick(CMUVoiceRadioFilter.CreateOpenClick(ClickVolume));
            }
        }

        stream.Relayed = message.Relayed;
        stream.RelayPosition = message.RelayPosition;
        stream.RelayDistance = message.RelayDistance;
        stream.AddFrame(message.Sequence, message.Flags, pcm, _timing.RealTime);
    }

    private CMUVoicePlaybackParams GetParams(CMUVoicePlaybackStream stream, MapCoordinates listener)
    {
        var referenceDistance = _audio.GetAudioDistance(1f);
        var maxDistance = _audio.GetAudioDistance(stream.Range > 0f ? stream.Range : _range);
        var gain = MathF.Min(_volume, 1f);
        var boost = MathF.Max(_volume, 1f);

        // Radio, announcements and phone calls are heard in the ear, not from the speaker's position.
        stream.Audibility = 0f;
        if (stream.RadioFilter != null)
        {
            stream.Audibility = 1f;
            return new CMUVoicePlaybackParams(listener.Position, gain, 0f, referenceDistance, maxDistance, boost);
        }

        if (stream.Relayed)
            return GetRelayedParams(stream, listener, gain, referenceDistance, maxDistance, boost);

        if (!TryGetEntity(stream.Source, out var uid) || TerminatingOrDeleted(uid.Value))
            return new CMUVoicePlaybackParams(listener.Position, 0f, 0f, referenceDistance, maxDistance, boost);

        var coordinates = _transform.GetMapCoordinates(uid.Value);
        if (coordinates.MapId != listener.MapId)
            return new CMUVoicePlaybackParams(listener.Position, 0f, 0f, referenceDistance, maxDistance, boost);

        var delta = coordinates.Position - listener.Position;
        var distance = delta.Length();
        if (_audio.GetAudioDistance(distance) > maxDistance)
            return new CMUVoicePlaybackParams(coordinates.Position, 0f, 0f, referenceDistance, maxDistance, boost);

        var occlusion = distance > 0.1f ? _audio.GetOcclusion(listener, delta, distance, uid.Value) * WallMuffle : 0f;
        stream.Audibility = Math.Clamp(1f - distance / MathF.Max(stream.Range > 0f ? stream.Range : _range, 0.1f), 0f, 1f) * MathF.Exp(-occlusion * 0.5f);
        return new CMUVoicePlaybackParams(coordinates.Position, gain, occlusion, referenceDistance, maxDistance, boost);
    }

    /// <summary>
    /// Voices heard in the world echo like other sounds when the speaker stands in a large room.
    /// Radio, PA and phone voices are already in the ear and stay dry.
    /// </summary>
    private void UpdateEcho(CMUVoicePlaybackStream stream, TimeSpan now)
    {
        if (now < stream.NextEchoCheck)
            return;

        stream.NextEchoCheck = now + EchoCheckInterval;
        stream.Echo = null;

        if (!_echo ||
            stream.RadioFilter != null ||
            stream.Relayed ||
            !TryGetEntity(stream.Source, out var speaker) ||
            TerminatingOrDeleted(speaker.Value) ||
            _areaEcho.CMUGetEchoPreset(speaker.Value) is not { } preset ||
            !_audioEffect.ResolveCachedEffect(preset, out var auxiliary, out _) ||
            !TryComp<AudioAuxiliaryComponent>(auxiliary, out var auxiliaryComp))
        {
            return;
        }

        stream.Echo = auxiliaryComp.Auxiliary;
    }

    /// <summary>
    /// A speaker on an adjacent z-level is heard from the floor opening the server picked. The source is
    /// pushed back along the listener-to-opening line by the distance already travelled to the opening,
    /// so attenuation covers the whole route while panning still points at the opening.
    /// </summary>
    private CMUVoicePlaybackParams GetRelayedParams(
        CMUVoicePlaybackStream stream,
        MapCoordinates listener,
        float gain,
        float referenceDistance,
        float maxDistance,
        float boost)
    {
        var delta = stream.RelayPosition - listener.Position;
        var openingDistance = delta.Length();
        var distance = openingDistance + stream.RelayDistance;
        if (_audio.GetAudioDistance(distance) > maxDistance)
            return new CMUVoicePlaybackParams(stream.RelayPosition, 0f, 0f, referenceDistance, maxDistance, boost);

        stream.Audibility = Math.Clamp(1f - distance / MathF.Max(stream.Range > 0f ? stream.Range : _range, 0.1f), 0f, 1f);
        var direction = openingDistance > 0.01f ? delta / openingDistance : Vector2.UnitY;
        var occlusion = openingDistance > 0.1f ? _audio.GetOcclusion(listener, delta, openingDistance) * WallMuffle : 0f;
        return new CMUVoicePlaybackParams(listener.Position + direction * distance, gain, occlusion, referenceDistance, maxDistance, boost);
    }

    private void OnSelf(CMUMsgVoiceSelf message)
    {
        Self = (message.Speaker, message.Level / 255f, 1f, message.Flags);
        _lastSelfFrame = _timing.RealTime;
    }

    private void OnSpeakerInfo(CMUMsgVoiceSpeakerInfo message)
    {
        _speakerInfo[message.Speaker] = (message.Name, message.Channel);
    }

    public bool IsSelf(ushort speaker)
    {
        return Self.Speaker != 0 && speaker == Self.Speaker;
    }

    public string GetSpeakerName(ushort speaker)
    {
        return _speakerInfo.TryGetValue(speaker, out var info)
            ? info.Name
            : Loc.GetString("cmu-voice-speaker-unknown");
    }

    private bool TryGetRadioChannel(ushort speaker, [NotNullWhen(true)] out RadioChannelPrototype? channel)
    {
        channel = null;
        return _speakerInfo.TryGetValue(speaker, out var info) &&
               info.Channel.Length > 0 &&
               _prototype.TryIndex(info.Channel, out channel);
    }

    public string? GetRouteLabel(CMUVoicePlaybackStream stream)
    {
        return stream.Route switch
        {
            CMUVoiceRoute.Radio => TryGetRadioChannel(stream.Speaker, out var channel)
                ? channel.LocalizedName
                : Loc.GetString("cmu-voice-route-radio"),
            CMUVoiceRoute.Announcement => Loc.GetString("cmu-voice-route-announcement"),
            CMUVoiceRoute.Telephone => Loc.GetString("cmu-voice-route-telephone"),
            _ => GetLoudness(stream) switch
            {
                > 0 => Loc.GetString("cmu-voice-loudness-shout"),
                < 0 => Loc.GetString("cmu-voice-loudness-whisper"),
                _ => null,
            },
        };
    }

    public Color GetRouteColor(CMUVoicePlaybackStream stream)
    {
        return stream.Route switch
        {
            CMUVoiceRoute.Radio => TryGetRadioChannel(stream.Speaker, out var channel) ? channel.Color : RadioColor,
            CMUVoiceRoute.Announcement => AnnouncementColor,
            CMUVoiceRoute.Telephone => TelephoneColor,
            _ => GetLoudness(stream) switch
            {
                > 0 => ShoutColor,
                < 0 => WhisperColor,
                _ => Color.White,
            },
        };
    }

    public string? GetSelfLabel()
    {
        var flags = Self.Flags;
        if ((flags & CMUVoiceSelfFlags.Blocked) != 0)
            return Loc.GetString("cmu-voice-self-blocked");
        if ((flags & CMUVoiceSelfFlags.Announcement) != 0)
            return Loc.GetString("cmu-voice-route-announcement");
        if ((flags & CMUVoiceSelfFlags.Radio) != 0)
            return TryGetRadioChannel(Self.Speaker, out var channel) ? channel.LocalizedName : Loc.GetString("cmu-voice-route-radio");
        if ((flags & CMUVoiceSelfFlags.Telephone) != 0)
            return Loc.GetString("cmu-voice-route-telephone");
        if ((flags & CMUVoiceSelfFlags.Shout) != 0)
            return Loc.GetString("cmu-voice-loudness-shout");
        if ((flags & CMUVoiceSelfFlags.Whisper) != 0)
            return Loc.GetString("cmu-voice-loudness-whisper");
        return null;
    }

    public Color GetSelfColor()
    {
        var flags = Self.Flags;
        if ((flags & CMUVoiceSelfFlags.Blocked) != 0)
            return BlockedColor;
        if ((flags & CMUVoiceSelfFlags.Announcement) != 0)
            return AnnouncementColor;
        if ((flags & CMUVoiceSelfFlags.Radio) != 0)
            return TryGetRadioChannel(Self.Speaker, out var channel) ? channel.Color : RadioColor;
        if ((flags & CMUVoiceSelfFlags.Telephone) != 0)
            return TelephoneColor;
        if ((flags & CMUVoiceSelfFlags.Shout) != 0)
            return ShoutColor;
        if ((flags & CMUVoiceSelfFlags.Whisper) != 0)
            return WhisperColor;
        return Color.White;
    }

    private int GetLoudness(CMUVoicePlaybackStream stream)
    {
        if (stream.Range <= 0f)
            return 0;

        if (stream.Range > _range + 0.01f)
            return 1;

        return stream.Range < _range - 0.01f ? -1 : 0;
    }

    private void OnRadioKey(bool transmitting)
    {
        PlayClick(transmitting
            ? CMUVoiceRadioFilter.CreateOpenClick(ClickVolume)
            : CMUVoiceRadioFilter.CreateCloseClick(ClickVolume));
    }

    private bool IsOwnVoice(CMUVoicePlaybackStream stream)
    {
        return _player.LocalEntity is { } local && TryGetEntity(stream.Source, out var source) && source == local;
    }

    private void PlayClick(short[] pcm)
    {
        var gain = MathF.Min(_volume, 1f);
        if (gain <= 0f)
            return;

        var audio = _audioManager.LoadAudioRaw(pcm, 1, CMUVoiceCodec.SampleRate);
        var source = _audioManager.CreateAudioSource(audio);
        if (source == null)
        {
            audio.Dispose();
            return;
        }

        source.Global = true;
        source.Gain = gain;
        source.StartPlaying();
        _clicks.Add((audio, source));
    }

    private void RetireClicks()
    {
        for (var i = _clicks.Count - 1; i >= 0; i--)
        {
            var (audio, source) = _clicks[i];
            if (source.Playing)
                continue;

            source.Dispose();
            audio.Dispose();
            _clicks.RemoveAt(i);
        }
    }

    private void ClearStreams()
    {
        _speakerInfo.Clear();
        foreach (var stream in _streams.Values)
        {
            stream.Dispose();
        }

        _streams.Clear();
    }
}
