using Content.Shared._RMC14.Ordnance;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Ordnance;

/// <summary>
///     Resolves an inserted ordnance item into the chemistry profile that scanners and simulators should use.
///     This keeps rocket ammo, mortar shells, grenades, mines, and direct casing samples on one estimation path.
/// </summary>
public sealed class RMCOrdnanceSampleResolverSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public bool TryResolveSample(EntityUid uid, out RMCOrdnanceSampleData sample)
    {
        if (TryResolveCasingSample(uid, out sample))
            return true;

        if (TryResolveChemicalAmmoSample(uid, out sample))
            return true;

        sample = default;
        return false;
    }

    private bool TryResolveCasingSample(EntityUid uid, out RMCOrdnanceSampleData sample)
    {
        if (!TryComp(uid, out RMCChembombCasingComponent? casing) ||
            !_solution.TryGetSolution(uid, casing.ChemicalSolution, out _, out var solution))
        {
            sample = default;
            return false;
        }

        var estimate = RMCOrdnanceYieldEstimator.Estimate(solution, _prototype, RMCOrdnanceYieldEstimator.FromCasing(casing));
        sample = new RMCOrdnanceSampleData(MetaData(uid).EntityName, estimate, (float) casing.MaxVolume);
        return true;
    }

    private bool TryResolveChemicalAmmoSample(EntityUid uid, out RMCOrdnanceSampleData sample)
    {
        if (!TryComp(uid, out RMCChemicalAmmoTransferComponent? transfer) ||
            !TryComp(uid, out CartridgeAmmoComponent? cartridge) ||
            !_solution.TryGetSolution(uid, transfer.Solution, out _, out var solution) ||
            !_prototype.TryIndex(cartridge.Prototype, out var projectileProto) ||
            !projectileProto.TryGetComponent(out RMCChembombCasingComponent? projectileCasing, _compFactory))
        {
            sample = default;
            return false;
        }

        var estimate = RMCOrdnanceYieldEstimator.Estimate(solution, _prototype, RMCOrdnanceYieldEstimator.FromCasing(projectileCasing));
        sample = new RMCOrdnanceSampleData(MetaData(uid).EntityName, estimate, (float) projectileCasing.MaxVolume);
        return true;
    }
}

/// <summary>
///     Common simulation payload returned by <see cref="RMCOrdnanceSampleResolverSystem"/>.
/// </summary>
public readonly record struct RMCOrdnanceSampleData(
    string Name,
    RMCOrdnanceYieldEstimate Estimate,
    float MaxVolume);
