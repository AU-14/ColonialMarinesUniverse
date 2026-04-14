using Content.Server.GameTicking;
using Content.Shared.AU14.util;

namespace Content.Server.AU14;

public sealed class DestroyOnPresetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DestroyOnPresetComponent, MapInitEvent>(OnStartup);
    }

    private void OnStartup(EntityUid uid, DestroyOnPresetComponent component, MapInitEvent args)
    {
        var gameTicker = EntityManager.System<GameTicker>();
        var preset = gameTicker.Preset;

        if (preset != null)
        {
            var matches = preset.ID == component.Preset;

            // If inverted is true, delete when it does NOT match. Otherwise delete when it matches.
            if ((matches && !component.Inverted) || (!matches && component.Inverted))
            {
                EntityManager.QueueDeleteEntity(uid);
            }
        }
    }

}
