using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Client._AU14.Chemistry.Reagents;

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
        foreach (var reagent in args.Reagents)
        {
            if (!_generatedReagents.Contains(reagent.Key))
            {
                CreateReagent(reagent.Value);
                _generatedReagents.Add(reagent.Key);
                _generatedRecipes.Add(reagent.Key);
            }
        }
        //another foreach because the client lowkey deserves it
        foreach (var generatedReagent in _generatedReagents)
        {
            if (!args.Reagents.ContainsKey(generatedReagent))
            {
                if (_generatedRecipes.Contains(generatedReagent))
                {
                    //UNCOMMENT WHEN https://github.com/space-wizards/RobustToolbox/pull/6609 IS MERGED
                    //_protoMan.TryDelete<ReactionPrototype>(generatedReagent);
                    _protoMan.RemoveString(RecipeYamls[generatedReagent]);
                    _generatedRecipes.Remove(generatedReagent);
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
        IdentifiedChemicals.Clear();
        KnownProperties.Clear();
        _lockedDownChems.Clear();
        ChemicalGenClassesList.Clear();
        _unfoldedConflicts = UnfoldConflicts();
    }
}
