using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Nutrition;

/// <summary>
///     Ported from cmss13 PR 7895. An MRE spread, tube or can whose contents can be put onto food: using it on a food
///     moves some of it into the food's own solution, so it's eaten along with it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUFoodSpreadComponent : Component
{
    [DataField]
    public string Solution = "drink";

    [DataField]
    public FixedPoint2 TransferAmount = 2;
}

public sealed class CMUFoodSpreadSystem : EntitySystem
{
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SolutionTransferSystem _transfer = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFoodSpreadComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(SolutionTransferSystem)]);
        SubscribeLocalEvent<CMUFoodSpreadComponent, ExaminedEvent>(OnExamined);
    }

    private void OnAfterInteract(Entity<CMUFoodSpreadComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == ent.Owner)
            return;

        // Only real food, not drinks: those already take a pour through the normal solution transfer.
        if (!TryComp(target, out EdibleComponent? edible) || edible.Edible != IngestionSystem.Food)
            return;

        if (!_solution.TryGetSolution(target, edible.Solution, out var targetSoln, out var targetSolution))
            return;

        args.Handled = true;
        if (_openable.IsClosed(ent))
        {
            _popup.PopupClient(Loc.GetString("cmu-food-spread-closed", ("spread", ent)), ent, args.User);
            return;
        }

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var sourceSoln, out var sourceSolution) ||
            sourceSolution.Volume <= 0)
        {
            _popup.PopupClient(Loc.GetString("cmu-food-spread-empty", ("spread", ent)), ent, args.User);
            return;
        }

        if (targetSolution.AvailableVolume <= 0)
        {
            _popup.PopupClient(Loc.GetString("cmu-food-spread-full", ("food", target)), target, args.User);
            return;
        }

        _transfer.Transfer(args.User, ent, sourceSoln.Value, target, targetSoln.Value, ent.Comp.TransferAmount);
    }

    private void OnExamined(Entity<CMUFoodSpreadComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-food-spread-examine"));
    }
}
