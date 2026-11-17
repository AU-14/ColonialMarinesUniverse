using Content.Server.CMU14.Chat; // CMU14
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.CMU14;
using Content.Shared.Audio;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Comms;

public sealed partial class ColonyCommsConsoleSystem : EntitySystem
{
    [Dependency] private RadioSystem _radioSystem = default!;
    [Dependency] private SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ColonyGarrisonLiaisonFundsSystem _funds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColonyCommsConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ColonyCommsConsoleComponent, ColonyCommsConsoleMessage>(OnMessageSent);
        SubscribeLocalEvent<ColonyCommsConsoleComponent, ColonyCommsConsoleSendMessageBuiMsg>(OnSendMessageBuiMsg);
        SubscribeLocalEvent<ColonyCommsConsoleComponent, ColonyCommsConsoleSirenBuiMsg>(OnSirenBuiMsg);
    }

    private void OnUiOpened(EntityUid uid, ColonyCommsConsoleComponent component, BoundUIOpenedEvent args)
    {
        // No need to set UI state for siren toggle; only the garrison liaison office shows anything from it.
        if (args.UiKey is ColonyGarrisonLiaisonUi)
        {
            UpdateLiaisonState(uid, component);
            _funds.SendFunds(uid, args.Actor);
        }
    }

    private void UpdateLiaisonState(EntityUid uid, ColonyCommsConsoleComponent component)
    {
        _ui.SetUiState(uid, ColonyGarrisonLiaisonUi.Key,
            new ColonyGarrisonLiaisonBuiState(component.MaxLength, component.NextSendAt, component.Cooldown));
    }

    private void OnMessageSent(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleMessage args)
    {
        BroadcastColonyAlert(uid, args.Message, component.AnnouncementTitle);
    }

    public void BroadcastColonyAlert(EntityUid source, string message, LocId? title = null)
    {
        // Send to radio channel (for intercoms)
        _radioSystem.SendRadioMessage(source, message, "colonyAlert", source);

        // Send announcement to everyone except xenos // CMU14
        var sender = Loc.GetString(title?.Id ?? "colony-comms-console-announcement-title");
        var announcementSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
        //_chatSystem.DispatchGlobalAnnouncement(message, sender, playSound: true, announcementSound: announcementSound); // CMU14: xenos must not receive colony alerts
        _chatSystem.DispatchFilteredAnnouncement(ColonyAnnouncements.Recipients(_entityManager), message, source, sender, playSound: true, announcementSound: announcementSound); // CMU14
    }

    private void OnSendMessageBuiMsg(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleSendMessageBuiMsg args)
    {
        var message = args.Message?.Trim();
        if (string.IsNullOrEmpty(message))
            return;

        if (component.MaxLength > 0 && message.Length > component.MaxLength)
            message = message[..component.MaxLength];

        var time = _timing.CurTime;
        if (component.Cooldown > TimeSpan.Zero)
        {
            if (time < component.NextSendAt)
            {
                var seconds = (int) Math.Ceiling((component.NextSendAt - time).TotalSeconds);
                _popup.PopupEntity(Loc.GetString("colony-garrison-liaison-cooldown", ("seconds", seconds)), uid, args.Actor);
                return;
            }

            component.NextSendAt = time + component.Cooldown;
            UpdateLiaisonState(uid, component);
        }

        RaiseLocalEvent(uid, new ColonyCommsConsoleMessage(message), false);
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
