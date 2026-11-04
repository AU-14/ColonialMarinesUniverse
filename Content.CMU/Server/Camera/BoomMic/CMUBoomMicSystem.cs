using System.Linq;
using Content.Server.Camera;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Camera.Television;
using Content.Server.CMU14.Chat;
using Content.Shared.Camera;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Camera.BoomMic;

/// <summary>
/// Relays what a <see cref="CMUBoomMicComponent"/> hears to the TVs watching its synced camera, and what a
/// <see cref="CMUCameraMicrophoneComponent"/> camera hears to its own. TVs repeat speech as "television set (Name)"
/// and emotes the same way. A camera gets each line once, however many of its mics heard it.
/// </summary>
public sealed partial class CMUBoomMicSystem : EntitySystem
{
    [Dependency] private CameraSessionSystem _cameraSessions = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUTelevisionSystem _television = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    // Emotes with quoted speech raise the emote and then the speech in the same tick; the emote already carries it.
    private readonly HashSet<EntityUid> _emotedThisTick = new();
    private GameTick _emoteTick;

    // What each camera has already sent its TVs this tick, so a camera mic and a boom mic hearing the same line
    // repeat it once. Keyed by speaker rather than text: a mic further away hears a whisper garbled.
    private readonly HashSet<(EntityUid Camera, EntityUid Source, InGameICChatType Type)> _relayedThisTick = new();
    private GameTick _relayTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUBoomMicComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CMUBoomMicComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUBoomMicComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUBoomMicComponent, ListenAttemptEvent>(OnListenAttempt);
        SubscribeLocalEvent<CMUBoomMicComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<CMUCameraMicrophoneComponent, ComponentInit>(OnCameraMicInit);
        SubscribeLocalEvent<CMUCameraMicrophoneComponent, ListenEvent>(OnCameraListen);
        SubscribeLocalEvent<CMUEntityEmotedEvent>(OnEmoted);
    }

    private void OnInit(Entity<CMUBoomMicComponent> ent, ref ComponentInit args)
    {
        EnsureComp<ActiveListenerComponent>(ent).Range = ent.Comp.ListenRange;
    }

    private void OnCameraMicInit(Entity<CMUCameraMicrophoneComponent> ent, ref ComponentInit args)
    {
        // The camera may already listen further for something else; only ever widen it.
        var listener = EnsureComp<ActiveListenerComponent>(ent);
        listener.Range = MathF.Max(listener.Range, ent.Comp.ListenRange);
    }

    private void OnAfterInteract(Entity<CMUBoomMicComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!_whitelist.IsWhitelistPass(ent.Comp.CameraWhitelist, target))
            return;

        args.Handled = true;
        ent.Comp.Camera = target;
        _popup.PopupEntity(Loc.GetString("cmu-boom-mic-synced", ("camera", target)), target, args.User);
    }

    private void OnExamined(Entity<CMUBoomMicComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Camera is { } camera && !TerminatingOrDeleted(camera))
            args.PushMarkup(Loc.GetString("cmu-boom-mic-examine-synced", ("camera", camera)));
        else
            args.PushMarkup(Loc.GetString("cmu-boom-mic-examine-unsynced"));
    }

    private void OnListenAttempt(Entity<CMUBoomMicComponent> ent, ref ListenAttemptEvent args)
    {
        // TVs repeating the mic would feed back into it.
        if (HasComp<CameraNetworkReceiverComponent>(args.Source) || !TryGetCamera(ent, out _))
            args.Cancel();
    }

    private void OnListen(Entity<CMUBoomMicComponent> ent, ref ListenEvent args)
    {
        if (_emoteTick == _timing.CurTick && _emotedThisTick.Contains(args.Source))
            return;

        if (!TryGetCamera(ent, out var camera))
            return;

        RelaySpeech(camera, args.Source, args.Message);
    }

    private void OnCameraListen(Entity<CMUCameraMicrophoneComponent> ent, ref ListenEvent args)
    {
        if (_emoteTick == _timing.CurTick && _emotedThisTick.Contains(args.Source))
            return;

        // The camera's listener can reach further for its other microphone, so check the built-in one's range.
        if (HasComp<CameraNetworkReceiverComponent>(args.Source) || !InRange(ent, args.Source, ent.Comp.ListenRange))
            return;

        RelaySpeech(ent, args.Source, args.Message);
    }

    private void RelaySpeech(EntityUid camera, EntityUid source, string message)
    {
        var nameEv = new TransformSpeakerNameEvent(source, Name(source));
        RaiseLocalEvent(source, nameEv);
        Relay(camera, source, message, InGameICChatType.Speak, nameEv.VoiceName);
    }

    private void OnEmoted(ref CMUEntityEmotedEvent args)
    {
        if (_emoteTick != _timing.CurTick)
        {
            _emoteTick = _timing.CurTick;
            _emotedThisTick.Clear();
        }

        _emotedThisTick.Add(args.Source);

        if (HasComp<CameraNetworkReceiverComponent>(args.Source))
            return;

        var name = Name(Identity.Entity(args.Source, EntityManager));
        var cameras = EntityQueryEnumerator<CMUCameraMicrophoneComponent>();
        while (cameras.MoveNext(out var uid, out var cameraMic))
        {
            if (InRange(uid, args.Source, cameraMic.ListenRange))
                Relay(uid, args.Source, args.Action, InGameICChatType.Emote, name);
        }

        var mics = EntityQueryEnumerator<CMUBoomMicComponent>();
        while (mics.MoveNext(out var uid, out var mic))
        {
            if (InRange(uid, args.Source, mic.ListenRange) && TryGetCamera((uid, mic), out var camera))
                Relay(camera, args.Source, args.Action, InGameICChatType.Emote, name);
        }
    }

    private bool InRange(EntityUid listener, EntityUid source, float range)
    {
        var listenerCoords = _transform.GetMapCoordinates(listener);
        var sourceCoords = _transform.GetMapCoordinates(source);
        return listenerCoords.MapId == sourceCoords.MapId &&
               (listenerCoords.Position - sourceCoords.Position).Length() <= range;
    }

    /// <summary>
    /// Gets the synced camera if the mic is held by someone and close enough to it.
    /// </summary>
    private bool TryGetCamera(Entity<CMUBoomMicComponent> ent, out EntityUid camera)
    {
        camera = default;
        if (ent.Comp.Camera is not { } synced || TerminatingOrDeleted(synced))
            return false;

        if (!_container.TryGetContainingContainer(ent.Owner, out var container) ||
            !_hands.IsHolding(container.Owner, ent))
        {
            return false;
        }

        var micCoords = _transform.GetMapCoordinates(ent);
        var cameraCoords = _transform.GetMapCoordinates(synced);
        if (micCoords.MapId != cameraCoords.MapId ||
            (micCoords.Position - cameraCoords.Position).Length() > ent.Comp.CameraRange)
        {
            return false;
        }

        camera = synced;
        return true;
    }

    private void Relay(EntityUid camera, EntityUid source, string message, InGameICChatType type, string speakerName)
    {
        if (_relayTick != _timing.CurTick)
        {
            _relayTick = _timing.CurTick;
            _relayedThisTick.Clear();
        }

        if (!_relayedThisTick.Add((camera, source, type)))
            return;

        // Sets left tuned to the camera keep playing with nobody watching them.
        foreach (var tv in _cameraSessions.GetSessionsForCamera(camera)
                     .Select(session => session.Receiver)
                     .Concat(_television.GetTunedTo(camera))
                     .Distinct())
        {
            if (TerminatingOrDeleted(tv))
                continue;

            var name = Loc.GetString("speech-name-relay", ("speaker", Name(tv)), ("originalName", speakerName));

            // Ghost range limited so many TVs do not flood ghost chat, the same as camera speakers.
            _chat.TrySendInGameICMessage(tv,
                message,
                type,
                ChatTransmitRange.GhostRangeLimit,
                nameOverride: name,
                ignoreActionBlocker: true);
        }
    }
}
