using Content.Client._RMC14.UserInterface;
using Content.Client.CMU14.UI;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using static Content.Client.CMU14.Chemistry.Research.ChemSimulatorWindow;
using static Content.Client.CMU14.UI.CMUMachineUiStyle;

namespace Content.Client.CMU14.Chemistry.Research;

[UsedImplicitly]
public sealed partial class ChemSimulatorBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private ChemSimulatorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ChemSimulatorWindow>();

        _window.AmplifyButton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickModeBuiMsg(ChemSimulatorMode.Amplify));
        _window.SuppressButton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickModeBuiMsg(ChemSimulatorMode.Suppress));
        _window.RelateButton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickModeBuiMsg(ChemSimulatorMode.Relate));
        _window.AddButton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickModeBuiMsg(ChemSimulatorMode.Add));

        _window.EjectTargetButton.OnPressed += _ =>
            SendPredictedMessage(new ChemSimulatorEjectBuiMsg(false, EntMan.GetNetEntity(PlayerManager.LocalEntity)));
        _window.EjectReferenceButton.OnPressed += _ =>
            SendPredictedMessage(new ChemSimulatorEjectBuiMsg(true, EntMan.GetNetEntity(PlayerManager.LocalEntity)));
        _window.SimulateButton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorAttemptSimulateBuiMsg());
        _window.OverrideButton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorToggleOverrideBuiMsg());
        _window.FinalizeButton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorFinalizeBuiMsg());

        if (State is ChemSimulatorBuiState s)
            RefreshState(s);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ChemSimulatorBuiState s)
            RefreshState(s);
    }

    private void RefreshState(ChemSimulatorBuiState state)
    {
        if (_window is not { IsOpen: true })
            return;

        _window.SetCredits(state.SampleData);

        var locked = state.Stage != ChemSimulatorStage.Off;
        _window.SimulateButton.Disabled = locked || !state.Ready;

        _window.AmplifyButton.Disabled = locked;
        _window.SuppressButton.Disabled = locked;
        _window.RelateButton.Disabled = locked;
        _window.AddButton.Disabled = locked;

        _window.OverrideButton.Disabled = locked;
        _window.OverrideButton.Pressed = state.Override;
        _window.OverrideButton.Accent = state.Override ? ButtonCautionColor : ButtonColor;

        var targetItem = GetSlotItem("target");
        var referenceItem = GetSlotItem("reference");

        _window.EjectTargetButton.Disabled = targetItem == null || locked;
        _window.EjectReferenceButton.Disabled = referenceItem == null || locked;

        SetModeButton(_window.AmplifyButton, state.Mode == ChemSimulatorMode.Amplify);
        SetModeButton(_window.SuppressButton, state.Mode == ChemSimulatorMode.Suppress);
        SetModeButton(_window.RelateButton, state.Mode == ChemSimulatorMode.Relate);
        SetModeButton(_window.AddButton, state.Mode == ChemSimulatorMode.Add);

        _window.TargetIcon.SetEntity(targetItem);
        _window.ReferenceIcon.SetEntity(referenceItem);

        var noData = Loc.GetString("research-sim-ui-no-data");
        _window.SetNotice(_window.TargetStatusPanel, _window.TargetNameLabel, state.Target?.Name ?? noData,
            targetItem != null ? NoticeColor : NoticeDangerColor);
        _window.SetNotice(_window.ReferenceStatusPanel, _window.ReferenceNameLabel, state.Reference?.Name ?? noData,
            referenceItem != null ? NoticeColor : NoticeDangerColor);

        var costProperty = state.Mode == ChemSimulatorMode.Add ? state.ReferenceProp : state.TargetProp;
        int? cost = costProperty != null && state.Costs.TryGetValue(costProperty, out var propertyCost)
            ? propertyCost
            : state.Cost;
        _window.CostLabel.Text = cost is { } c
            ? Loc.GetString("research-sim-ui-cost", ("NUM", c))
            : Loc.GetString("research-sim-ui-cost-null");
        _window.OverdoseLabel.Text = state.Overdose is { } od
            ? Loc.GetString("research-sim-ui-overdose", ("NUM", od))
            : Loc.GetString("research-sim-ui-no-overdose");

        var statusText = string.IsNullOrEmpty(state.StatusBar)
            ? Loc.GetString("research-sim-ui-status-not-ready")
            : state.StatusBar;
        var statusColor = state.Stage switch
        {
            ChemSimulatorStage.Final => NoticeSuccessColor,
            ChemSimulatorStage.Failure => NoticeDangerColor,
            > ChemSimulatorStage.Final => NoticeInfoColor,
            _ => state.Ready ? NoticeColor : NoticeDangerColor,
        };
        _window.SetNotice(_window.StatusPanel, _window.StatusLabel, statusText, statusColor);

        var showReference = state.Mode is ChemSimulatorMode.Relate or ChemSimulatorMode.Add;

        _window.TargetPropertiesPanel.Visible = state.Target != null && state.Costs.Count > 0;
        _window.ReferenceSection.Visible = showReference;
        _window.ReferencePropertiesPanel.Visible = state.Reference != null;

        PopulateProperties(_window.TargetPropertiesBox, state, state.Target, state.TargetProp, state.ReferenceProp,
            state.Mode != ChemSimulatorMode.Add, "research-sim-ui-selected-ref-conflict", id =>
                SendPredictedMessage(new ChemSimulatorPickTargetPropertyBuiMsg(id)));

        if (showReference)
        {
            PopulateProperties(_window.ReferencePropertiesBox, state, state.Reference, state.ReferenceProp, state.TargetProp,
                true, "research-sim-ui-selected-targ-conflict", id =>
                    SendPredictedMessage(new ChemSimulatorPickReferencePropertyBuiMsg(id)));
        }

        var picking = state.Stage == ChemSimulatorStage.Final;
        _window.RecipePickerSection.Visible = picking;
        PopulateRecipeCandidates(picking ? state.RecipeOptions : [], state.RecipePicked);

        _window.FinalizeButton.Disabled = !picking || state.RecipePicked is null;
    }

    private EntityUid? GetSlotItem(string slotId)
    {
        if (!EntMan.System<SharedContainerSystem>().TryGetContainer(Owner, slotId, out var container))
            return null;

        return container.ContainedEntities.Count > 0 ? container.ContainedEntities[0] : null;
    }

    private void PopulateProperties(
        BoxContainer box,
        ChemSimulatorBuiState state,
        GeneratedReagentData? data,
        string? selectedId,
        string? opposingSelectedId,
        bool selectable,
        string conflictTooltip,
        Action<string> onSelect)
    {
        if (data is not { } reagent)
        {
            box.RemoveAllChildren();
            return;
        }

        var locked = state.Stage != ChemSimulatorStage.Off;
        var conflicts = EntMan.System<SharedReagentGeneratorSystem>().UnfoldedConflicts;

        var index = 0;
        foreach (var (propertyId, level) in reagent.Effects)
        {
            var name = propertyId;
            var description = string.Empty;
            string? categoryIcon = null;
            if (_prototype.TryIndex<ReagentPropertyPrototype>(propertyId, out var proto))
            {
                name = proto.LocalizedName;
                description = proto.LocalizedDescription;
                categoryIcon = proto.Hint switch
                {
                    ReagentPropertyHintEnum.Positive => CMUIcons.SquarePlus,
                    ReagentPropertyHintEnum.Negative => CMUIcons.SquareMinus,
                    _ => null,
                };
            }

            if (state.Costs.TryGetValue(propertyId, out var price))
            {
                var priceText = Loc.GetString("research-sim-ui-price", ("COST", price));
                description = description.Length > 0 ? description + "\n" + priceText : priceText;
            }

            var conflicting = opposingSelectedId != null && conflicts.Exists(pair =>
                pair[0] == opposingSelectedId && pair[1] == propertyId ||
                pair[0] == propertyId && pair[1] == opposingSelectedId);

            SelectButton button;
            if (index < box.ChildCount && box.GetChild(index) is SelectButton existing)
            {
                button = existing;
            }
            else
            {
                button = new SelectButton { HorizontalExpand = true, ClipText = true };
                box.AddChild(button);
            }

            var iconRow = button.SetIcon(categoryIcon ?? CMUIcons.SquarePlus,
                Loc.GetString("research-sim-ui-property", ("NAME", name), ("LEVEL", level)));
            iconRow.Icon.Visible = categoryIcon != null;
            button.ToggleMode = selectable;
            button.Pressed = propertyId == selectedId;
            button.Disabled = selectable && conflicting && !state.Override;
            button.ToolTip = conflicting && !state.Override ? Loc.GetString(conflictTooltip) : description;
            button.MouseFilter = selectable && !locked ? Control.MouseFilterMode.Stop : Control.MouseFilterMode.Ignore;
            button.Id = propertyId;
            button.OnSelect = selectable ? onSelect : null;

            button.Accent = button.Pressed ? ButtonSelectedColor : ButtonColor;

            index++;
        }

        box.RemoveChildrenAfter(index);
    }

    private void PopulateRecipeCandidates(List<(string, int, bool, bool)> candidates, string? picked)
    {
        var box = _window!.RecipeCandidatesBox;

        for (var i = 0; i < candidates.Count; i++)
        {
            var id = candidates[i].Item1;
            var name = EntMan.System<RMCReagentSystem>().TryIndex(id, out var reagent) ? reagent.LocalizedName : id;

            SelectButton button;
            if (i < box.ChildCount && box.GetChild(i) is SelectButton existing)
            {
                button = existing;
            }
            else
            {
                button = new SelectButton { HorizontalExpand = true, ClipText = true };
                box.AddChild(button);
            }

            button.Text = name;
            button.ToggleMode = true;
            button.Pressed = picked == id;
            button.Id = id;
            button.OnSelect = pick => SendPredictedMessage(new ChemSimulatorPickRecipeChemBuiMsg(pick));

            button.Accent = button.Pressed ? ButtonSelectedColor : ButtonColor;
        }

        box.RemoveChildrenAfter(candidates.Count);
    }
}
