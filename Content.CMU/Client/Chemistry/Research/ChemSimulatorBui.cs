using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared._CMU14.Chemistry.Reagent;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Client._AU14.Chemistry.Research;

[UsedImplicitly]
public sealed partial class ChemSimulatorBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _protoman = default!;
    [Dependency] private SharedReagentGeneratorSystem _gen = default!;


    private ChemSimulatorWindow? _window;
    

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ChemSimulatorWindow>();
        ButtonGroup _mode = new ButtonGroup(true);
        _window.Add.Group = _mode;
        _window.Relate.Group = _mode;
        _window.Amplify.Group = _mode;
        _window.Suppress.Group = _mode;

        _window.Finalize.OnPressed += _ => SendPredictedMessage(new ChemSimulatorFinalizeBuiMsg());
        _window.Simulate.OnPressed += _ => SendPredictedMessage(new ChemSimulatorAttemptSimulateBuiMsg());
        _window.Override.OnPressed += _ => SendPredictedMessage(new ChemSimulatorToggleOverrideBuiMsg());
        _window.EjectReference.OnPressed += _ =>
        SendPredictedMessage(new ChemSimulatorEjectBuiMsg(true, EntMan.GetNetEntity(PlayerManager.LocalEntity)));
        _window.EjectTarget.OnPressed += _ =>
        SendPredictedMessage(new ChemSimulatorEjectBuiMsg(false, EntMan.GetNetEntity(PlayerManager.LocalEntity)));
        _window.Amplify.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickModeBuiMsg(ChemSimulatorMode.Amplify));
        _window.Suppress.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickModeBuiMsg(ChemSimulatorMode.Suppress));
        _window.Relate.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickModeBuiMsg(ChemSimulatorMode.Relate));
        _window.Add.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickModeBuiMsg(ChemSimulatorMode.Add));

        if (State is ChemSimulatorBuiState s)
        {
            RefreshState(s);
        }
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ChemSimulatorBuiState s)
            RefreshState(s);
    }
    private void RefreshState(ChemSimulatorBuiState state)
    {
        if (_window is null || !EntMan.TryGetComponent<ChemSimulatorComponent>(Owner, out _))
            return;

        bool CanEjectTarget = ((state.Target is not null ? true : false) && state.Stage == ChemSimulatorStage.Off);
        bool CanEjectRef = ((state.Reference is not null ? true : false) && state.Stage == ChemSimulatorStage.Off);
        bool LockControl = state.Stage != ChemSimulatorStage.Off;
        bool isPicking = state.Stage == ChemSimulatorStage.Final;
        bool canSimulate = (state.Ready && state.Stage == ChemSimulatorStage.Off);

        _window.Status.Text = state.StatusBar;
        _window.CreditsBar.Value = state.Credits;
        _window.Credits.Text = Loc.GetString("research-sim-ui-credits", ("NUM", state.Credits));
        var props = _protoman.GetInstances<ReagentPropertyPrototype>();
        _window.Simulate.Disabled = !canSimulate;
        var recigroup = new ButtonGroup();
        _window.Finalize.Disabled = true;
        _window.PickRecipeContainer.RemoveAllChildren();
        if (state.Stage == ChemSimulatorStage.Final)
        {
            _window.Controls.Visible = false;
            _window.ControlsFinalize.Visible = true;
            foreach (var recipick in state.RecipeOptions)
            {
                var recibutton = new Button();
                recibutton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickRecipeChemBuiMsg(recipick.Item1));
                recibutton.Text = recipick.Item1;
                recibutton.StyleClasses.Add("ButtonSquare");
                recibutton.Group = recigroup;
                if (state.RecipePicked is not null && state.RecipePicked == recipick.Item1)
                    recibutton.Disabled = true; //can't do Pressed because then that mucks with the group
                _window.PickRecipeContainer.AddChild(recibutton);
            }
            if (state.RecipePicked is not null)
                _window.Finalize.Disabled = false;
        }
        
        _window.Override.Pressed = state.Override;
        
        _window.NoDat.Visible = true;
        _window.ModeChange.Visible = false;
        _window.ModeRelateAdd.Visible = false;
        _window.TargetPropertyContainer.RemoveAllChildren();
        _window.ReferencePropertyContainer.RemoveAllChildren();
        _window.EjectTarget.Disabled = true;
        
        _window.EjectReference.Disabled = true;
        
        _window.TargPickBox.Visible = false;
        _window.RefPickBox.Visible = false;

        

        _window.Amplify.Disabled = LockControl;
        _window.Suppress.Disabled = LockControl;
        _window.Relate.Disabled = LockControl;
        _window.Add.Disabled = LockControl;

        _window.Override.Disabled = LockControl;

        _window.TargPickBox.Visible = false;
        _window.RefPickBox.Visible = false;
        _window.Overdose.Text = (state.Overdose is not null) ?
            Loc.GetString("research-sim-ui-overdose", ("NUM", state.Overdose.Value)) : Loc.GetString("research-sim-ui-no-overdose");
        _window.SimCost.Text = (state.Cost is not null) ?
            Loc.GetString("research-sim-ui-sim-cost", ("NUM", state.Cost.Value)) : Loc.GetString("research-sim-ui-cost-null");
        _window.TargetName.Text = (state.TargetProp is not null) ?
            Loc.GetString("research-sim-ui-target-name", ("NAME", state.TargetProp)) : Loc.GetString("research-sim-ui-no-targ-chem");
        _window.ReferenceName.Text = (state.ReferenceProp is not null) ?
            Loc.GetString("research-sim-ui-ref-name", ("NAME", state.ReferenceProp)) : Loc.GetString("research-sim-ui-no-ref-chem");
        

        switch (state.Mode)
        {
            case ChemSimulatorMode.Amplify:
                _window.Amplify.Pressed = true;
                break;
            case ChemSimulatorMode.Suppress:
                _window.Suppress.Pressed = true;
                break;
            case ChemSimulatorMode.Relate:
                _window.Relate.Pressed = true;
                break;
            case ChemSimulatorMode.Add:
                _window.Add.Pressed = true;
                break;
            default:
                break;
        }
        var targetgroup = new ButtonGroup();
        var referencegroup = new ButtonGroup();
        _window.EjectTarget.Disabled = !CanEjectTarget | LockControl;
        _window.EjectReference.Disabled = !CanEjectRef | LockControl;
        if (state.Target is not null && state.Costs.Count > 0)
        {
            foreach (var kvp in state.Target.Value.Effects)
            {
                var propdat = props[kvp.Key];
                var propbutton = new Button();
                propbutton.Access = AccessLevel.Public;
                //propbutton.Name = "TargProps." + propdat.ID;
                if (state.Mode != ChemSimulatorMode.Add)
                {
                    propbutton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickTargetPropertyBuiMsg(kvp.Key));
                    propbutton.Group = targetgroup;
                }
                propbutton.StyleClasses.Add("ButtonSquare");
                propbutton.Text = propdat.Code + " " + kvp.Value.ToString();
                bool isLocked = false;
                //this fucking sucks
                if (state.ReferenceProp is not null)
                {
                    foreach (var list in _gen.UnfoldedConflicts)
                    {
                        if ((list[0] == state.ReferenceProp && list[1] == kvp.Key) |
                            (list[0] == kvp.Key && list[1] == state.ReferenceProp))
                        {
                            propbutton.ToolTip = Loc.GetString("research-sim-ui-selected-ref-conflict");
                            isLocked = true;
                            break;
                        }
                    }
                }
                propbutton.Disabled = LockControl || (!state.Override ? isLocked : false);
                if (state.TargetProp is not null && state.TargetProp == kvp.Key)
                    propbutton.Pressed = true;
                _window.TargetPropertyContainer.AddChild(propbutton);
            }
        }
        if (state.Reference is not null)
        {
            foreach (var kvp in state.Reference.Value.Effects)
            {
                var propdat = props[kvp.Key];
                var propbutton = new Button();
                propbutton.Access = AccessLevel.Public;
                //propbutton.Name = "RefProps." + propdat.ID;
                propbutton.OnPressed += _ => SendPredictedMessage(new ChemSimulatorPickReferencePropertyBuiMsg(kvp.Key));
                propbutton.StyleClasses.Add("ButtonSquare");
                propbutton.Text = propdat.Code + " " + kvp.Value.ToString();
                var isLocked = false;
                if (state.TargetProp is not null)
                {
                    foreach (var list in _gen.UnfoldedConflicts)
                    {
                        if ((list[0] == state.TargetProp && list[1] == kvp.Key) |
                            (list[0] == kvp.Key && list[1] == state.TargetProp))
                        {
                            propbutton.ToolTip = Loc.GetString("research-sim-ui-selected-targ-conflict");
                            isLocked = true;
                            break;
                        }
                    }
                }
                propbutton.Disabled = LockControl || (!state.Override ? isLocked : false);
                propbutton.Group = referencegroup;
                if (state.ReferenceProp is not null && state.ReferenceProp == kvp.Key)
                    propbutton.Pressed = true;
                _window.ReferencePropertyContainer.AddChild(propbutton);
            }
        }
        if (state.Mode == ChemSimulatorMode.Amplify || state.Mode == ChemSimulatorMode.Suppress)
        {
            if (state.Target is not null && state.Costs.Count > 0)
            {
                _window.NoDat.Visible = false;
                _window.ModeChange.Visible = true;
                _window.TargetPropertyContainer.Orphan();
                _window.ModeChangeTargDatCon.AddChild(_window.TargetPropertyContainer);
                _window.TargetPropertyContainer.SetPositionLast();
            }
            if (state.TargetProp is not null)
            {
                _window.TargPickBox.Visible = true;
                _window.PropPickName.Text = props[state.TargetProp].LocalizedName;
                _window.PropPickDesc.Text = props[state.TargetProp].LocalizedDescription;
                _window.SimPrice.Text = Loc.GetString("research-sim-ui-price", ("COST", state.Costs[state.TargetProp]));
            }
        }
        else
        {
            if (state.Target is not null && state.Reference is not null && state.Costs.Count > 0)
            {
                _window.NoDat.Visible = false;
                _window.ModeRelateAdd.Visible = true;
                _window.TargetPropertyContainer.Orphan();
                _window.ModeRelateAddTargDatCon.AddChild(_window.TargetPropertyContainer);
                _window.TargetPropertyContainer.SetPositionLast();
                if (state.Mode == ChemSimulatorMode.Add)
                {
                    if (state.ReferenceProp is not null)
                    {
                        _window.RefPickBox.Visible = true;
                        _window.RAPropPickName.Text = props[state.ReferenceProp].LocalizedName;
                        _window.RAPropPickDesc.Text = props[state.ReferenceProp].LocalizedDescription;
                        _window.RASimPrice.Text = Loc.GetString("research-sim-ui-price", ("COST", state.Costs[state.ReferenceProp]));
                    }
                }
                else
                {
                    if (state.TargetProp is not null)
                    {
                        _window.RefPickBox.Visible = true;
                        _window.RAPropPickName.Text = props[state.TargetProp].LocalizedName;
                        _window.RAPropPickDesc.Text = props[state.TargetProp].LocalizedDescription;
                        _window.RASimPrice.Text = Loc.GetString("research-sim-ui-price", ("COST", state.Costs[state.TargetProp]));
                    }
                }
            }
            
        }
    }
}
