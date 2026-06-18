using Content.Shared.FootPrint;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.FootPrint;

public sealed partial class PuddleFootPrintsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    private const string HumanBloodReagent = "Blood";
    private const string SynthBloodReagent = "RMCSynthBlood";
    private const string YautjaBloodReagent = "CMUYautjaBlood";
    private const string XenoBloodReagent = "FluorosulfuricAcid";

    private static readonly Color HumanBloodColor = Color.FromHex("#980002");
    private static readonly Color SynthBloodColor = Color.FromHex("#EEEEEE");
    private static readonly Color XenoBloodColor = Color.FromHex("#bed700");
    private static readonly Color YautjaBloodColor = Color.FromHex("#81d434");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PuddleFootPrintsComponent, EndCollideEvent>(OnStepTrigger);
    }

    private void OnStepTrigger(EntityUid uid, PuddleFootPrintsComponent component, ref EndCollideEvent args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance)
            || !TryComp<PuddleComponent>(uid, out var puddle)
            || !TryComp<FootPrintsComponent>(args.OtherEntity, out var tripper)
            || !TryComp<SolutionContainerManagerComponent>(uid, out var solutionManager)
            || !_solutionContainer.ResolveSolution((uid, solutionManager), puddle.SolutionName, ref puddle.Solution, out var solutions))
            return;

        if (solutions.Contents.Count <= 0)
            return;

        var dominantReagent = solutions.Contents[0].Reagent.Prototype;
        var dominantQuantity = 0f;
        var totalSolutionQuantity = 0f;
        var waterQuantity = 0f;

        foreach (var content in solutions.Contents)
        {
            var quantity = (float) content.Quantity;
            totalSolutionQuantity += quantity;

            if (content.Reagent.Prototype == "Water")
                waterQuantity += quantity;

            if (quantity <= dominantQuantity)
                continue;

            dominantQuantity = quantity;
            dominantReagent = content.Reagent.Prototype;
        }

        if (totalSolutionQuantity <= 0f ||
            waterQuantity / (totalSolutionQuantity / 100f) > component.OffPercent)
            return;

        tripper.ReagentToTransfer = dominantReagent;

        if (_appearance.TryGetData(uid, PuddleVisuals.SolutionColor, out var color, appearance)
            && _appearance.TryGetData(uid, PuddleVisuals.CurrentVolume, out var volume, appearance))
        {
            var stainColor = TryGetBloodColor(dominantReagent, out var bloodColor)
                ? bloodColor
                : (Color) color;

            AddColor(stainColor, (float) volume * component.SizeRatio, tripper);
        }

        _solutionContainer.RemoveEachReagent(puddle.Solution.Value, 1);
    }

    private void AddColor(Color col, float quantity, FootPrintsComponent component)
    {
        component.PrintsColor = component.ColorQuantity == 0f || component.PrintsColor.A <= 0f
            ? col
            : Color.InterpolateBetween(component.PrintsColor, col, component.ColorInterpolationFactor);
        component.ColorQuantity += quantity;
    }

    private static bool TryGetBloodColor(string reagent, out Color color)
    {
        color = reagent switch
        {
            HumanBloodReagent => HumanBloodColor,
            XenoBloodReagent => XenoBloodColor,
            SynthBloodReagent => SynthBloodColor,
            YautjaBloodReagent => YautjaBloodColor,
            _ => HumanBloodColor,
        };

        return reagent is HumanBloodReagent or XenoBloodReagent or SynthBloodReagent or YautjaBloodReagent;
    }
}
