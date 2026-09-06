using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Camera;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Player;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraMicrophoneSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xforms = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private CameraSessionSystem _cameraSessions = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurveillanceCameraMicrophoneComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SurveillanceCameraMicrophoneComponent, ListenEvent>(RelayEntityMessage);
        SubscribeLocalEvent<SurveillanceCameraMicrophoneComponent, ListenAttemptEvent>(CanListen);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourceXform = Transform(ev.Source);
        var sourcePos = _xforms.GetWorldPosition(sourceXform, xformQuery);

        // This function ensures that chat popups appear on camera views that have connected microphones.
        var cameras = EntityQueryEnumerator<SurveillanceCameraMicrophoneComponent,
            ActiveListenerComponent,
            SurveillanceCameraComponent,
            TransformComponent>();
        while (cameras.MoveNext(out var uid, out _, out _, out _, out var xform))
        {
            var sessions = _cameraSessions.GetSessionsForCamera(uid);
            if (sessions.Count == 0)
                continue;

            // get range to camera. This way wispers will still appear as obfuscated if they are too far from the camera's microphone
            var range = (xform.MapID != sourceXform.MapID)
                ? -1
                : (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();

            if (range < 0 || range > ev.VoiceRange)
                continue;

            foreach (var session in sessions)
            {
                // if the player has not already received the chat message, send it to them but don't log it to the chat
                // window. This is simply so that it appears in camera.
                ev.Recipients.TryAdd(session.Viewer, new ICChatRecipientData(range, false, true));
            }
        }
    }

    private void OnInit(EntityUid uid, SurveillanceCameraMicrophoneComponent component, ComponentInit args)
    {
        if (component.Enabled)
            EnsureComp<ActiveListenerComponent>(uid).Range = component.Range;
        else
            RemCompDeferred<ActiveListenerComponent>(uid);
    }

    public void CanListen(EntityUid uid, SurveillanceCameraMicrophoneComponent microphone, ListenAttemptEvent args)
    {
        // TODO maybe just make this a part of ActiveListenerComponent?
        if (_whitelistSystem.IsBlacklistPass(microphone.Blacklist, args.Source))
            args.Cancel();
    }

    public void RelayEntityMessage(EntityUid uid, SurveillanceCameraMicrophoneComponent component, ListenEvent args)
    {
        if (!TryComp(uid, out SurveillanceCameraComponent? camera))
            return;

        var ev = new SurveillanceCameraSpeechSendEvent(args.Source, args.Message);

        foreach (var monitor in _cameraSessions.GetSessionsForCamera(uid)
                     .Select(session => session.Receiver)
                     .Distinct())
        {
            RaiseLocalEvent(monitor, ev);
        }
    }

    public void SetEnabled(EntityUid uid, bool value, SurveillanceCameraMicrophoneComponent? microphone = null)
    {
        if (!Resolve(uid, ref microphone))
            return;

        if (value == microphone.Enabled)
            return;

        microphone.Enabled = value;

        if (value)
            EnsureComp<ActiveListenerComponent>(uid).Range = microphone.Range;
        else
            RemCompDeferred<ActiveListenerComponent>(uid);
    }
}

public sealed partial class SurveillanceCameraSpeechSendEvent : EntityEventArgs
{
    public EntityUid Speaker { get; }
    public string Message { get; }

    public SurveillanceCameraSpeechSendEvent(EntityUid speaker, string message)
    {
        Speaker = speaker;
        Message = message;
    }
}
