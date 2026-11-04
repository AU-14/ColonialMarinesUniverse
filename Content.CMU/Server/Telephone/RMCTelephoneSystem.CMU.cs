using Content.Server.CMU14.Telephone;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.Telephone;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.CMU14.Telephone;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Telephone;

public sealed partial class RMCTelephoneSystem
{
    [Dependency] private IGameTiming _cmuTiming = default!;
    [Dependency] private SharedPopupSystem _cmuPopup = default!;
    [Dependency] private RadioSystem _cmuRadio = default!;
    [Dependency] private SharedUserInterfaceSystem _cmuUi = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    private static readonly ProtoId<RadioChannelPrototype>[] CMUDispatchChannels = { "radioCMB", "ColonyMedical" };
    private static readonly TimeSpan CMUDispatchPromptDelay = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan CMUEmergencyCallCooldown = TimeSpan.FromSeconds(120);

    private EntityUid? _cmuDispatcher;
    private readonly Dictionary<EntityUid, TimeSpan> _cmuNextEmergencyCall = new();

    private void CMUInitialize()
    {
        Subs.BuiEvents<RotaryPhoneComponent>(RMCTelephoneUiKey.Key,
            subs => subs.Event<CMUEmergencyCallBuiMsg>(OnEmergencyCallMsg));
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _cmuDispatcher = null;
            _cmuNextEmergencyCall.Clear();
        });
    }

    private void OnEmergencyCallMsg(Entity<RotaryPhoneComponent> ent, ref CMUEmergencyCallBuiMsg args)
    {
        _cmuUi.CloseUi(ent.Owner, RMCTelephoneUiKey.Key);

        var user = args.Actor;
        if (HasComp<RotaryPhoneDialingComponent>(ent) ||
            HasComp<RotaryPhoneReceivingComponent>(ent) ||
            HasComp<CMUEmergencyCallComponent>(ent))
        {
            _cmuPopup.PopupEntity(Loc.GetString("cmu-911-phone-busy"), user, user, PopupType.MediumCaution);
            return;
        }

        if (ent.Comp.Phone is not { } phone)
            return;

        var now = _cmuTiming.CurTime;
        if (_cmuNextEmergencyCall.TryGetValue(ent, out var nextCall) && now < nextCall)
        {
            var seconds = (int) Math.Ceiling((nextCall - now).TotalSeconds);
            _cmuPopup.PopupEntity(Loc.GetString("cmu-911-cooldown", ("seconds", seconds)), user, user, PopupType.MediumCaution);
            return;
        }

        _cmuNextEmergencyCall[ent] = now + CMUEmergencyCallCooldown;
        ent.Comp.Idle = false;
        ent.Comp.LastCall = now;
        Dirty(ent);

        // Dialing with no other phone: the normal hang-up flow ends the call
        EnsureComp<RotaryPhoneDialingComponent>(ent);

        var call = EnsureComp<CMUEmergencyCallComponent>(ent);
        call.Service = args.Service;
        call.Caller = user;
        call.Stage = 0;
        call.AwaitingAnswer = false;
        call.NextPromptAt = now + CMUDispatchPromptDelay;

        PickupPhone(ent, phone, user);
    }

    /// <summary>
    /// Handles speech into a handset on a 911 call. Returns true if the call consumed it.
    /// </summary>
    private bool CMUHandleEmergencyListen(Entity<RMCTelephoneComponent> telephone, EntityUid source, string message)
    {
        if (telephone.Comp.RotaryPhone is not { } rotary ||
            !TryComp(rotary, out CMUEmergencyCallComponent? call))
        {
            return false;
        }

        if (!call.AwaitingAnswer)
            return true;

        switch (call.Stage)
        {
            case 0:
                call.Location = message;
                break;
            case 1:
                call.Details = message;
                break;
        }

        call.AwaitingAnswer = false;
        call.Stage++;
        call.NextPromptAt = _cmuTiming.CurTime + CMUDispatchPromptDelay;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _cmuTiming.CurTime;
        var query = EntityQueryEnumerator<CMUEmergencyCallComponent, RotaryPhoneComponent>();
        while (query.MoveNext(out var uid, out var call, out var rotary))
        {
            // Hung up or snapped back
            if (!HasComp<RotaryPhoneDialingComponent>(uid))
            {
                RemCompDeferred<CMUEmergencyCallComponent>(uid);
                continue;
            }

            if (call.NextPromptAt is not { } promptAt || now < promptAt)
                continue;

            call.NextPromptAt = null;
            switch (call.Stage)
            {
                case 0:
                    DispatcherSay((uid, rotary), Loc.GetString("cmu-911-ask-location"));
                    call.AwaitingAnswer = true;
                    break;
                case 1:
                    DispatcherSay((uid, rotary), Loc.GetString("cmu-911-ask-details"));
                    call.AwaitingAnswer = true;
                    break;
                case 2:
                    DispatcherSay((uid, rotary), Loc.GetString("cmu-911-dispatched"));
                    BroadcastEmergency((uid, rotary), call);
                    call.Stage = 3;
                    break;
            }
        }
    }

    private void DispatcherSay(Entity<RotaryPhoneComponent> rotary, string line)
    {
        if (rotary.Comp.Phone is not { } handset ||
            !CMUTryGetPhoneHolder(handset, out var holder) ||
            !TryComp(holder, out ActorComponent? actor) ||
            !TryComp(handset, out RMCTelephoneComponent? telephone))
        {
            return;
        }

        var message = Loc.GetString("cmu-911-dispatcher-says", ("message", FormattedMessage.EscapeText(line)));
        var sound = _audio.GetAudioPath(_audio.ResolveSound(telephone.SpeakSound));
        _chatManager.ChatMessageToOne(ChatChannel.Local, message, message, handset, false, actor.PlayerSession.Channel, Color.FromHex("#9956D3"), true, sound, -12, hidePopup: true);
    }

    private void BroadcastEmergency(Entity<RotaryPhoneComponent> rotary, CMUEmergencyCallComponent call)
    {
        var service = Loc.GetString(call.Service == CMUEmergencyService.FireMedical
            ? "cmu-911-service-fire-medical"
            : "cmu-911-service-law");
        var caller = Exists(call.Caller) ? Name(call.Caller) : Loc.GetString("cmu-911-unknown-caller");

        var report = Loc.GetString("cmu-911-radio-report",
            ("service", service),
            ("caller", caller),
            ("phone", GetPhoneName(rotary.AsNullable())),
            ("location", call.Location),
            ("details", call.Details));

        // Sent from an off-map speaker so the radio shows "911 Dispatch" and nothing is said near the caller
        var dispatcher = GetDispatcher();
        foreach (var channel in CMUDispatchChannels)
        {
            _cmuRadio.SendRadioMessage(dispatcher, report, channel, dispatcher);
        }
    }

    private EntityUid GetDispatcher()
    {
        if (_cmuDispatcher is { } existing && !TerminatingOrDeleted(existing))
            return existing;

        var dispatcher = Spawn(null, MapCoordinates.Nullspace);
        _metaData.SetEntityName(dispatcher, Loc.GetString("cmu-911-dispatcher-name"));
        _cmuDispatcher = dispatcher;
        return dispatcher;
    }
}
