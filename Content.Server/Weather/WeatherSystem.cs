using Content.Shared.Weather;
using Robust.Server.GameStates;

namespace Content.Server.Weather;

public sealed partial class WeatherSystem : SharedWeatherSystem
{
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WeatherStatusEffectComponent, ComponentInit>(OnCompInit);
        SubscribeLocalEvent<WeatherStatusEffectComponent, ComponentShutdown>(OnCompShutdown);
    }

    private void OnCompInit(Entity<WeatherStatusEffectComponent> ent, ref ComponentInit args)
    {
        // The map entitiy itself is networked by PVS if the player is on that map but not anything inside a container,
        // So we need to add an overridce to make sure the client sees it.
        _pvs.AddGlobalOverride(ent);
    }

    private void OnCompShutdown(Entity<WeatherStatusEffectComponent> ent, ref ComponentShutdown args)
    {
        _pvs.RemoveGlobalOverride(ent);
    }
}
