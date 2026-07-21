using Content.Server._AU14.Chemistry.Reagents;
using Content.Server.GameTicking;
using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared.Dataset;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Content.Shared._CMU14.Chemistry.Reagent;
using System.Security.Cryptography;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using System.Linq;
using Content.Shared.FixedPoint;
using Content.Shared.Paper;
using Content.Shared.GameTicking;

namespace Content.Server._AU14.Chemistry.Research;

public sealed partial class ServerChemSimulatorSystem : SharedChemicalSimulatorSystem
{
    [Dependency] private SharedContainerSystem _con = default!;
    [Dependency] private SharedAppearanceSystem _app = default!;
    [Dependency] private SharedPopupSystem _pop = default!;
    [Dependency] private ServerReagentGeneratorSystem _gen = default!;
    [Dependency] private ServerResearchDataTerminalSystem _dat = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _protoman = default!;
    [Dependency] private MetaDataSystem _mets = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IGameTiming _time = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PaperSystem _paper = default!;
    

    private HashSet<Entity<ChemSimulatorComponent>> _processing = [];
    private HashSet<Entity<ChemSimulatorComponent>> _finalizing = [];
    private HashSet<string> _simulations = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChemSimulatorComponent, BeginChemSimulatorProcessEvent>(OnBeginProcess);
        SubscribeLocalEvent<ChemSimulatorComponent, FinalizeChemSimulatorEvent>(OnFinalize);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _finalizing.Clear();
        _simulations.Clear();
        _processing.Clear();
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_finalizing.Count > 0)
        {
            foreach (var ent in _finalizing)
            {
                Finalize(ent);
                _finalizing.Remove(ent);
            }
        }
        var query = EntityQueryEnumerator<ChemSimulatorComponent>();
        while (query.MoveNext(out var uid, out var acomp))
        {
            ProcessPrint(uid, acomp, frameTime);
            ProcessRead(uid, acomp, frameTime);
        }



        if (_processing.Count == 0)
            return;
        //awful horrible no good very bad wretched evil vile wicked
        foreach (var ent in _processing)
        {
            if (_time.CurTime < ent.Comp.NextProcess)
                return;
            if (ent.Comp.Stage > ChemSimulatorStage.Off)
            {
                ent.Comp.Stage = (ChemSimulatorStage)Math.Max((int)ent.Comp.Stage - 1, (int)ChemSimulatorStage.Final);
                switch (ent.Comp.Stage)
                {
                    case ChemSimulatorStage.Stage4:
                        var literal = "CMUSimStatesStageFour";
                        LocalizedDatasetPrototype dat = _protoman.Index<LocalizedDatasetPrototype>(literal);
                        ent.Comp.StatusBar = _random.Pick(dat);
                        break;
                    case ChemSimulatorStage.Stage3:
                        var literal3 = "CMUSimStatesStageThree";
                        LocalizedDatasetPrototype dat3 = _protoman.Index<LocalizedDatasetPrototype>(literal3);
                        ent.Comp.StatusBar = _random.Pick(dat3);
                        break;
                    case ChemSimulatorStage.Wait:
                        var modifying = new GeneratedReagentData();
                        switch (ent.Comp.Mode)
                        {
                            case ChemSimulatorMode.Amplify:
                                Amplify(ent, ref modifying);
                                break;
                            case ChemSimulatorMode.Suppress:
                                Suppress(ent, ref modifying);
                                break;
                            case ChemSimulatorMode.Relate:
                                Relate(ent, ref modifying);
                                break;
                            case ChemSimulatorMode.Add:
                                Add(ent, ref modifying);
                                break;
                            default:
                                break;
                        }

                        if (modifying.OriginalID == string.Empty)
                        {
                            _con.TryGetContainer(ent, "target", out var targcon);
                            if (targcon is null || targcon.Count == 0 ||
                                !TryComp<ResearchReportComponent>(targcon.ContainedEntities[0], out var targcomp) ||
                                targcomp is null || targcomp.Data is null)
                            {
                                //how did we get here??
                                throw new UnreachableException($"Chemical simulator {ToPrettyString(ent)} has an invalid target ID!");
                            }

                            modifying.OriginalID = targcomp.Data.Value.ID;
                        }
                        EncodeReagent(ref modifying);
                        if (_simulations.Contains(modifying.ID))
                        {
                            Print(ent, modifying.ID);
                            ent.Comp.StatusBar = Loc.GetString("research-sim-status-done");
                            ent.Comp.Stage = ChemSimulatorStage.Off;
                            Dirty(ent);
                        }
                        else if (PrepareRecipeOptions(ent, ref modifying))
                        {
                            ent.Comp.ChemCache = modifying;
                            ent.Comp.StatusBar = Loc.GetString("research-sim-status-pick-ready");
                            //play sound here
                            UpdateAppearance(ent.Owner, ent.Comp);
                            Dirty(ent);
                        }
                        else
                        {
                            FinalizeSimulation(ent, ref modifying);
                        }
                        break;
                    default:
                        break;
                }
            }
            else
            {
                ent.Comp.Ready = CheckReady(ent);
                _processing.Remove(ent);
                Dirty(ent);
            }
            ent.Comp.NextProcess = _time.CurTime + TimeSpan.FromSeconds(ent.Comp.SecondsPerProcess);
            _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
            Dirty(ent);
        }
    }
    private void Amplify(Entity<ChemSimulatorComponent> ent, ref GeneratedReagentData chem)
    {
        if (!_con.TryGetContainer(ent, "target", out var targcon) ||
            targcon is null || targcon.Count == 0 ||
            !TryComp<ResearchReportComponent>(targcon.ContainedEntities[0], out var targcomp) ||
            ent.Comp.TargetProperty is null || targcomp.Data is null)
            return;
        _gen.MakeAlike(ref chem, targcomp.Data.Value.ID);
        _gen.RelevelProperty(ref chem, ent.Comp.TargetProperty, chem.Effects[ent.Comp.TargetProperty] + 1);
    }

    private void Suppress(Entity<ChemSimulatorComponent> ent, ref GeneratedReagentData chem)
    {
        if (!_con.TryGetContainer(ent, "target", out var targcon) ||
            targcon is null || targcon.Count == 0 ||
            !TryComp<ResearchReportComponent>(targcon.ContainedEntities[0], out var targcomp) ||
            ent.Comp.TargetProperty is null || targcomp.Data is null)
            return;
        _gen.MakeAlike(ref chem, targcomp.Data.Value.ID);
        _gen.RelevelProperty(ref chem, ent.Comp.TargetProperty, chem.Effects[ent.Comp.TargetProperty] - 1);
    }
    private void Relate(Entity<ChemSimulatorComponent> ent, ref GeneratedReagentData chem)
    {
        //horrible
        if (!_con.TryGetContainer(ent, "target", out var targcon) ||
            targcon is null || targcon.Count == 0 ||
            !TryComp<ResearchReportComponent>(targcon.ContainedEntities[0], out var targcomp) ||
            ent.Comp.TargetProperty is null || targcomp.Data is null || !_con.TryGetContainer(ent, "reference", out var refcon) ||
            refcon is null || refcon.Count == 0 ||
            !TryComp<ResearchReportComponent>(refcon.ContainedEntities[0], out var refcomp) ||
            ent.Comp.ReferenceProperty is null || refcomp.Data is null)
            return;
        _gen.MakeAlike(ref chem, targcomp.Data.Value.ID);
        _gen.RemoveProperty(ref chem, ent.Comp.TargetProperty);
        _gen.InsertProperty(ref chem, ent.Comp.ReferenceProperty, refcomp.Data.Value.Effects[ent.Comp.ReferenceProperty]);
    }
    private void Add(Entity<ChemSimulatorComponent> ent, ref GeneratedReagentData chem)
    {
        //this one was haunted
        _con.EnsureContainer<ContainerSlot>(ent.Owner, "target");
        if (!_con.TryGetContainer(ent, "target", out var targcon) ||
            targcon is null || targcon.Count == 0 ||
            !TryComp<ResearchReportComponent>(targcon.ContainedEntities[0], out var targcomp) ||
            targcomp.Data is null || !_con.TryGetContainer(ent, "reference", out var refcon) ||
            refcon is null || refcon.Count == 0 ||
            !TryComp<ResearchReportComponent>(refcon.ContainedEntities[0], out var refcomp) ||
            ent.Comp.ReferenceProperty is null || refcomp.Data is null)
            return;
        _gen.MakeAlike(ref chem, targcomp.Data.Value.ID);
        _gen.InsertProperty(ref chem, ent.Comp.ReferenceProperty, refcomp.Data.Value.Effects[ent.Comp.ReferenceProperty]);
        _gen.RetroactiveLockdown(refcomp.Data.Value);
    }
    private void OnFinalize(Entity<ChemSimulatorComponent> ent, ref FinalizeChemSimulatorEvent args)
    {
        /*if (ent.Comp.ChemCache is null)
        {
            ent.Comp.Stage = ChemSimulatorStage.Failure;
            Dirty(ent);
            return;
        }
        GeneratedReagentData chem = ent.Comp.ChemCache.Value;
        FinalizeSimulation(ent, ref chem);*/
        _finalizing.Add(ent);
    }
    private void Finalize(Entity<ChemSimulatorComponent> ent)
    {
        if (ent.Comp.ChemCache is null)
        {
            ent.Comp.Stage = ChemSimulatorStage.Failure;
            Dirty(ent);
            return;
        }
        GeneratedReagentData chem = ent.Comp.ChemCache.Value;
        FinalizeSimulation(ent, ref chem);
    }

    private void FinalizeSimulation(Entity<ChemSimulatorComponent> ent, ref GeneratedReagentData chem)
    {
        ent.Comp.Stage = ChemSimulatorStage.Off;
        EndSimulation(ent, ref chem);
        ent.Comp.ChemCache = null;
        Dirty(ent);
    }

    private void EndSimulation(Entity<ChemSimulatorComponent> ent, ref GeneratedReagentData chem)
    {
        chem.GenTier = Math.Max(Math.Max(Math.Min((int)chem.Class, (int)ReagentClass.Common), chem.GenTier), 1);
        if (chem.Class == ReagentClass.Special)
        {
            chem.GenTier = 4;
        }

        GeneratedReagentData recipe = new GeneratedReagentData();
        GeneratedReagentData? assocRecipe = new GeneratedReagentData();
        if (!_con.TryGetContainer(ent, "target", out var targcon) || targcon is null || targcon.Count == 0 ||
            !TryComp<ResearchReportComponent>(targcon.ContainedEntities[0], out var targcomp) || targcomp is null ||
            targcomp.Data is null)
        {
            //failure
            ent.Comp.Stage = ChemSimulatorStage.Failure;
            UpdateAppearance(ent.Owner, ent.Comp);
            Dirty(ent);
            return;
        }
        if (ent.Comp.PickedRecipeChem is null || ent.Comp.Overdose is null ||
            (ent.Comp.TargetProperty is null && ent.Comp.Mode != ChemSimulatorMode.Add))
        {
            //impressive failure
            ent.Comp.Stage = ChemSimulatorStage.Failure;
            UpdateAppearance(ent.Owner, ent.Comp);
            Dirty(ent);
            return;
        }

        recipe.GenTier = chem.GenTier;

        if (!_gen.GetReagentData(targcomp.Data.Value.ID, out assocRecipe))
        {
            GeneratedReagentData rdat = new GeneratedReagentData();
            rdat.Recipe = [];
            rdat.GenTier = 0;
            if (!_gen.GenerateRecipe(ref rdat, new HashSet<string>()))
            {
                //failure
                ent.Comp.Stage = ChemSimulatorStage.Failure;
                UpdateAppearance(ent.Owner, ent.Comp);
                Dirty(ent);
                return;
            }
            assocRecipe = rdat;
        }
        recipe.Recipe = assocRecipe.Value.Recipe;
        (string, int, bool, bool)? index = null;
        foreach (var tuple in ent.Comp.RecipeChemOptions)
        {
            if (tuple.Item1 == ent.Comp.PickedRecipeChem)
            {
                index = tuple;
                break;
            }
        }
        if (index is null)
        {
            index ??= (string.Empty, 0, false, false);
        }
        if (recipe.Recipe.Count > 2 && !index.Value.Item4)
        {
            recipe.Recipe.Remove(_random.Pick(recipe.Recipe.Keys));
        }
        var weights = new Dictionary<int, float>() { { 1, 30f }, { 2, 15f }, { 3, 15f }, { 4, 5f } };
        _gen.AddChemical(ref recipe, ent.Comp.PickedRecipeChem, _random.Pick(weights), null);

        chem.Overdose = ent.Comp.Overdose.Value;
        if (chem.Overdose < 1)
            chem.Overdose = 1;
        chem.CriticalOverdose = Math.Max(Math.Min(ent.Comp.Overdose.Value * 2, ent.Comp.Overdose.Value + 30), 10);
        int credloss = 0;
        if (ent.Comp.Mode != ChemSimulatorMode.Add)
        {
            if (ent.Comp.TargetProperty is null)
            {
                //impressive failure
                ent.Comp.Stage = ChemSimulatorStage.Failure;
                UpdateAppearance(ent.Owner, ent.Comp);
                Dirty(ent);
                return;
            }
            credloss = ent.Comp.PropertyCosts[ent.Comp.TargetProperty];
        }
        else
        {
            if (ent.Comp.ReferenceProperty is null)
            {
                //failure
                UpdateAppearance(ent.Owner, ent.Comp);
                ent.Comp.Stage = ChemSimulatorStage.Failure;
                Dirty(ent);
                return;
            }
            credloss = ent.Comp.PropertyCosts[ent.Comp.ReferenceProperty];
        }

        var reagents = _protoman.GetInstances<ReagentPrototype>();
        var reagent = reagents[ent.Comp.PickedRecipeChem];
        if (reagent is not null && reagent.Class >= ReagentClass.Rare)
            credloss--;
        _dat.UpdateClearance(_dat.Credits - credloss, -1);
        chem.Recipe = recipe.Recipe;
        _simulations.Add(chem.ID);

        chem.Class = ReagentClass.Rare;
        if (chem.OriginalID != string.Empty)
        {
            if (_gen.ProceduralReagentData.ContainsKey(chem.OriginalID))
            {
                _gen.ProceduralReagentData[chem.OriginalID].ModifiedChems.Add(chem.ID);
            }
            else if (_gen.ReagentData.ContainsKey(chem.OriginalID))
            {
                _gen.ReagentData[chem.OriginalID].ModifiedChems.Add(chem.ID);
            }
            else
            {
                //????? how did we get here???
                throw new UnreachableException($"Chemical \'{chem.ID}\' has invalid OriginalID \'{chem.OriginalID}\'!");
            }
        }

        _gen.ChemicalGenClassesList["TAU"].Add(chem.ID);
        var ev = new GenerateReagentEvent(chem);
        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
        _gen.ProceduralReagentData.Add(chem.ID, chem);
        ent.Comp.StatusBar = Loc.GetString("research-sim-status-done");
        Print(ent, chem.ID);
    }

    private void Print(Entity<ChemSimulatorComponent> ent, string ID)
    {
        //todo: sound
        var dat = _gen.CreateReport(ID);
        TrySpawnNextTo("CMUResearchReportSynthesis", ent.Owner, out var paper);
        if (paper is null || dat is null)
            return;
        ent.Comp.PrintTimeRemaining = ent.Comp.PrintTime;
        UpdateAppearance(ent.Owner, ent.Comp);
        var realpaper = paper.Value;
        if (TryComp<ResearchReportComponent>(realpaper, out var repcomp))
        {
            repcomp.Completed = dat.Value.Completed;
            repcomp.Valid = dat.Value.Valid;
            if (_gen.GetReagentData(ID, out var rdat))
            {
                repcomp.Data = rdat;
            }
        }
        string name = Loc.GetString("research-report-simulation-name", ("ID", ID));
        string contents = Loc.GetString("cmu-paper-header-wy-sim", ("ID", ID)) + '\n';
        contents += dat.Value.Info + '\n';
        contents += Loc.GetString("cmu-paper-sim-footer");
        _mets.SetEntityName(realpaper, name);
        _paper.SetContent(realpaper, contents);
        if (repcomp is not null && repcomp.Data is not null)
        {
            _dat.ResearchData.TryAdd(_dat.ResearchData.Count - 1,
                (ID, contents, _time.CurTime, true, repcomp.Data.Value, repcomp.Valid, repcomp.Completed));
        }
        DirtyEntity(realpaper);
        Dirty(ent);
    }
    private void OnBeginProcess(Entity<ChemSimulatorComponent> ent, ref BeginChemSimulatorProcessEvent args)
    {
        _processing.Add(ent);
        ent.Comp.NextProcess = _time.CurTime + TimeSpan.FromSeconds(ent.Comp.SecondsPerProcess);
        ent.Comp.StatusBar = Loc.GetString("research-sim-status-commencing");
        ent.Comp.Stage = ChemSimulatorStage.Begin;
        UpdateAppearance(ent.Owner, ent.Comp);
        ent.Comp.RecipeChemOptions.Clear();
        ent.Comp.PickedRecipeChem = null;
        _ui.SetUiState(ent.Owner, ChemSimulatorUI.Key, GetStateForBui(ent));
        Dirty(ent);
    }
    private bool PrepareRecipeOptions(Entity<ChemSimulatorComponent> ent, ref GeneratedReagentData chem)
    {
        if (!_con.TryGetContainer(ent, "target", out var targcon) || targcon is null || targcon.Count == 0 ||
            !TryComp<ResearchReportComponent>(targcon.ContainedEntities[0], out var targcomp) || targcomp is null ||
            targcomp.Data is null)
            return false;
        var reagents = _protoman.GetInstances<ReagentPrototype>();
        var original = new Dictionary<string, (int, bool)>();
        foreach (var ing in targcomp.Data.Value.Recipe)
        {
            original.Add(ing.Key, ing.Value);
        }
        ent.Comp.RecipeChemOptions.Clear();
        //chem.Recipe = original;
        chem.Recipe.Clear();
        foreach (var ing in original)
        {
            chem.Recipe.Add(ing.Key, ing.Value);
        }
        //i REALLY don't like this
        while(ent.Comp.RecipeChemOptions.Count < 3)
        {
            List<(string, int, bool, bool)> elevated = [];
            for (int i = 0; i < 9; i++)
            {

                if (chem.Recipe.Count > 2)
                {
                    chem.Recipe.Remove(_random.Pick(chem.Recipe.Keys));
                }
                string newchemid = _gen.AddChemical(ref chem, string.Empty, 0, Math.Max(targcomp.Data.Value.GenTier - 1, 1));
                var reagentdef = reagents[newchemid];

                if (_gen.IsDuplicate(ref chem, out _) || _gen.IsAllMedicine(ref chem) || reagentdef.Class >= ReagentClass.Special)
                {
                    chem.Recipe.Clear();
                    foreach (var ing in original)
                    {
                        chem.Recipe.Add(ing.Key, ing.Value);
                    }
                    if (i >= 8)
                    {
                        elevated.Add((newchemid, 1, false, true));
                        break;
                    }
                    continue;
                }
                elevated.Add((newchemid, 1, false, false));
                break;
            }
            foreach(var elev in elevated)
            {
                ent.Comp.RecipeChemOptions.Add(elev);
            }
            //var ugh = ent.Comp.RecipeChemOptions.Concat(elevated);
            //ent.Comp.RecipeChemOptions = (List<(string,int,bool,bool)>)ugh;
            chem.Recipe.Clear();
            foreach (var ing in original)
            {
                chem.Recipe.Add(ing.Key, ing.Value);
            }
        }
        return true;
    }
    private void EncodeReagent(ref GeneratedReagentData chem)
    {
        if (_gen.GetReagentData(chem.OriginalID, out var original))
        {
            var props = _protoman.GetInstances<ReagentPropertyPrototype>();
            string hashsuffix = string.Empty;
            string suffix = string.Empty;
            string hash = string.Empty;
            foreach (var effect in chem.Effects)
            {
                hashsuffix += " " + props[effect.Key].Code + effect.Value;
                suffix += "-" + props[effect.Key].Code + effect.Value;
            }
            var md5 = MD5.Create();
            byte[] input = Encoding.UTF8.GetBytes(hashsuffix);
            byte[] output = md5.ComputeHash(input);
            md5.Dispose();
            hash = Convert.ToHexStringLower(output);
            chem.ID = "TAU-" + _gen.ChemicalGenClassesList["TAU"].Count +
                "-" + original.Value.Name + "-" + hash.Substring(0, 2) + suffix; //pray to god they didn't put spaces
            chem.Name = original.Value.Name + " " + hash.Substring(0, 2);
        }
    }

    private void ProcessPrint(EntityUid ent, ChemSimulatorComponent comp, float delta)
    {
        if (comp.PrintTimeRemaining > 0)
        {
            comp.PrintTimeRemaining -= delta;
            UpdateAppearance(ent, comp);
        }
    }
    private void ProcessRead(EntityUid ent, ChemSimulatorComponent comp, float delta)
    {
        if (comp.InsertTimeRemaining <= 0)
            return;
        comp.InsertTimeRemaining -= delta;
        UpdateAppearance(ent, comp);
    }

    private void UpdateAppearance(EntityUid ent, ChemSimulatorComponent? comp = null)
    {
        if (!Resolve(ent, ref comp))
            return;

        ChemSimulatorVisState state = ChemSimulatorVisState.Normal;
        switch (comp.Stage)
        {
            case ChemSimulatorStage.Failure:
                state = ChemSimulatorVisState.Normal;
                break;
            case ChemSimulatorStage.Off:
                state = ChemSimulatorVisState.Normal;
                break;
            case ChemSimulatorStage.Final:
                state = ChemSimulatorVisState.Normal;
                break;
            case ChemSimulatorStage.Wait:
                state = ChemSimulatorVisState.Ready;
                break;
            case ChemSimulatorStage.Stage3:
                state = ChemSimulatorVisState.Running;
                break;
            case ChemSimulatorStage.Stage4:
                state = ChemSimulatorVisState.Running;
                break;
            case ChemSimulatorStage.Begin:
                state = ChemSimulatorVisState.Running;
                break;
            default:
                state = ChemSimulatorVisState.Normal;
                break;
        }
        if (comp.InsertTimeRemaining > 0)
        {
            _app.SetData(ent, ChemSimulatorVisuals.Sim, ChemSimulatorVisState.Reading);
            Dirty(ent, comp);
        }
        else if (comp.PrintTimeRemaining > 0)
        {
            _app.SetData(ent, ChemSimulatorVisuals.Sim, ChemSimulatorVisState.Printing);
            Dirty(ent, comp);
        }
        else
        {
            _app.SetData(ent, ChemSimulatorVisuals.Sim, state);
        }
    }
}
