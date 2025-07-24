using Content.Server.Radio.EntitySystems;
using Content.Shared.AU14;
using Content.Shared.Audio;
using Robust.Shared.Audio;

namespace Content.Server.AU14.Comms;

public sealed class ColonyCommsConsoleSystem : EntitySystem
{
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

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
        _radioSystem.SendRadioMessage(uid, args.Message, "colonyalert", uid);
    }

    private void OnSendMessageBuiMsg(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleSendMessageBuiMsg args)
    {
        RaiseLocalEvent(uid, new ColonyCommsConsoleMessage(args.Message), false);
    }

    private void OnSirenBuiMsg(EntityUid uid, ColonyCommsConsoleComponent component, ColonyCommsConsoleSirenBuiMsg args)
    {
        // Toggle siren sound on or off
        if (!component.SirenActive)
        {
            foreach (var sirenComp in EntityManager.EntityQuery<ColonySirenComponent>())
            {
                var sirenUid = sirenComp.Owner;
                if (!EntityManager.HasComponent<AmbientSoundComponent>(sirenUid))
                {
                    var ambient = EntityManager.AddComponent<AmbientSoundComponent>(sirenUid);
                    _ambientSound.SetSound(sirenUid, new SoundPathSpecifier("/Audio/Effects/Vehicle/ambulancesiren.ogg"), ambient);
                    _ambientSound.SetRange(sirenUid, 48f, ambient);
                    _ambientSound.SetVolume(sirenUid, -1f, ambient);
                    _ambientSound.SetAmbience(sirenUid, true, ambient);
                }
                else
                {
                    if (EntityManager.TryGetComponent<AmbientSoundComponent>(sirenUid, out var ambient))
                    {
                        _ambientSound.SetVolume(sirenUid, -2f, ambient);
                    }
                }
            }
            component.SirenActive = true;
        }
        else
        {
            foreach (var sirenComp in EntityManager.EntityQuery<ColonySirenComponent>())
            {
                var sirenUid = sirenComp.Owner;
                if (EntityManager.TryGetComponent<AmbientSoundComponent>(sirenUid, out var ambient))
                {
                    _ambientSound.SetVolume(sirenUid, -999f, ambient);
                }
            }
            component.SirenActive = false;
        }
    }
}
