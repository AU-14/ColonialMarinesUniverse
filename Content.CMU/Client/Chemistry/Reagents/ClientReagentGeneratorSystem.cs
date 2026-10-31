using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Client.CMU14.Chemistry.Reagents;

public sealed partial class ClientReagentGeneratorSystem : SharedReagentGeneratorSystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private ILogManager _logMan = default!;
    /*
    [ViewVariables(VVAccess.ReadOnly)]
    private HashSet<string> _generatedReagents = [];
    [ViewVariables(VVAccess.ReadOnly)]
    private HashSet<string> _generatedRecipes = [];
    /**/
    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeNetworkEvent<SendReagentDataEvent>(OnRecieveData);
        SubscribeNetworkEvent<SyncKnownPropertiesEvent>(OnPropertySync);
        SubscribeNetworkEvent<IdentifyChemicalEvent>(OnChemIdentified);
        _sawmill = _logMan.GetSawmill("reagent");
    }
    private void OnRecieveData(SendReagentDataEvent args)
    {
        _sawmill.Info("Recieved reagent data from server.");
        // ARCH-02: a nested contract's recipe names another generated chemical, and CreateRecipe indexes every
        // reactant. Load in dependency order: each pass loads the entries whose reactants are all loaded,
        // until a pass makes no progress.
        var pending = new List<GeneratedReagentData>();
        foreach (var reagent in args.Reagents)
        {
            if (!_generatedReagents.Contains(reagent.Key))
                pending.Add(reagent.Value);
        }
        var progress = true;
        while (pending.Count > 0 && progress)
        {
            progress = false;
            var deferred = new List<GeneratedReagentData>();
            foreach (var data in pending)
            {
                if (!ReactantsLoaded(data))
                {
                    deferred.Add(data);
                    continue;
                }
                CreateReagent(data);
                _generatedReagents.Add(data.ID);
                _generatedRecipes.Add(data.ID);
                progress = true;
            }
            pending = deferred;
        }
        foreach (var data in pending)
        {
            _sawmill.Error($"Could not load generated reagent {data.ID}: its recipe needs a reagent that does not exist.");
        }
        //another foreach because the client lowkey deserves it
        // Copy: entries are removed while walking the set.
        foreach (var generatedReagent in new List<string>(_generatedReagents))
        {
            if (!args.Reagents.ContainsKey(generatedReagent))
            {
                // The ignition and uranium-alternative reactions belong to the reagent and go with it.
                foreach (var recipe in new[] { generatedReagent, IgnitionRecipeId(generatedReagent), $"CMUGeneratedUraniumAlternative{generatedReagent}" })
                {
                    if (!_generatedRecipes.Contains(recipe) || !RecipeYamls.TryGetValue(recipe, out var recipeYaml))
                        continue;
                    //UNCOMMENT WHEN https://github.com/space-wizards/RobustToolbox/pull/6609 IS MERGED
                    //_protoMan.TryDelete<ReactionPrototype>(recipe);
                    _protoMan.RemoveString(recipeYaml);
                    _generatedRecipes.Remove(recipe);
                }
                //UNCOMMENT WHEN https://github.com/space-wizards/RobustToolbox/pull/6609 IS MERGED
                //_protoMan.TryDelete<ReagentPrototype>(generatedReagent);
                _protoMan.RemoveString(ChemYamls[generatedReagent]);
                _generatedReagents.Remove(generatedReagent);
            }
        }
        IdentifiedChemicals = args.IdentifiedChemicals;
        KnownProperties = args.KnownProperties;
        _lockedDownChems = args.LockedDownChems;
        ChemicalGenClassesList = args.ChemicalList;
        _unfoldedConflicts = args.Conflicts;
        _unfoldedCombinations = args.Combinations;
    }
    private bool ReactantsLoaded(GeneratedReagentData data)
    {
        // Re-fetched per check: the instance dictionary is a snapshot replaced by every load.
        var reagents = _protoMan.GetInstances<ReagentPrototype>();
        foreach (var reactant in data.Recipe.Keys)
        {
            if (!reagents.ContainsKey(reactant))
                return false;
        }
        return true;
    }

    private void OnPropertySync(SyncKnownPropertiesEvent args)
    {
        KnownProperties.Clear();
        KnownProperties = args.KnownProperties;
    }
    private void OnChemIdentified(IdentifyChemicalEvent args)
    {
        IdentifiedChemicals.Add(args.Chem, args.Reward);
    }
    private void OnCleanup(RoundRestartCleanupEvent args)
    {
        if (_generatedRecipes.Count > 0)
        {
            _sawmill.Info("Clearing procedural reagent recipes.");
            foreach (var recipe in _generatedRecipes)
            {
                //UNCOMMENT WHEN https://github.com/space-wizards/RobustToolbox/pull/6609 IS MERGED
                //_protoMan.TryDelete<ReactionPrototype>(recipe);
                _protoMan.RemoveString(RecipeYamls[recipe]);
                _generatedRecipes.Remove(recipe);
            }
            DebugTools.Assert(_generatedRecipes.Count == 0);
        }
        if (_generatedReagents.Count > 0)
        {
            _sawmill.Info("Clearing procedural reagents.");
            foreach (var reagent in _generatedReagents)
            {
                //UNCOMMENT WHEN https://github.com/space-wizards/RobustToolbox/pull/6609 IS MERGED
                //_protoMan.TryDelete<ReagentPrototype>(reagent);
                _protoMan.RemoveString(ChemYamls[reagent]);
                _generatedReagents.Remove(reagent);
            }
            DebugTools.Assert(_generatedReagents.Count == 0);
        }
        // STATE-01: the YAML sources are only needed to unload this round's prototypes.
        ChemYamls.Clear();
        RecipeYamls.Clear();
        IdentifiedChemicals.Clear();
        KnownProperties.Clear();
        _lockedDownChems.Clear();
        ChemicalGenClassesList.Clear();
        _unfoldedConflicts = UnfoldConflicts();
    }
}
