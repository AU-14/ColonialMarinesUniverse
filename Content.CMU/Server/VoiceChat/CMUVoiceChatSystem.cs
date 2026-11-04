using System.Numerics;
using Content.Server._RMC14.Admin;
using Content.Server.Atmos.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.CCVar;
using Content.Shared.Clothing.Components;
using Content.Shared.CMU14.Input;
using Content.Shared.CMU14.VoiceChat;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Radio;
using Content.Shared.Speech;
using Content.Shared.Tag;
using Content.Shared.VoiceMask;
using Content.Shared._RMC14.Xenonids;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.VoiceChat;

public sealed partial class CMUVoiceChatSystem : EntitySystem
{
    [Dependency] private CMUVoiceChatManager _voice = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private RMCChatBansManager _chatBans = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<SpeciesPrototype> HumanSpecies = "Human";
    private static readonly ProtoId<TagPrototype> GasMaskTag = "GasMask";

    private static readonly TimeSpan SpeakingTimeout = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan TransmissionGap = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PermissionRecheck = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RefusalDisplay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StateRefreshInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Frames still in flight when a talk key is released are kept so the end of a word is not clipped.
    /// </summary>
    private static readonly TimeSpan PushToTalkGrace = TimeSpan.FromMilliseconds(400);
    private const float ListenerMargin = 2f;

    private readonly Dictionary<NetUserId, Speaker> _speakers = new();
    private readonly HashSet<NetUserId> _adminMuted = new();
    private readonly Dictionary<NetUserId, Audience> _audiences = new();
    private readonly Stack<Audience> _audiencePool = new();

    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<EyeComponent> _eyeQuery;
    private EntityQuery<SpeechComponent> _speechQuery;
    private EntityQuery<HumanoidProfileComponent> _humanoidQuery;
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;

    private ICommonSession[]? _sessions;
    private ushort _nextSpeakerId = 1;
    private float _range;
    private float _zOpeningRadius;
    private bool _dynamicRange;
    private float _shoutRange;
    private float _whisperRange;
    private float _shoutThreshold;
    private float _whisperThreshold;
    private TimeSpan _nextStateRefresh;

    public override void Initialize()
    {
        base.Initialize();

        _ghostQuery = GetEntityQuery<GhostComponent>();
        _eyeQuery = GetEntityQuery<EyeComponent>();
        _speechQuery = GetEntityQuery<SpeechComponent>();
        _humanoidQuery = GetEntityQuery<HumanoidProfileComponent>();
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();

        Subs.CVar(_cfg, CCVars.CMUVoiceChatRange, value => _range = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatZOpeningRadius, value => _zOpeningRadius = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatDynamicRange, value => _dynamicRange = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatShoutRange, value => _shoutRange = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatWhisperRange, value => _whisperRange = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatShoutThreshold, value => _shoutThreshold = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatWhisperThreshold, value => _whisperThreshold = value, true);

        InitializeRadio();
        InitializeAnnouncements();
        InitializeTelephone();

        _voice.PushToTalkReceived += OnPushToTalk;

        CommandBinds.Builder
            .Bind(CMUKeyFunctions.CMUVoiceRadioCycleChannel,
                InputCmdHandler.FromDelegate(CycleRadioChannel, handle: false))
            .Register<CMUVoiceChatSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _voice.PushToTalkReceived -= OnPushToTalk;
        CommandBinds.Unregister<CMUVoiceChatSystem>();
    }

    private void OnPushToTalk(NetUserId user, bool pressed, bool radio)
    {
        if (!_player.TryGetSessionById(user, out var session))
            return;

        if (radio)
            SetRadioPushToTalk(session, pressed);
        else
            SetPushToTalk(session, pressed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.RealTime;

        foreach (var audience in _audiences.Values)
        {
            audience.Clear();
            _audiencePool.Push(audience);
        }

        _audiences.Clear();
        _sessions = null;

        while (_voice.TryDequeueEvent(out var ev))
        {
            switch (ev)
            {
                case CMUVoiceWebFrame frame:
                    HandleFrame(frame, now);
                    break;
                case CMUVoiceWebConnectionChanged changed:
                    UpdateEffect(changed.User);
                    SendState(changed.User, true);
                    break;
                case CMUVoiceWebEffectSelected selected:
                    GetSpeaker(selected.User).SelectedEffect = selected.Effect;
                    UpdateEffect(selected.User);
                    break;
            }
        }

        UpdateSpeaking(now);
        UpdateAnnouncements(now);

        if (now < _nextStateRefresh)
            return;

        _nextStateRefresh = now + StateRefreshInterval;
        foreach (var user in _speakers.Keys)
        {
            UpdateEffect(user);
            SendState(user, false);
        }
    }

    public bool ToggleAdminMute(NetUserId user)
    {
        var muted = _adminMuted.Add(user);
        if (!muted)
            _adminMuted.Remove(user);

        SendState(user, false);
        return muted;
    }

    /// <summary>
    /// Muted for this round by an admin, or under a persistent voice chat ban.
    /// </summary>
    public bool IsMuted(NetUserId user)
    {
        return _adminMuted.Contains(user) || _chatBans.IsChatBanned(user, ChatType.Voice);
    }

    /// <summary>
    /// Only humans take part in voice chat. Xenos, Yautja and other species neither speak nor hear it.
    /// </summary>
    public bool IsVoiceCapable(EntityUid uid)
    {
        return !HasComp<XenoComponent>(uid) &&
               !HasComp<YautjaComponent>(uid) &&
               _humanoidQuery.TryComp(uid, out var humanoid) &&
               humanoid.Species == HumanSpecies;
    }

    /// <summary>
    /// Ghosts may listen in but never transmit.
    /// </summary>
    public bool CanHear(EntityUid uid)
    {
        return _ghostQuery.HasComp(uid) || IsVoiceCapable(uid);
    }

    private void HandleFrame(CMUVoiceWebFrame frame, TimeSpan now)
    {
        if (!_player.TryGetSessionById(frame.User, out var session) ||
            session.Status != SessionStatus.InGame ||
            session.AttachedEntity is not { } uid)
        {
            return;
        }

        var speaker = GetSpeaker(frame.User);

        // Push-to-talk only: audio sent without a talk key held is an open mic and is dropped.
        if (!speaker.PushToTalk &&
            !speaker.RadioPushToTalk &&
            now - speaker.TalkKeyReleased > PushToTalkGrace)
        {
            return;
        }

        // Level and loudness are judged on the raw microphone, before masks or effects change it.
        var levels = default(CMUVoiceLevels);
        var decoded = CMUVoiceCodec.Decode(frame.Raw, speaker.Pcm);
        if (decoded)
            levels = speaker.Analyzer.Analyze(speaker.Pcm);

        if (IsMuted(frame.User))
        {
            SendSelf(session, speaker, levels, CMUVoiceSelfFlags.Blocked);
            return;
        }

        var newTransmission = now - speaker.LastAttempt > TransmissionGap;
        speaker.LastAttempt = now;

        if (!CanTransmit(speaker, uid, now, newTransmission))
        {
            SendSelf(session, speaker, levels, CMUVoiceSelfFlags.Blocked);
            return;
        }

        MarkSpeaking(speaker, uid, now);

        if (newTransmission)
        {
            speaker.Loudness.Reset();
            speaker.InfoSent.Clear();
            UpdateEffect(frame.User);
        }

        var range = _range;
        var loudness = _dynamicRange && decoded
            ? speaker.Loudness.Update(speaker.Analyzer.LastDb, _shoutThreshold, _whisperThreshold)
            : CMUVoiceLoudness.Normal;
        range = GetDirectRange(loudness);
        if (IsUsingMegaphone(uid))
            range = MathF.Max(range, _megaphoneRange);

        var selfFlags = loudness switch
        {
            CMUVoiceLoudness.Shout => CMUVoiceSelfFlags.Shout,
            CMUVoiceLoudness.Whisper => CMUVoiceSelfFlags.Whisper,
            _ => CMUVoiceSelfFlags.None,
        };

        if (IsTransmittingRadio(speaker))
            selfFlags |= CMUVoiceSelfFlags.Radio;

        if (speaker.Announcement != null)
            selfFlags |= CMUVoiceSelfFlags.Announcement;

        var onTelephone = TryGetTelephoneListener(uid, out var telephoneListener);
        if (onTelephone)
            selfFlags |= CMUVoiceSelfFlags.Telephone;

        SendSelf(session, speaker, levels, selfFlags);

        var source = GetNetEntity(uid);
        SendAnnouncementFrame(session, speaker, frame, source);

        // On the radio or a console, hear-self comes back through that filter instead of directly.
        var audience = GetAudience(session, uid, IsTransmittingRadio(speaker) || speaker.Announcement != null, range);

        if (audience.Direct.Count > 0)
        {
            SendSpeakerInfo(speaker, uid, audience.Direct);
            _net.ServerSendToMany(new CMUMsgVoiceFrame
            {
                Source = source,
                Speaker = speaker.Id,
                Sequence = frame.Sequence,
                Flags = frame.Flags,
                Payload = frame.Payload,
                Range = range,
            }, audience.Direct);
        }

        foreach (var relay in audience.Relays)
        {
            if (relay.Channels.Count == 0)
                continue;

            SendSpeakerInfo(speaker, uid, relay.Channels);
            _net.ServerSendToMany(new CMUMsgVoiceFrame
            {
                Source = source,
                Speaker = speaker.Id,
                Sequence = frame.Sequence,
                Flags = frame.Flags,
                Payload = frame.Payload,
                Range = range,
                Relayed = true,
                RelayPosition = relay.Position,
                RelayDistance = relay.Distance,
            }, relay.Channels);
        }

        SendRadioFrame(session, speaker, uid, frame, source, now);

        if (onTelephone)
            SendTelephoneFrame(speaker, uid, frame, source, telephoneListener);
    }

    private void SendSelf(ICommonSession session, Speaker speaker, CMUVoiceLevels levels, CMUVoiceSelfFlags flags)
    {
        _net.ServerSendMessage(new CMUMsgVoiceSelf
        {
            Speaker = speaker.Id,
            Level = (byte) Math.Clamp(levels.Overall * 255f, 0f, 255f),
            Flags = flags,
        }, session.Channel);
    }

    /// <summary>
    /// Names the speaker for any listener who hasn't been told yet this transmission, for their speaker list.
    /// </summary>
    private void SendSpeakerInfo(Speaker speaker, EntityUid uid, List<INetChannel> channels, string? channelOverride = null)
    {
        foreach (var channel in channels)
        {
            if (!speaker.InfoSent.Add(channel))
                continue;

            _net.ServerSendMessage(new CMUMsgVoiceSpeakerInfo
            {
                Speaker = speaker.Id,
                Name = Identity.Name(uid, EntityManager),
                Channel = channelOverride ?? (IsTransmittingRadio(speaker) ? speaker.RadioChannel!.ID : string.Empty),
            }, channel);
        }
    }

    private bool CanTransmit(Speaker speaker, EntityUid uid, TimeSpan now, bool recheck)
    {
        if (!recheck && speaker.CheckedEntity == uid && now - speaker.CheckedAt < PermissionRecheck)
            return speaker.Allowed;

        var allowed = CanSpeakPassive(uid) && _actionBlocker.CanSpeak(uid);
        var changed = allowed != speaker.Allowed || speaker.CheckedEntity != uid;

        speaker.CheckedEntity = uid;
        speaker.CheckedAt = now;
        speaker.Allowed = allowed;

        if (changed)
            SendState(speaker.User, false);

        return allowed;
    }

    private bool CanSpeakPassive(EntityUid uid)
    {
        if (_ghostQuery.HasComp(uid) || !IsVoiceCapable(uid))
            return false;

        if (_mobState.IsIncapacitated(uid))
            return false;

        return _speechQuery.TryComp(uid, out var speech) && speech.Enabled;
    }

    private void MarkSpeaking(Speaker speaker, EntityUid uid, TimeSpan now)
    {
        speaker.LastFrame = now;
        if (speaker.SpeakingEntity == uid)
            return;

        if (speaker.SpeakingEntity is { } previous && !TerminatingOrDeleted(previous))
            RemComp<CMUVoiceChatSpeakingComponent>(previous);

        speaker.SpeakingEntity = uid;
        EnsureComp<CMUVoiceChatSpeakingComponent>(uid);
    }

    private void UpdateSpeaking(TimeSpan now)
    {
        foreach (var speaker in _speakers.Values)
        {
            if (speaker.SpeakingEntity is not { } uid || now - speaker.LastFrame < SpeakingTimeout)
                continue;

            speaker.SpeakingEntity = null;
            if (!TerminatingOrDeleted(uid))
                RemComp<CMUVoiceChatSpeakingComponent>(uid);
        }
    }

    private Audience GetAudience(ICommonSession speakerSession, EntityUid speakerUid, bool excludeSelf, float range)
    {
        if (_audiences.TryGetValue(speakerSession.UserId, out var cached))
            return cached;

        var audience = _audiencePool.Count > 0 ? _audiencePool.Pop() : new Audience();
        _audiences[speakerSession.UserId] = audience;

        var speakerXform = Transform(speakerUid);
        if (speakerXform.MapUid is not { } speakerMap)
            return audience;

        var origin = _transform.GetWorldPosition(speakerXform);
        var maxDistance = range + ListenerMargin;
        var maxDistanceSquared = maxDistance * maxDistance;

        _sessions ??= _player.Sessions;
        foreach (var session in _sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { } listener ||
                !_voice.Receives(session.UserId))
            {
                continue;
            }

            if (session == speakerSession)
            {
                if (!excludeSelf && _voice.HearsSelf(session.UserId))
                    audience.Direct.Add(session.Channel);

                continue;
            }

            if (!CanHear(listener))
                continue;

            var listenerXform = Transform(GetListenerEntity(listener));
            if (listenerXform.MapUid is not { } listenerMap)
                continue;

            var position = _transform.GetWorldPosition(listenerXform);
            if (listenerMap == speakerMap)
            {
                if (Vector2.DistanceSquared(position, origin) <= maxDistanceSquared)
                    audience.Direct.Add(session.Channel);

                continue;
            }

            if (GetRelay(audience, speakerMap, origin, listenerMap, maxDistance) is { } relay &&
                relay.Distance + Vector2.Distance(position, relay.Position) <= maxDistance)
            {
                relay.Channels.Add(session.Channel);
            }
        }

        return audience;
    }

    /// <summary>
    /// Voice reaches an adjacent z-level through the nearest floor opening, mirroring cross-z ambient audio.
    /// The opening is resolved once per destination map for each speaker per tick.
    /// </summary>
    private Relay? GetRelay(Audience audience, EntityUid speakerMap, Vector2 origin, EntityUid listenerMap, float maxDistance)
    {
        foreach (var existing in audience.Relays)
        {
            if (existing.Map == listenerMap)
                return existing.Valid ? existing : null;
        }

        var relay = audience.RentRelay(listenerMap);
        if (!TryFindRelayOpening(speakerMap, origin, listenerMap, maxDistance, out var opening))
            return null;

        relay.Valid = true;
        relay.Position = opening;
        relay.Distance = Vector2.Distance(origin, opening);
        return relay;
    }

    private bool TryFindRelayOpening(EntityUid speakerMap, Vector2 origin, EntityUid listenerMap, float maxDistance, out Vector2 opening)
    {
        opening = default;
        if (!_zMapQuery.TryComp(speakerMap, out var speakerZ) ||
            !_zMapQuery.TryComp(listenerMap, out var listenerZ) ||
            !speakerZ.NetworkUid.IsValid() ||
            speakerZ.NetworkUid != listenerZ.NetworkUid ||
            Math.Abs(speakerZ.Depth - listenerZ.Depth) != 1)
        {
            return false;
        }

        // The separating surface belongs to the upper map.
        var surfaceMap = listenerZ.Depth > speakerZ.Depth ? listenerMap : speakerMap;
        return _zLevels.TryFindOpeningNear(surfaceMap, origin, Math.Min(_zOpeningRadius, maxDistance), out opening);
    }

    /// <summary>
    /// Distance the speaker's voice travels to reach the listener, routed through a floor opening when they
    /// are on adjacent z-levels. False when the voice cannot reach the listener at all.
    /// </summary>
    public bool TryGetHearingDistance(EntityUid speaker, EntityUid listener, out float distance)
    {
        distance = 0f;
        var speakerXform = Transform(speaker);
        var listenerXform = Transform(listener);
        if (speakerXform.MapUid is not { } speakerMap || listenerXform.MapUid is not { } listenerMap)
            return false;

        var origin = _transform.GetWorldPosition(speakerXform);
        var position = _transform.GetWorldPosition(listenerXform);
        var maxDistance = _range + ListenerMargin;

        if (speakerMap == listenerMap)
        {
            distance = Vector2.Distance(origin, position);
        }
        else
        {
            if (!TryFindRelayOpening(speakerMap, origin, listenerMap, maxDistance, out var opening))
                return false;

            distance = Vector2.Distance(origin, opening) + Vector2.Distance(opening, position);
        }

        return distance <= maxDistance;
    }

    private float GetDirectRange(CMUVoiceLoudness loudness)
    {
        return loudness switch
        {
            CMUVoiceLoudness.Shout => MathF.Max(_shoutRange, _range),
            CMUVoiceLoudness.Whisper => MathF.Min(_whisperRange, _range),
            _ => _range,
        };
    }

    /// <summary>
    /// Pushes the speaker's current voice effect and muffling to the web server, which applies them
    /// to every frame before it is routed, so proximity, radio and announcements all carry them.
    /// </summary>
    private void UpdateEffect(NetUserId user)
    {
        var speaker = GetSpeaker(user);
        var settings = default(CMUVoiceEffectSettings);

        if (_player.TryGetSessionById(user, out var session) &&
            session.AttachedEntity is { } uid &&
            !_ghostQuery.HasComp(uid))
        {
            var effect = CMUVoiceEffect.None;
            if (speaker.SelectedEffect != CMUVoiceEffect.None && HasVoiceChanger(uid))
                effect = speaker.SelectedEffect;
            else if (TryComp<CMUVoiceChatEffectComponent>(uid, out var builtIn))
                effect = builtIn.Effect;

            settings = new CMUVoiceEffectSettings(effect, GetMuffle(uid), IsUsingMegaphone(uid));
        }

        if (settings == speaker.AppliedEffect)
            return;

        speaker.AppliedEffect = settings;
        _voice.SetEffect(user, settings);
    }

    /// <summary>
    /// Heavy when shut inside something such as a locker or body bag, light behind a gas mask or sealed helmet.
    /// </summary>
    public CMUVoiceMuffle GetMuffle(EntityUid uid)
    {
        if (_container.TryGetContainingContainer((uid, null, null), out var container) &&
            !_hands.IsHolding(container.Owner, uid))
        {
            return CMUVoiceMuffle.Heavy;
        }

        if (_inventory.TryGetSlotEntity(uid, "mask", out var mask) && CoversMouth(mask.Value))
            return CMUVoiceMuffle.Light;

        if (_inventory.TryGetSlotEntity(uid, "head", out var head) &&
            (HasComp<PressureProtectionComponent>(head) || CoversMouth(head.Value)))
        {
            return CMUVoiceMuffle.Light;
        }

        return CMUVoiceMuffle.None;
    }

    private bool CoversMouth(EntityUid item)
    {
        // A mask pulled down off the face no longer covers the mouth.
        if (TryComp<MaskComponent>(item, out var mask) && mask.IsToggled)
            return false;

        return _tag.HasTag(item, GasMaskTag) ||
               TryComp<IngestionBlockerComponent>(item, out var blocker) && blocker.Enabled;
    }

    private bool HasVoiceChanger(EntityUid uid)
    {
        if (!_inventory.TryGetContainerSlotEnumerator(uid, out var slots, SlotFlags.WITHOUT_POCKET))
            return false;

        while (slots.NextItem(out var item))
        {
            if (TryComp<VoiceMaskComponent>(item, out var voiceMask) && voiceMask.Active)
                return true;
        }

        return false;
    }

    private EntityUid GetListenerEntity(EntityUid listener)
    {
        if (_eyeQuery.TryComp(listener, out var eye) && eye.Target is { } target && Exists(target))
            return target;

        return listener;
    }

    private void SetPushToTalk(ICommonSession? session, bool active)
    {
        if (session == null)
            return;

        var speaker = GetSpeaker(session.UserId);
        if (speaker.PushToTalk == active)
            return;

        speaker.PushToTalk = active;
        if (!active)
            speaker.TalkKeyReleased = _timing.RealTime;
        SendState(session.UserId, false);
    }

    private void SendState(NetUserId user, bool force)
    {
        if (!_voice.IsWebConnected(user))
            return;

        var speaker = GetSpeaker(user);
        var state = BuildState(user, speaker);
        if (!force && speaker.LastState == state)
            return;

        speaker.LastState = state;
        _voice.SendState(user, state);
    }

    private CMUVoiceWebState BuildState(NetUserId user, Speaker speaker)
    {
        if (!_player.TryGetSessionById(user, out var session))
            return new CMUVoiceWebState(string.Empty, false, false, IsMuted(user), false, false, false, null);

        if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } uid)
            return new CMUVoiceWebState(session.Name, false, false, IsMuted(user), false, false, false, null);

        var refused = speaker.CheckedEntity == uid &&
                      !speaker.Allowed &&
                      _timing.RealTime - speaker.LastAttempt < RefusalDisplay;

        return new CMUVoiceWebState(
            session.Name,
            true,
            !refused && CanSpeakPassive(uid),
            IsMuted(user),
            speaker.PushToTalk || IsTransmittingRadio(speaker),
            speaker.Announcement != null,
            HasVoiceChanger(uid),
            IsTransmittingRadio(speaker) ? speaker.RadioChannel!.LocalizedName : null);
    }

    private Speaker GetSpeaker(NetUserId user)
    {
        if (_speakers.TryGetValue(user, out var speaker))
            return speaker;

        speaker = new Speaker(user, _nextSpeakerId);
        _nextSpeakerId = _nextSpeakerId == ushort.MaxValue ? (ushort) 1 : (ushort) (_nextSpeakerId + 1);
        _speakers[user] = speaker;
        return speaker;
    }

    private sealed class Speaker(NetUserId user, ushort id)
    {
        public readonly NetUserId User = user;
        public readonly ushort Id = id;
        public bool PushToTalk;
        public bool Allowed;
        public EntityUid? CheckedEntity;
        public TimeSpan CheckedAt;
        public TimeSpan LastAttempt;
        public TimeSpan LastFrame;
        public EntityUid? SpeakingEntity;
        public CMUVoiceWebState? LastState;
        public bool RadioPushToTalk;
        public RadioChannelPrototype? RadioChannel;
        public EntityUid? RadioSource;
        public TimeSpan RadioAudienceExpires;
        public readonly List<INetChannel> RadioAudience = new();
        public readonly CMUVoiceLoudnessTracker Loudness = new();
        public readonly CMUVoiceLevelAnalyzer Analyzer = new();
        public readonly short[] Pcm = new short[CMUVoiceCodec.FrameSamples];
        public CMUVoiceEffect SelectedEffect;
        public CMUVoiceEffectSettings AppliedEffect;
        public Announcement? Announcement;
        public TimeSpan TalkKeyReleased;
        public readonly HashSet<INetChannel> InfoSent = new();
    }

    private sealed class Audience
    {
        public readonly List<INetChannel> Direct = new();
        public readonly List<Relay> Relays = new();
        private readonly Stack<Relay> _relayPool = new();

        public Relay RentRelay(EntityUid map)
        {
            var relay = _relayPool.Count > 0 ? _relayPool.Pop() : new Relay();
            relay.Map = map;
            Relays.Add(relay);
            return relay;
        }

        public void Clear()
        {
            Direct.Clear();
            foreach (var relay in Relays)
            {
                relay.Channels.Clear();
                relay.Valid = false;
                _relayPool.Push(relay);
            }

            Relays.Clear();
        }
    }

    private sealed class Relay
    {
        public readonly List<INetChannel> Channels = new();
        public EntityUid Map;
        public bool Valid;
        public Vector2 Position;
        public float Distance;
    }
}
