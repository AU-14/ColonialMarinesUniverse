using Content.Server.CMU14.Chat; // CMU14
using Content.Server.CMU14.Round.ColonyShift;
using Content.Server.Chat.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Shared.CMU14;
using Content.Shared.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;

namespace Content.Server.CMU14.Comms;

public sealed partial class ColonyCommsConsoleSystem : EntitySystem
{
    [Dependency] private RadioSystem _radioSystem = default!;
    [Dependency] private SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private ColonyRotationRuleSystem _rotation = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColonyCommsConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ColonyCommsConsoleComponent, ColonyCommsConsoleMessage>(OnMessageSent);
        SubscribeLocalEvent<ColonyCommsConsoleComponent, ColonyCommsConsoleSendMessageBuiMsg>(OnSendMessageBuiMsg);
        SubscribeLocalEvent<ColonyCommsConsoleComponent, ColonyCommsConsoleSirenBuiMsg>(OnSirenBuiMsg);
        SubscribeLocalEvent<ColonyCommsConsoleComponent, ColonyCommsConsoleTransportCallBuiMsg>(OnTransportCall);
        SubscribeLocalEvent<ColonyCommsConsoleComponent, ColonyCommsConsoleTransportRecallBuiMsg>(OnTransportRecall);
    }

    private void OnUiOpened(EntityUid uid, ColonyCommsConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateTransportUi(uid);
    }

    // greenshift only, the rule pushes this whenever the transport changes phase
    public void RefreshTransportUi()
    {
        var query = EntityQueryEnumerator<ColonyCommsConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, ColonyCommsConsoleUI.Key))
                UpdateTransportUi(uid);
        }
    }

    private void UpdateTransportUi(EntityUid uid)
    {
        _rotation.TryGetTransport(out var status, out var at, out var canCall, out var canRecall);
        _ui.SetUiState(uid, ColonyCommsConsoleUI.Key, new ColonyCommsConsoleBuiState(status, at, canCall, canRecall));
    }

    private void OnTransportCall(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleTransportCallBuiMsg args)
    {
        if (!_accessReader.IsAllowed(args.Actor, uid))
        {
            _popup.PopupEntity(Loc.GetString("colony-rotation-access-denied"), uid, args.Actor);
            return;
        }

        if (!_rotation.TryCall(out var reason) && reason != null)
            _popup.PopupEntity(reason, uid, args.Actor);

        UpdateTransportUi(uid);
    }

    private void OnTransportRecall(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleTransportRecallBuiMsg args)
    {
        if (!_accessReader.IsAllowed(args.Actor, uid))
        {
            _popup.PopupEntity(Loc.GetString("colony-rotation-access-denied"), uid, args.Actor);
            return;
        }

        if (!_rotation.TryRecall(out var reason) && reason != null)
            _popup.PopupEntity(reason, uid, args.Actor);

        UpdateTransportUi(uid);
    }

    private void OnMessageSent(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleMessage args)
    {
        BroadcastColonyAlert(uid, args.Message);
    }

    public void BroadcastColonyAlert(EntityUid source, string message)
    {
        // Send to radio channel (for intercoms)
        _radioSystem.SendRadioMessage(source, message, "colonyAlert", source);

        // Send announcement to everyone except xenos // CMU14
        var sender = Loc.GetString("colony-comms-console-announcement-title");
        var announcementSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
        //_chatSystem.DispatchGlobalAnnouncement(message, sender, playSound: true, announcementSound: announcementSound); // CMU14: xenos must not receive colony alerts
        _chatSystem.DispatchFilteredAnnouncement(ColonyAnnouncements.Recipients(_entityManager), message, source, sender, playSound: true, announcementSound: announcementSound); // CMU14
    }

    private void OnSendMessageBuiMsg(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleSendMessageBuiMsg args)
    {
        RaiseLocalEvent(uid, new ColonyCommsConsoleMessage(args.Message), false);
    }

    private void OnSirenBuiMsg(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleSirenBuiMsg args)
    {
        var sirenActive = !component.SirenActive;
        var sirenQuery = AllEntityQuery<ColonySirenComponent>();
        while (sirenQuery.MoveNext(out var sirenUid, out _))
        {
            if (sirenActive)
            {
                if (!HasComp<AmbientSoundComponent>(sirenUid))
                {
                    var ambient = AddComp<AmbientSoundComponent>(sirenUid);
                    _ambientSound.SetSound(sirenUid, new SoundPathSpecifier("/Audio/CMU14/Machines/ColonySiren.ogg"), ambient);
                    _ambientSound.SetRange(sirenUid, 48f, ambient);
                    _ambientSound.SetVolume(sirenUid, -1f, ambient);
                    _ambientSound.SetAmbience(sirenUid, true, ambient);
                }
                else if (TryComp<AmbientSoundComponent>(sirenUid, out var ambient))
                    _ambientSound.SetVolume(sirenUid, -2f, ambient);
            }
            else if (TryComp<AmbientSoundComponent>(sirenUid, out var ambient))
                _ambientSound.SetVolume(sirenUid, -999f, ambient); // mute
        }
        // Persist state on console (even when there's no siren comps)
        component.SirenActive = sirenActive;
    }
}
