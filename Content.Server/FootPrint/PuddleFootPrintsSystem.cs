using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.FootPrint;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.FootPrint;

public sealed partial class PuddleFootPrintsSystem : EntitySystem
{
    private static readonly ProtoId<ReagentPrototype> WaterReagent = "Water";

    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PuddleFootPrintsComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<PuddleFootPrintsComponent, StepTriggeredOffEvent>(OnStepTrigger);
    }

    private void OnStepTriggerAttempt(EntityUid uid, PuddleFootPrintsComponent component, ref StepTriggerAttemptEvent args)
    {
        args.Continue |= HasComp<FootPrintsComponent>(args.Tripper);
    }

    private void OnStepTrigger(EntityUid uid, PuddleFootPrintsComponent component, ref StepTriggeredOffEvent args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance)
            || !TryComp<PuddleComponent>(uid, out var puddle)
            || !TryComp<FootPrintsComponent>(args.Tripper, out var tripper)
            || !TryComp<SolutionContainerManagerComponent>(uid, out var solutionManager)
            || !_solutionContainer.ResolveSolution((uid, solutionManager), puddle.SolutionName, ref puddle.Solution, out var solutions))
            return;

        if (solutions.Contents.Count <= 0)
            return;

        CMUUpdateShoeStain(args.Tripper, solutions); // CMU14

        if (!TryGetFootprintReagent(solutions, out var totalSolutionQuantity, out var waterQuantity, out var reagentToTransfer))
            return;

        if (waterQuantity > totalSolutionQuantity * component.OffPercent / 100f ||
            !component.ActivatedEntities.Add(args.Tripper))
        {
            return;
        }

        tripper.ReagentToTransfer = reagentToTransfer;

        if (_appearance.TryGetData(uid, PuddleVisuals.SolutionColor, out var color, appearance)
            && _appearance.TryGetData(uid, PuddleVisuals.CurrentVolume, out var volume, appearance))
            AddColor((Color) color, (float) volume * component.SizeRatio, tripper);

        _solutionContainer.RemoveEachReagent(puddle.Solution.Value, 1);
    }

    private static bool TryGetFootprintReagent(
        Solution solution,
        out float totalQuantity,
        out float waterQuantity,
        out string? reagentToTransfer)
    {
        totalQuantity = 0f;
        waterQuantity = 0f;
        reagentToTransfer = null;
        var largestQuantity = 0f;

        foreach (var reagentQuantity in solution.Contents)
        {
            var quantity = (float) reagentQuantity.Quantity;
            totalQuantity += quantity;

            if (reagentQuantity.Reagent.Prototype == WaterReagent)
                waterQuantity += quantity;

            if (quantity <= largestQuantity)
                continue;

            largestQuantity = quantity;
            reagentToTransfer = reagentQuantity.Reagent.Prototype;
        }

        return totalQuantity > 0f && reagentToTransfer != null;
    }

    private static void AddColor(Color col, float quantity, FootPrintsComponent component)
    {
        component.PrintsColor = component.ColorQuantity == 0f
            ? col
            : Color.InterpolateBetween(component.PrintsColor, col, component.ColorInterpolationFactor);
        component.ColorQuantity += quantity;
    }
}
