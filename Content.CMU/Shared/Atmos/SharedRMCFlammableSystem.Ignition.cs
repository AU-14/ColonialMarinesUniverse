namespace Content.Shared._RMC14.Atmos;

public abstract partial class SharedRMCFlammableSystem
{
    private readonly List<Entity<RMCIgniteOnCollideComponent>> _cmuIgnitionSources = [];
}
