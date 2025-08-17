using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.AU14;
using Content.Shared.AU14.util;
using Content.Shared.GameTicking.Components;

namespace Content.Server.AU14;

public sealed class DestroyOnPresetSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DestroyOnPresetComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, DestroyOnPresetComponent component, ComponentStartup args)
    {


        var gameTicker = EntityManager.System<GameTicker>();
        var preset = gameTicker.Preset;



            if (preset != null && preset.ID == component.Preset)
            {

                EntityManager.QueueDeleteEntity(component.Owner);
            }
        }

    }



