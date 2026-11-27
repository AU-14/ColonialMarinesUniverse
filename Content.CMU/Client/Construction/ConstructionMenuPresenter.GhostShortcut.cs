using System.Numerics;
using Content.Shared.Construction.Prototypes;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.Construction.UI;

internal sealed partial class ConstructionMenuPresenter
{
    [Dependency] private readonly IConfigurationManager _inputConfig = default!;
    [Dependency] private readonly IGameTiming _clickTiming = default!;

    private ContainerButton? _lastRecipeButton;
    private TimeSpan _lastRecipeClick;
    private Vector2 _lastRecipePosition;

    private bool TryStartGhostShortcut(ConstructionPrototype recipe, ContainerButton button, BaseButton.ButtonToggledEventArgs args)
    {
        var now = _clickTiming.RealTime;
        var position = args.Event.PointerLocation.Position;
        var doubleClick = _lastRecipeButton == button &&
            now - _lastRecipeClick <= TimeSpan.FromMilliseconds(_inputConfig.GetCVar(CVars.DoubleClickDelay)) &&
            (position - _lastRecipePosition).IsShorterThan(_inputConfig.GetCVar(CVars.DoubleClickRange));
        _lastRecipeButton = doubleClick ? null : button;
        _lastRecipeClick = now;
        _lastRecipePosition = position;

        if (!doubleClick || recipe.Type != ConstructionType.Structure || recipe.RMCPrototype != null)
            return false;

        OnGridViewButtonToggled(recipe, button);
        BuildButtonToggled(true);
        return true;
    }
}
