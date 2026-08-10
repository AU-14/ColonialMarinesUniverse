using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Round;

/// <summary>
/// Applies compatibility side effects for an accepted candidate force transition.
/// </summary>
public sealed partial class RoundForceSelectionSideEffectSystem : EntitySystem
{
    [Dependency] private IntelSystem _intel = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RequisitionsSystem _requisitions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundForceSelectionAppliedEvent>(OnForceSelectionApplied);
    }

    private void OnForceSelectionApplied(ref RoundForceSelectionAppliedEvent ev)
    {
        _requisitions.ReapplyPlatoonCatalogs();

        var team = ev.Side switch
        {
            RoundSide.Govfor => Team.GovFor,
            RoundSide.Opfor => Team.OpFor,
            _ => throw new ArgumentOutOfRangeException(nameof(ev.Side), ev.Side, null),
        };
        string? techTree = null;
        if (ev.CurrentForce is { } force &&
            _prototypes.TryIndex<PlatoonPrototype>(force.Value, out var platoon))
        {
            techTree = platoon.TechTree;
        }

        _intel.SetTeamTechTreeOverride(team, techTree);
    }
}
