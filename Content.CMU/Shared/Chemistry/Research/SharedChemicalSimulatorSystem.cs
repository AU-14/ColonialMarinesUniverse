using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

public abstract partial class SharedChemicalSimulatorSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _con = default!;
    [Dependency] private SharedPopupSystem _pop = default!;
    [Dependency] private SharedReagentGeneratorSystem _gen = default!;
    [Dependency] private SharedResearchDataTerminalSystem _dat = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _protoman = default!;
    [Dependency] private MetaDataSystem _mets = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedAppearanceSystem _app = default!;


    private ISawmill _sawmill = default!;


    private readonly int AddBulk = 2;
    private readonly int MaxPropCost = 8;
    private readonly int AddValue = 3;
    private readonly int MultAnomalous = 5;
    private readonly int MultRare = 3;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChemSimulatorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ChemSimulatorComponent, BoundUIOpenedEvent>(OnBuiOpened);
        Subs.BuiEvents<ChemSimulatorComponent>(ChemSimulatorUI.Key, subs =>
        {
            subs.Event<ChemSimulatorPickTargetPropertyBuiMsg>(OnTargetPick);
            subs.Event<ChemSimulatorPickReferencePropertyBuiMsg>(OnRefPick);
            subs.Event<ChemSimulatorPickModeBuiMsg>(OnChangeMode);
            subs.Event<ChemSimulatorPickRecipeChemBuiMsg>(OnPickRecipe);
            subs.Event<ChemSimulatorAttemptSimulateBuiMsg>(OnAttemptSimulate);
            subs.Event<ChemSimulatorToggleOverrideBuiMsg>(OnToggleOverride);
            subs.Event<ChemSimulatorEjectBuiMsg>(OnEject);
            subs.Event<ChemSimulatorFinalizeBuiMsg>(OnFinalize);
        });
        _sawmill = _log.GetSawmill("reagent");
    }


    private void OnInteractUsing(Entity<ChemSimulatorComponent> ent, ref InteractUsingEvent args)
    {
        //TODO: skillcheck here

        _con.TryGetContainer(ent.Owner, "target", out var targetCon);
        _con.TryGetContainer(ent.Owner, "reference", out var referenceCon);
        // why is the second | load bearing here ????
        if (targetCon is null || referenceCon is null)
            return;
        if (TryComp<ResearchNoteComponent>(args.Used, out var noteComp))
        {
            if (targetCon.Count == 0 ||
                (ent.Comp.Mode == ChemSimulatorMode.Relate || ent.Comp.Mode == ChemSimulatorMode.Add) &&
               referenceCon.Count == 0 && noteComp.Data is not null)
            {
                //nothing!
            }
            else
            {
                _pop.PopupEntity(Loc.GetString("research-sim-already-inserted"), ent.Owner);
                return;
            }
        }
        if (TryComp<ResearchReportComponent>(args.Used, out var reportComp))
        {
            if (targetCon.Count == 0 && reportComp.Data is not null)
            {
                _con.Insert(args.Used, targetCon);
                ent.Comp.Ready = CheckReady(ent);
            }
            else if ((ent.Comp.Mode == ChemSimulatorMode.Relate || ent.Comp.Mode == ChemSimulatorMode.Add) &&
                referenceCon.Count == 0 && reportComp.Data is not null)
            {
                ent.Comp.TargetProperty = null;
                _con.Insert(args.Used, referenceCon);
                ent.Comp.Ready = CheckReady(ent);
            }
            else
            {
                if (reportComp.Data is not null)
                    _pop.PopupEntity(Loc.GetString("research-sim-already-inserted"), ent.Owner);
                else
                    _pop.PopupEntity(Loc.GetString("research-sim-refused"), ent.Owner);
                return;

            }
        }
        else
        {
            _pop.PopupEntity(Loc.GetString("research-sim-refuses", ("SCANNER", ent.Owner), ("PAPER", args.Used)), ent.Owner);
            return;
        }
        UpdatePropertyCosts(ent);
        ent.Comp.InsertTimeRemaining = ent.Comp.InsertTime;
        if (_net.IsClient)
            return;
        _app.SetData(ent.Owner, ChemSimulatorVisuals.Sim, ChemSimulatorVisState.Reading);
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
        Dirty(ent);
    }

    protected bool CheckReady(Entity<ChemSimulatorComponent> ent)
    {
        if (ent.Comp.Stage == ChemSimulatorStage.Failure)
        {
            ent.Comp.StatusBar = Loc.GetString("research-sim-status-critfail");
            ent.Comp.Stage = ChemSimulatorStage.Off;
            return false;
        }
        _con.TryGetContainer(ent.Owner, "target", out var targetCon);
        _con.TryGetContainer(ent.Owner, "reference", out var referenceCon);
        var reagents = _protoman.GetInstances<ReagentPrototype>();
        var reactions = _protoman.GetInstances<ReactionPrototype>();
        var properties = _protoman.GetInstances<ReagentPropertyPrototype>();
        if (targetCon is not null && targetCon.Count != 0)
        {
            if (!TryComp<ResearchReportComponent>(targetCon.ContainedEntities[0], out var targcomp))
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-bad-target-data");
                Dirty(ent);
                return false;
            }
            if (targcomp is null)
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-bad-target-data");
                Dirty(ent);
                return false;
            }
            if (targcomp.Completed == false)
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-incomplete-target");
                Dirty(ent);
                return false;
            }
            if (targcomp.Data is null)
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-target-corrupted");
                Dirty(ent);
                return false;
            }
            if (_gen.LockedDownChems.Contains(targcomp.Data.Value.ID))
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-target-destroyed");
                Dirty(ent);
                return false;
            }
            if (!reagents.TryGetValue(targcomp.Data.Value.ID, out var targreagent))
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-target-corrupted");
                Dirty(ent);
                return false;
            }
            if (targreagent.Class < ReagentClass.Basic || !targreagent.Generated)
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-target-cannot-alter");
                Dirty(ent);
                return false;
            }
            reactions.TryGetValue(targreagent.ID, out var targrecipe);
            if (targrecipe is not null)
            {
                foreach (var reactant in targrecipe.Reactants)
                {
                    if (reagents[reactant.Key].Class >= ReagentClass.Special &&
                        _gen.IdentifiedChemicals.ContainsKey(reagents[reactant.Key].ID) &&
                        reagents[reactant.Key].Class != ReagentClass.Hydro)
                    {
                        if (reactant.Value.Catalyst)
                        {
                            ent.Comp.StatusBar = Loc.GetString("research-sim-status-unknown-catalysts");
                            Dirty(ent);
                            return false;
                        }
                        else
                        {
                            ent.Comp.StatusBar = Loc.GetString("research-sim-status-unknown-components");
                            Dirty(ent);
                            return false;
                        }
                    }
                }
            }
            if (ent.Comp.TargetProperty is not null && ent.Comp.Mode != ChemSimulatorMode.Add)
            {
                if (ent.Comp.PropertyCosts[ent.Comp.TargetProperty] > _dat.Credits)
                {
                    ent.Comp.StatusBar = Loc.GetString("research-sim-status-insufficient-funds");
                    Dirty(ent);
                    return false;
                }
                if (properties[ent.Comp.TargetProperty].Category.HasFlag(ReagentPropertyTypeEnum.Unadjustable))
                {
                    ent.Comp.StatusBar = Loc.GetString("research-sim-status-unadjustable");
                    Dirty(ent);
                    return false;
                }
                if (ent.Comp.Mode == ChemSimulatorMode.Amplify)
                {
                    if (targcomp.Data.Value.Effects[ent.Comp.TargetProperty] >=
                        _dat.Clearance*2 + 2 && _dat.Clearance < 5)
                    {
                        ent.Comp.StatusBar = Loc.GetString("research-sim-status-insufficient-clearance-amplify");
                        Dirty(ent);
                        return false;
                    }

                    if (targcomp.Data.Value.Effects[ent.Comp.TargetProperty] >=
                    properties[ent.Comp.TargetProperty].MaxLevel)
                    {
                        ent.Comp.StatusBar = Loc.GetString("research-sim-status-unable-amplify-further");
                        Dirty(ent);
                        return false;
                    }
                }
            }
            else if (ent.Comp.Mode != ChemSimulatorMode.Add)
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-no-target-picked");
                Dirty(ent);
                return false;
            }
            if (targcomp.Data.Value.Effects.Count < 2)
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-target-no-complexity");
                Dirty(ent);
                return false;
            }
            if ((ent.Comp.Mode == ChemSimulatorMode.Relate || ent.Comp.Mode == ChemSimulatorMode.Add) &&
                (referenceCon is null || referenceCon.Count == 0))
            {
                ent.Comp.StatusBar = Loc.GetString("research-sim-status-no-reference");
                Dirty(ent);
                return false;
            }
            if (ent.Comp.Mode == ChemSimulatorMode.Relate || ent.Comp.Mode == ChemSimulatorMode.Add)
            {
                if (referenceCon is not null && referenceCon.Count != 0)
                {
                    if (!TryComp<ResearchReportComponent>(referenceCon.ContainedEntities[0], out var referenceComp))
                    {
                        ent.Comp.StatusBar = Loc.GetString("research-sim-status-bad-reference-data");
                        Dirty(ent);
                        return false;
                    }
                    if (!referenceComp.Completed)
                    {
                        ent.Comp.StatusBar = Loc.GetString("research-sim-status-incomplete-reference");
                        Dirty(ent);
                        return false;
                    }
                    if (referenceComp.Data is null)
                    {
                        ent.Comp.StatusBar = Loc.GetString("research-sim-status-reference-corrupted");
                        Dirty(ent);
                        return false;
                    }
                    if (_gen.LockedDownChems.Contains(referenceComp.Data.Value.Name))
                    {
                        ent.Comp.StatusBar = Loc.GetString("research-sim-status-reference-destroyed");
                        Dirty(ent);
                        return false;
                    }
                    if (ent.Comp.ReferenceProperty is not null)
                    {
                        if (targcomp.Data.Value.Effects.ContainsKey(ent.Comp.ReferenceProperty))
                        {
                            ent.Comp.StatusBar = Loc.GetString("research-sim-status-already-in-target");
                            Dirty(ent);
                            return false;
                        }
                        //it's got the || in the CM13 code
                        //we all make sacrifices for parity...
                        if (ent.Comp.TargetProperty is not null || ent.Comp.Mode != ChemSimulatorMode.Add)
                        {
                            if (ent.Comp.TargetProperty is null)
                            {
                                ent.Comp.StatusBar = Loc.GetString("research-sim-status-critfail");
                                Dirty(ent);
                                return false;
                            }
                            if (targcomp.Data.Value.Effects[ent.Comp.TargetProperty] !=
                                referenceComp.Data.Value.Effects[ent.Comp.ReferenceProperty])
                            {
                                ent.Comp.StatusBar = Loc.GetString("research-sim-status-inequal");
                                Dirty(ent);
                                return false;
                            }
                        }
                        if (properties[ent.Comp.ReferenceProperty].Category.HasFlag(ReagentPropertyTypeEnum.Unadjustable))
                        {
                            ent.Comp.StatusBar = Loc.GetString("reseach-sim-status-reference-unadjustable");
                            Dirty(ent);
                            return false;
                        }
                    }
                    else
                    {
                        ent.Comp.StatusBar = Loc.GetString("research-sim-status-no-reference-picked");
                        Dirty(ent);
                        return false;
                    }
                    if (ent.Comp.ReferenceProperty is null)
                    {
                        ent.Comp.StatusBar = Loc.GetString("research-sim-status-critfail");
                        Dirty(ent);
                        return false;
                    }
                    if (ent.Comp.Mode == ChemSimulatorMode.Add)
                    {
                        if (ent.Comp.PropertyCosts[ent.Comp.ReferenceProperty] > _dat.Credits)
                        {
                            ent.Comp.StatusBar = Loc.GetString("research-sim-status-insufficient-funds");
                            Dirty(ent);
                            return false;
                        }
                    }
                }
            }
        }
        if (targetCon is null || targetCon.Count == 0)
        {
            ent.Comp.StatusBar = Loc.GetString("research-sim-status-no-target");
            Dirty(ent);
            return false;
        }
        if (ent.Comp.Stage == ChemSimulatorStage.Off)
        {
            ent.Comp.StatusBar = Loc.GetString("research-sim-status-ready");
        }
        Dirty(ent);
        return true;
    }

    private void UpdatePropertyCosts(Entity<ChemSimulatorComponent> ent)
    {
        ent.Comp.PropertyCosts.Clear();
        bool onlyPositive = true;
        _con.TryGetContainer(ent.Owner, "target", out var targetCon);
        _con.TryGetContainer(ent.Owner, "reference", out var referenceCon);
        var props = _protoman.GetInstances<ReagentPropertyPrototype>();
        ResearchReportComponent? refcomp = null;
        if (targetCon is not null && targetCon.Count > 0 &&
            TryComp<ResearchReportComponent>(targetCon.ContainedEntities[0], out var targcomp) &&
            targcomp.Data is not null && targcomp.Completed)
        {
            if (ent.Comp.Mode == ChemSimulatorMode.Add && referenceCon is not null && referenceCon.Count > 0 &&
                TryComp<ResearchReportComponent>(referenceCon.ContainedEntities[0], out refcomp) &&
                refcomp.Data is not null && refcomp.Completed)
            {
                int totalvalue = 0;
                foreach (var refprop in refcomp.Data.Value.Effects)
                {
                    var prop = props[refprop.Key];
                    ent.Comp.PropertyCosts.Add(refprop.Key, Math.Clamp(prop.Value + refprop.Value - 1, 1, MaxPropCost));
                    if (targcomp.Data.Value.Effects.Count > 4)
                    {
                        ent.Comp.PropertyCosts[refprop.Key] += AddBulk;
                    }
                }
                foreach (var targprop in targcomp.Data.Value.Effects)
                {
                    var prop = props[targprop.Key];
                    totalvalue += prop.Value;
                    if (prop.Hint != ReagentPropertyHintEnum.Positive)
                        onlyPositive = false;
                }
                if (totalvalue > 7)
                {
                    foreach (var penalty in ent.Comp.PropertyCosts)
                    {
                        ent.Comp.PropertyCosts[penalty.Key] += AddValue;
                    }
                }
            }
            if (ent.Comp.PropertyCosts.Count == 0)
            {
                foreach (var targprop in targcomp.Data.Value.Effects)
                {
                    var prop = props[targprop.Key];
                    if (prop.Hint != ReagentPropertyHintEnum.Positive)
                        onlyPositive = false;
                    if (prop.Category.HasFlag(ReagentPropertyTypeEnum.Anomalous))
                    {
                        ent.Comp.PropertyCosts.Add(targprop.Key, targprop.Value * MultAnomalous);
                        continue;
                    }
                    switch (ent.Comp.Mode)
                    {
                        case ChemSimulatorMode.Amplify:
                            ent.Comp.PropertyCosts.Add(targprop.Key,
                                Math.Max(Math.Min(targprop.Value + prop.Value - 1, MaxPropCost), 1));
                            break;
                        case ChemSimulatorMode.Suppress:
                            ent.Comp.PropertyCosts.Add(targprop.Key, 2);
                            break;
                        case ChemSimulatorMode.Relate:
                            if (refcomp is null && referenceCon is not null && referenceCon.Count != 0)
                            {
                                TryComp<ResearchReportComponent>(referenceCon.ContainedEntities[0], out refcomp);
                            }
                            if (ent.Comp.ReferenceProperty is not null && refcomp is not null && refcomp.Data is not null)
                            {
                                var refprop = props[ent.Comp.ReferenceProperty];
                                if (refprop.Category.HasFlag(ReagentPropertyTypeEnum.Anomalous))
                                {
                                    ent.Comp.PropertyCosts[targprop.Key] =
                                        targprop.Value * 10;
                                }
                                else if (refprop.Rarity < ReagentPropertyRarityEnum.Rare)
                                {
                                    ent.Comp.PropertyCosts[targprop.Key] =
                                        targprop.Value;
                                }
                                else
                                {
                                    ent.Comp.PropertyCosts[targprop.Key] =
                                        (targprop.Value * MultRare) +
                                        props[targprop.Key].Value;
                                }
                            }
                            else
                            {
                                ent.Comp.PropertyCosts[targprop.Key] = props[targprop.Key].Value + targprop.Value;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            if (!onlyPositive)
            {
                foreach (var prop in ent.Comp.PropertyCosts)
                {
                    ent.Comp.PropertyCosts[prop.Key] = Math.Max(ent.Comp.PropertyCosts[prop.Key] - 2, 1);
                }
            }
        }

    }
    #region User Interface
    private void OnBuiOpened(Entity<ChemSimulatorComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (_net.IsClient)
            return;
        var state = GetStateForBui(ent);
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, state);
    }


    protected ChemSimulatorBuiState GetStateForBui(Entity<ChemSimulatorComponent> ent)
    {
        //pray to god there is not any desync
        var targcon = _con.GetContainer(ent.Owner, "target");
        var refcon = _con.GetContainer(ent.Owner, "reference");
        EntityUid? targent = null;
        EntityUid? refent = null;
        if (targcon.Count > 0)
        {
            targent = targcon.ContainedEntities[0];
        }
        if (refcon.Count > 0)
        {
            refent = refcon.ContainedEntities[0];
        }
        TryComp<ResearchReportComponent>(targent, out var targcomp);
        TryComp<ResearchReportComponent>(refent, out var refcomp);
        GeneratedReagentData? targdat = targcomp?.Data;
        GeneratedReagentData? refdat = refcomp?.Data;
        CalculateNewODLevel(ent);
        UpdatePropertyCosts(ent);
        ent.Comp.Ready = CheckReady(ent);
        return new ChemSimulatorBuiState(
            target: targdat,
            reference: refdat,
            mode: ent.Comp.Mode,
            stage: ent.Comp.Stage,
            costs: ent.Comp.PropertyCosts,
            ready: ent.Comp.Ready,
            ovrride: ent.Comp.Override,
            statusBar: ent.Comp.StatusBar,
            targprop: ent.Comp.TargetProperty,
            refprop: ent.Comp.ReferenceProperty,
            credits: _dat.Credits,
            overdose: ent.Comp.Overdose,
            recipePicked: ent.Comp.PickedRecipeChem,
            recipeOptions: ent.Comp.RecipeChemOptions,
            cost: ent.Comp.SimulationCost);
    }

    protected void CalculateNewODLevel(Entity<ChemSimulatorComponent> ent)
    {
        
        if (_con.TryGetContainer(ent, "target", out var targcon) && targcon.Count > 0 &&
            TryComp<ResearchReportComponent>(targcon.ContainedEntities[0], out var targcomp) && targcomp.Data is not null)
        {
            ent.Comp.Overdose ??= 1;
            ent.Comp.Overdose = Math.Max((int)targcomp.Data.Value.Overdose, 1);
            if (ent.Comp.Mode == ChemSimulatorMode.Add)
                return;
            if (ent.Comp.Overdose <= 5)
            {
                ent.Comp.Overdose = Math.Max(ent.Comp.Overdose.Value - 1, 1);
            }
            else
            {
                ent.Comp.Overdose = Math.Max(ent.Comp.Overdose.Value - 5, 5);
            }
        }
    }
    private void OnTargetPick(Entity<ChemSimulatorComponent> ent, ref ChemSimulatorPickTargetPropertyBuiMsg args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.TargetProperty = args.Property;
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
        Dirty(ent);
    }
    private void OnRefPick(Entity<ChemSimulatorComponent> ent, ref ChemSimulatorPickReferencePropertyBuiMsg args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.ReferenceProperty = args.Property;
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
        Dirty(ent);
    }
    private void OnFinalize(Entity<ChemSimulatorComponent> ent, ref ChemSimulatorFinalizeBuiMsg args)
    {
        if (_net.IsClient)
            return;
        if (ent.Comp.PickedRecipeChem is null)
            return;
        bool hit = false;
        foreach(var tuple in ent.Comp.RecipeChemOptions)
        {
            if(tuple.Item1 == ent.Comp.PickedRecipeChem)
            {
                hit = true;
                break;
            }
        }
        if (hit)
        {
            var ev = new FinalizeChemSimulatorEvent();
            RaiseLocalEvent(ent.Owner, ev);
        }
        if (_net.IsClient)
            return;
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
    }
    private void OnChangeMode(Entity<ChemSimulatorComponent> ent, ref ChemSimulatorPickModeBuiMsg args)
    {
        if (_net.IsClient)
            return;
        ent.Comp.Mode = args.Mode;
        ent.Comp.TargetProperty = null;
        ent.Comp.ReferenceProperty = null;
        UpdatePropertyCosts(ent);
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
        Dirty(ent);
    }
    private void OnPickRecipe(Entity<ChemSimulatorComponent> ent, ref ChemSimulatorPickRecipeChemBuiMsg args)
    {
        if (_net.IsClient)
            return;
        ent.Comp.PickedRecipeChem = args.Pick;
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
        Dirty(ent);
    }
    private void OnAttemptSimulate(Entity<ChemSimulatorComponent> ent, ref ChemSimulatorAttemptSimulateBuiMsg args)
    {
        if (_net.IsClient)
            return;
        if (!ent.Comp.Ready)
            return;
        var ev = new BeginChemSimulatorProcessEvent();
        RaiseLocalEvent(ent.Owner, ev);
    }
    private void OnToggleOverride(Entity<ChemSimulatorComponent> ent, ref ChemSimulatorToggleOverrideBuiMsg args)
    {
        if (_net.IsClient)
            return;
        ent.Comp.Override = !ent.Comp.Override;
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
        Dirty(ent);
    }
    private void OnEject(Entity<ChemSimulatorComponent> ent, ref ChemSimulatorEjectBuiMsg args)
    {
        ent.Comp.PrintTimeRemaining = ent.Comp.PrintTime;
        if (_net.IsClient)
            return;
        _con.TryGetContainer(ent.Owner, "target", out var targcon);
        _con.TryGetContainer(ent.Owner, "reference", out var refcon);
        if (args.Player is null)
        {
            if (args.Reference)
            {
                if (refcon is null || refcon.Count == 0)
                    return;
                _con.Remove(refcon.ContainedEntities[0], refcon);
                ent.Comp.ReferenceProperty = null;
            }
            else
            {
                if (targcon is null || targcon.Count == 0)
                    return;
                _con.Remove(targcon.ContainedEntities[0], targcon);
                ent.Comp.TargetProperty = null;
            }
        }
        else
        {
            if (args.Reference)
            {
                if (refcon is null || refcon.Count == 0)
                    return;
                _hands.PickupOrDrop(GetEntity(args.Player), refcon.ContainedEntities[0]);
                ent.Comp.ReferenceProperty = null;
            }
            else
            {
                if (targcon is null || targcon.Count == 0)
                    return;
                _hands.PickupOrDrop(GetEntity(args.Player), targcon.ContainedEntities[0]);
                ent.Comp.TargetProperty = null;
            }
        }
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
        Dirty(ent);
    }
    #endregion
}
