using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Specimens;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Specimens;

/// <summary>
///     Tranquiliser shells: a handful holds up to <see cref="CMUTranqShellComponent.PerShell"/> units for
///     each shell, filled by syringe or beaker. The solution follows the shells as the handful is split,
///     merged and loaded, so nothing is duplicated, and a fired shell's dose goes into its slug.
/// </summary>
public sealed class CMUTranqShellSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private CMUSpecimenCaptureSystem _capture = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    public static readonly ProtoId<ReagentPrototype> Somnazine = "CMUSomnazine";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUTranqShellComponent, MapInitEvent>(OnShellMapInit);
        SubscribeLocalEvent<CMUTranqShellComponent, StackCountChangedEvent>(OnShellCountChanged);
        SubscribeLocalEvent<CMUTranqShellComponent, StackSplitEvent>(OnShellSplit);
        SubscribeLocalEvent<CMUTranqShellComponent, StackMergeEvent>(OnShellMerge);
        SubscribeLocalEvent<CMUTranqShellComponent, AmmoShotEvent>(OnShellShot);

        SubscribeLocalEvent<CMUTranqProjectileComponent, ProjectileHitEvent>(OnSlugHit);
    }

    private int Count(EntityUid uid)
    {
        return TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;
    }

    private bool TryGetShellSolution(Entity<CMUTranqShellComponent> ent, out Entity<SolutionComponent> soln)
    {
        soln = default;
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var found, out _))
            return false;

        soln = found.Value;
        return true;
    }

    /// <summary>
    ///     A handful holds a dose per shell. Anything over that is spilled.
    /// </summary>
    private void FitToCount(Entity<CMUTranqShellComponent> ent, Entity<SolutionComponent> soln)
    {
        var capacity = ent.Comp.PerShell * Count(ent);
        var volume = soln.Comp.Solution.Volume;
        if (volume > capacity)
            _solution.SplitSolution(soln, volume - capacity);

        _solution.SetCapacity(soln, capacity);
    }

    private void OnShellMapInit(Entity<CMUTranqShellComponent> ent, ref MapInitEvent args)
    {
        if (TryGetShellSolution(ent, out var soln))
            FitToCount(ent, soln);
    }

    private void OnShellCountChanged(Entity<CMUTranqShellComponent> ent, ref StackCountChangedEvent args)
    {
        if (!TryGetShellSolution(ent, out var soln))
            return;

        if (args.NewCount < args.OldCount && args.OldCount > 0)
        {
            if (ent.Comp.MergingAway)
            {
                // The receiving handful already took this share.
                ent.Comp.MergingAway = false;
            }
            else
            {
                // Shells taken off the handful take their share with them.
                var share = soln.Comp.Solution.Volume * (args.OldCount - args.NewCount) / args.OldCount;
                ent.Comp.Leaving = _solution.SplitSolution(soln, share);
            }
        }

        FitToCount(ent, soln);
    }

    private void OnShellSplit(Entity<CMUTranqShellComponent> ent, ref StackSplitEvent args)
    {
        var leaving = ent.Comp.Leaving;
        ent.Comp.Leaving = null;
        if (!TryComp<CMUTranqShellComponent>(args.NewId, out var newShell) ||
            !TryGetShellSolution((args.NewId, newShell), out var newSoln))
        {
            return;
        }

        // The new handful holds exactly what its shells carried, not its prototype's fill.
        _solution.RemoveAllSolution(newSoln);
        FitToCount((args.NewId, newShell), newSoln);
        if (leaving != null)
            _solution.TryAddSolution(newSoln, leaving);
    }

    private void OnShellMerge(Entity<CMUTranqShellComponent> ent, ref StackMergeEvent args)
    {
        if (args.Recipient != ent.Owner ||
            !TryComp<CMUTranqShellComponent>(args.Donor, out var donor) ||
            !TryGetShellSolution(ent, out var soln) ||
            !TryGetShellSolution((args.Donor, donor), out var donorSoln))
        {
            return;
        }

        var donorCount = Count(args.Donor);
        if (donorCount <= 0)
            return;

        var share = donorSoln.Comp.Solution.Volume * args.Amount / donorCount;
        var moving = _solution.SplitSolution(donorSoln, share);
        _solution.SetCapacity(soln, ent.Comp.PerShell * (Count(ent) + args.Amount));
        _solution.TryAddSolution(soln, moving);
        donor.MergingAway = true;
    }

    private void OnShellShot(Entity<CMUTranqShellComponent> ent, ref AmmoShotEvent args)
    {
        if (!TryGetShellSolution(ent, out var soln) || args.FiredProjectiles.Count == 0)
            return;

        var each = soln.Comp.Solution.Volume / args.FiredProjectiles.Count;
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<CMUTranqProjectileComponent>(projectile, out var slug) ||
                !_solution.TryGetSolution(projectile, slug.Solution, out var slugSoln, out _))
            {
                continue;
            }

            _solution.TryAddSolution(slugSoln.Value, _solution.SplitSolution(soln, each));
        }
    }

    private void OnSlugHit(Entity<CMUTranqProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution) ||
            solution.Volume <= FixedPoint2.Zero)
        {
            return;
        }

        var target = args.Target;
        if (_capture.IsSpecimen(target))
        {
            // No bloodstream to speak of: the Somnazine works on it directly.
            var units = solution.GetTotalPrototypeQuantity(Somnazine);
            if (units > FixedPoint2.Zero)
                _capture.Sedate(target, units.Float() * ent.Comp.SedationPerUnit, ent.Comp.SedationTime);

            _solution.RemoveAllSolution(soln.Value);
            return;
        }

        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        var dose = _solution.SplitSolution(soln.Value, solution.Volume);
        _bloodstream.TryAddToBloodstream((target, bloodstream), dose);
    }
}
