using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._RMC14.Temperature;
using Content.Shared.Temperature.Components;

namespace Content.Server._RMC14.Temperature;

public sealed partial class RMCTemperatureSystem : SharedRMCTemperatureSystem
{
    [Dependency] private TemperatureSystem _temperature = default!;

    public override float GetTemperature(EntityUid entity)
    {
        return CompOrNull<TemperatureComponent>(entity)?.Temperature ?? 0;
    }

    public override void ForceChangeTemperature(EntityUid entity, float temperature)
    {
        if (!TryComp(entity, out TemperatureComponent? temperatureComp))
            return;

        var heat = (temperature - temperatureComp.Temperature) * temperatureComp.HeatCapacity;
        _temperature.ChangeHeat((entity, temperatureComp), heat, true);
    }

    public override bool TryGetCurrentTemperature(EntityUid uid, out float temperature)
    {
        if (!TryComp(uid, out TemperatureComponent? temperatureComp))
        {
            temperature = 0;
            return true;
        }

        temperature = temperatureComp.Temperature;
        return false;
    }
}
