using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Body;

public abstract partial class SharedRMCBloodstreamSystem : EntitySystem
{
    [Dependency] private RMCReagentSystem _rmcReagent = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    private readonly List<ReagentId> _reagentsToRemove = new();

    public virtual bool TryGetBloodSolution(
        EntityUid uid,
        [NotNullWhen(true)] out Solution? solution)
    {
        if (!TryComp(uid, out BloodstreamComponent? bloodstream))
        {
            solution = null;
            return false;
        }

        return _solution.TryGetSolution(uid, bloodstream.BloodSolutionName, out _, out solution);
    }

    /// <summary>
    /// Resolves the unified bloodstream solution entity and returns an isolated snapshot containing only foreign
    /// chemicals. Reference blood reagents are never exposed through the snapshot.
    /// </summary>
    public virtual bool TryGetChemicalSolution(
        EntityUid uid,
        out Entity<SolutionComponent> solutionEnt,
        [NotNullWhen(true)] out Solution? solution)
    {
        if (!TryComp(uid, out BloodstreamComponent? bloodstream)
            || !_solution.TryGetSolution(
                uid,
                bloodstream.BloodSolutionName,
                out var nullableSolutionEnt,
                out var bloodSolution))
        {
            solutionEnt = default;
            solution = null;
            return false;
        }

        solutionEnt = nullableSolutionEnt.Value;
        var referenceReagents = _bloodstream.GetReferenceReagentPrototypes((uid, bloodstream));

        var snapshot = bloodSolution.Clone();
        solution = snapshot.SplitSolutionWithout(snapshot.Volume, referenceReagents);
        return true;
    }

    /// <summary>
    /// Gets the effective current blood volume and the configured normal reference volume.
    /// Foreign bloodstream chemicals do not contribute to either value.
    /// </summary>
    public bool TryGetBloodReadout(EntityUid uid, out FixedPoint2 current, out FixedPoint2 normal)
    {
        current = FixedPoint2.Zero;
        normal = FixedPoint2.Zero;
        if (!TryComp(uid, out BloodstreamComponent? bloodstream))
            return false;

        normal = _bloodstream.GetReferenceVolume((uid, bloodstream));
        if (normal <= FixedPoint2.Zero)
        {
            return false;
        }

        current = normal * _bloodstream.GetBloodLevel((uid, bloodstream));
        return true;
    }

    public virtual bool IsBleeding(EntityUid uid)
    {
        return CompOrNull<BloodstreamComponent>(uid) is { BleedAmount: > 0 };
    }

    public void RemoveBloodstreamToxins(EntityUid body, FixedPoint2 amount)
    {
        if (!TryGetChemicalSolution(body, out var solutionEnt, out var chemicals))
            return;

        _reagentsToRemove.Clear();
        foreach (var content in chemicals.Contents)
        {
            if (!_rmcReagent.TryIndex(content.Reagent, out var reagent))
                continue;

            if (!reagent.Toxin)
                continue;

            _reagentsToRemove.Add(content.Reagent);
        }

        foreach (var remove in _reagentsToRemove)
        {
            _solution.RemoveReagent(solutionEnt, remove, amount);
        }
    }

    public void RemoveBloodstreamChemical(EntityUid body, ProtoId<ReagentPrototype> reagentId, FixedPoint2 amount)
    {
        if (!TryGetChemicalSolution(body, out var solutionEnt, out var chemicals)
            || !chemicals.ContainsPrototype(reagentId))
        {
            return;
        }

        _solution.RemoveReagent(solutionEnt, reagentId, amount);
    }

    public bool RemoveBloodstreamAlcohols(EntityUid body, FixedPoint2 amount)
    {
        if (!TryGetChemicalSolution(body, out var solutionEnt, out var chemicals))
            return false;

        _reagentsToRemove.Clear();
        foreach (var content in chemicals.Contents)
        {
            if (!_rmcReagent.TryIndex(content.Reagent, out var reagent))
                continue;

            if (!reagent.Alcohol)
                continue;

            _reagentsToRemove.Add(content.Reagent);
        }

        var alcoholRemoved = _reagentsToRemove.Count > 0;

        foreach (var remove in _reagentsToRemove)
        {
            _solution.RemoveReagent(solutionEnt, remove, amount);
        }

        return alcoholRemoved;
    }
}
