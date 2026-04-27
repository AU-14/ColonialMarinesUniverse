using Content.Shared._RMC14.Ordnance;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._RMC14.Ordnance;

public sealed class RMCChemicalAmmoTransferSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCChemicalAmmoTransferComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(Entity<RMCChemicalAmmoTransferComponent> ent, ref AmmoShotEvent args)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var sourceSolution, out var source))
            return;

        if (args.FiredProjectiles.Count == 0 || source.Volume <= 0)
            return;

        var targets = new List<Entity<SolutionComponent>>();
        foreach (var projectile in args.FiredProjectiles)
        {
            if (_solution.TryGetSolution(projectile, ent.Comp.Solution, out var projectileSolution, out _))
                targets.Add(projectileSolution.Value);
        }

        if (targets.Count == 0)
            return;

        var amountPerProjectile = source.Volume / targets.Count;
        foreach (var target in targets)
        {
            var transferred = _solution.SplitSolution(sourceSolution.Value, amountPerProjectile);
            _solution.TryAddSolution(target, transferred);
        }
    }
}
