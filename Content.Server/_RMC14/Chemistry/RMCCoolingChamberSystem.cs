using Content.Server.Power.EntitySystems;
using Content.Server.Storage.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Storage.Components;

namespace Content.Server._RMC14.Chemistry;

public sealed class RMCCoolingChamberSystem : EntitySystem
{
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCCoolingChamberComponent, StorageAfterCloseEvent>(OnStorageClosed);
    }

    // PVS still needs a moment to hide moved entities after the door closes.
    // Deferring one tick avoids dirty-network-component assertions.
    private void OnStorageClosed(Entity<RMCCoolingChamberComponent> ent, ref StorageAfterCloseEvent args)
    {
        ent.Comp.SkipNextTick = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RMCCoolingChamberComponent, EntityStorageComponent>();
        while (query.MoveNext(out var uid, out var cooler, out var storage))
        {
            if (storage.Open || storage.Contents.ContainedEntities.Count == 0)
                continue;

            if (!_power.IsPowered(uid))
                continue;

            if (cooler.SkipNextTick)
            {
                cooler.SkipNextTick = false;
                continue;
            }

            foreach (var contained in storage.Contents.ContainedEntities)
            {
                if (!TryComp<SolutionContainerManagerComponent>(contained, out var manager))
                    continue;

                var energy = -cooler.CoolPerSecond * frameTime;
                foreach (var (_, solution) in _solutionContainer.EnumerateSolutions((contained, manager)))
                {
                    if (solution.Comp.Solution.Temperature <= cooler.TargetTemperature)
                        continue;

                    _solutionContainer.AddThermalEnergy(solution, energy);
                }
            }
        }
    }
}
