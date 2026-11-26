using Content.Server.CMU14.Chat; // CMU14
using Content.Server.Radio.EntitySystems;
using Content.Shared.CMU14;
using Content.Shared.Audio;
using Robust.Shared.Audio;

namespace Content.Server.CMU14.Comms;

public sealed partial class ColonyCommsConsoleSystem : EntitySystem
{
    [Dependency] private CMUApolloAnnouncementSystem _apollo = default!;
    [Dependency] private RadioSystem _radioSystem = default!;
    [Dependency] private SharedAmbientSoundSystem _ambientSound = default!;

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
        // No need to set UI state for siren toggle
    }

    private void OnMessageSent(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleMessage args)
    {
        BroadcastColonyAlert(uid, args.Message);
    }

    public void BroadcastColonyAlert(EntityUid source, string message, bool apollo = false)
    {
        var sender = Loc.GetString("colony-comms-console-announcement-title");
        var sound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
        if (apollo)
        {
            _radioSystem.SendRadioMessage(source, message, "colonyAlert", source, _apollo.BroadcastLanguage(source, "colony"));
            _apollo.AnnounceColony(message, sender, sound, source);
            return;
        }

        _radioSystem.SendRadioMessage(source, message, "colonyAlert", source);
        _apollo.AnnounceColony(message, sender, sound, source);
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
