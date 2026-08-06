using Content.Shared.Chemistry.Components;
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

    private void OnStepTriggerAttempt(Entity<PuddleFootPrintsComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue |= HasComp<FootPrintsComponent>(args.Tripper);
    }

    private void OnStepTrigger(Entity<PuddleFootPrintsComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (!TryComp(ent, out AppearanceComponent? appearance) ||
            !TryComp(ent, out PuddleComponent? puddle) ||
            !TryComp(args.Tripper, out FootPrintsComponent? tripper) ||
            !_solutionContainer.ResolveSolution(ent.Owner, puddle.SolutionName, ref puddle.Solution, out var solution) ||
            solution.Contents.Count == 0)
        {
            return;
        }

        CMUUpdateShoeStain(args.Tripper, solution);

        if (!TryGetFootprintReagent(solution,
                out var totalSolutionQuantity,
                out var waterQuantity,
                out var reagentToTransfer))
        {
            return;
        }

        if (waterQuantity > totalSolutionQuantity * ent.Comp.OffPercent / 100f ||
            !ent.Comp.ActivatedEntities.Add(args.Tripper))
        {
            return;
        }

        tripper.ReagentToTransfer = reagentToTransfer;

        if (_appearance.TryGetData(ent, PuddleVisuals.SolutionColor, out var color, appearance) &&
            _appearance.TryGetData(ent, PuddleVisuals.CurrentVolume, out var volume, appearance))
        {
            AddColor((Color) color, (float) volume * ent.Comp.SizeRatio, tripper);
        }

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

    private static void AddColor(Color color, float quantity, FootPrintsComponent component)
    {
        component.PrintsColor = component.ColorQuantity == 0f
            ? color
            : Color.InterpolateBetween(component.PrintsColor, color, component.ColorInterpolationFactor);
        component.ColorQuantity += quantity;
    }
}
