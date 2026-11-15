// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Electric;
using Content.Shared.FixedPoint;
using Content.Shared.PowerCell;
using Content.Shared.Tools.Components;

namespace Content.Server.CMU14.Electric;

// Turns the charge of the cell into the fuel of the welder: the tool is topped up to FuelReady once a second as long as the cell
// has what a unit costs. Nothing is burnt while the welder is off, so nothing is drawn.
public sealed class ElectricWelderSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;

    private const float Interval = 0.5f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ElectricWelderComponent, WelderComponent>();
        while (query.MoveNext(out var uid, out var electric, out var welder))
        {
            electric.NextCheck -= frameTime;
            if (electric.NextCheck > 0f)
                continue;

            electric.NextCheck = Interval;

            if (!_solutions.TryGetSolution(uid, welder.FuelSolutionName, out var soln, out var solution))
                continue;

            var have = (float) solution.GetTotalPrototypeQuantity(welder.FuelReagent);
            var missing = electric.FuelReady - have;
            if (missing < 0.1f)
                continue;

            // Only whole tenths of a unit are made, and only when the cell can pay for them.
            var cost = missing * electric.ChargePerFuel;
            if (!_cell.TryUseCharge(uid, cost))
                continue;

            _solutions.TryAddReagent(soln.Value, welder.FuelReagent, FixedPoint2.New(missing), out _);
        }
    }
}
