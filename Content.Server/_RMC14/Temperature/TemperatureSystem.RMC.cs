using Content.Shared.Temperature.Components;

namespace Content.Server.Temperature.Systems;

public sealed partial class TemperatureSystem
{
    internal void RMCSetTemperature(Entity<TemperatureComponent?> entity, float temperature)
    {
        if (!TemperatureQuery.Resolve(entity, ref entity.Comp, false))
            return;

        SetTemperature(entity, temperature);
    }
}
