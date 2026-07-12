using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._CMU14.Medical.Treatment.FieldCare;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.Medical.Treatment.FieldCare;

[UsedImplicitly]
public sealed partial class CMUMedicalFieldCraftingBui : BoundUserInterface
{
    private static readonly CMUFieldTreatmentFamily[] FamilyOrder =
    [
        CMUFieldTreatmentFamily.Hemostatic,
        CMUFieldTreatmentFamily.Antiseptic,
        CMUFieldTreatmentFamily.BurnGel,
        CMUFieldTreatmentFamily.TissueSealant,
        CMUFieldTreatmentFamily.TraumaFoam,
    ];

    [Dependency] private IClyde _displayManager = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly TransformSystem _transform;

    [ViewVariables]
    private CMUMedicalFieldCraftingMenu? _menu;

    public CMUMedicalFieldCraftingBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _transform = EntMan.System<TransformSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<CMUMedicalFieldCraftingMenu>();
        if (State is CMUMedicalFieldCraftingBuiState state)
            Refresh(state);

        var vpSize = _displayManager.ScreenSize;
        var pos = _inputManager.MouseScreenPosition.Position / vpSize;
        if (_player.LocalEntity is { } local)
            pos = _eye.WorldToScreen(_transform.GetMapCoordinates(local).Position) / vpSize;

        _menu.OpenCenteredAt(pos);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CMUMedicalFieldCraftingBuiState crafting)
            Refresh(crafting);
    }

    private void Refresh(CMUMedicalFieldCraftingBuiState state)
    {
        if (_menu is null)
            return;

        var parent = _menu.FindControl<RadialContainer>("Main");
        parent.RemoveAllChildren();

        foreach (var option in OrderedOptions(state.Options))
            AddCraftingButton(option, parent);
    }

    private void AddCraftingButton(CMUMedicalFieldCraftingOption option, RadialContainer parent)
    {
        var productName = _prototypes.TryIndex<EntityPrototype>(option.Product, out var prototype)
            ? prototype.Name
            : option.Product.Id;

        var icon = new EntityPrototypeView
        {
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
            SetSize = new Vector2(54, 54),
            Scale = new Vector2(2.4f, 2.4f),
            Stretch = SpriteView.StretchMode.Fill,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        icon.SetPrototype(option.Product.Id);

        var button = new RadialMenuTextureButton
        {
            EnableAllKeybinds = false,
            StyleClasses = { "RadialMenuButton" },
            SetSize = new Vector2(64, 64),
            ToolTip = Loc.GetString(
                "cmu-field-treatment-craft-tooltip",
                ("product", productName),
                ("cost", option.IngredientCost)),
        };

        button.OnPressed += args =>
        {
            if (args.Event.Function != EngineKeyFunctions.UIClick)
                return;

            SendPredictedMessage(new CMUMedicalFieldCraftingChooseBuiMsg(option.Family, option.BaseKind));
        };

        button.AddChild(icon);
        parent.AddChild(button);
    }

    private static IEnumerable<CMUMedicalFieldCraftingOption> OrderedOptions(
        List<CMUMedicalFieldCraftingOption> options)
    {
        foreach (var family in FamilyOrder)
        {
            foreach (var option in options)
            {
                if (option.BaseKind == CMUFieldTreatmentBaseKind.Gauze && option.Family == family)
                    yield return option;
            }
        }

        for (var i = FamilyOrder.Length - 1; i >= 0; i--)
        {
            var family = FamilyOrder[i];
            foreach (var option in options)
            {
                if (option.BaseKind == CMUFieldTreatmentBaseKind.TraumaDressing && option.Family == family)
                    yield return option;
            }
        }
    }
}
