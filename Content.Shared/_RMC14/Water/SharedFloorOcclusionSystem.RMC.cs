using Content.Shared._RMC14.Water;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Movement.Systems;

public abstract partial class SharedFloorOcclusionSystem
{
    [Dependency] private RMCWaterSystem _rmcWater = default!;

    private bool RMCWaterCanCollide(EntityUid water, EntityUid user)
    {
        return _rmcWater.CanCollide(water, user);
    }
}
