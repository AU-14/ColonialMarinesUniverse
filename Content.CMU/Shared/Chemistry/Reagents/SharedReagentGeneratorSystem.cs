//Pretty please license this under the MIT license :) - MACMAN2003
using System;
using System.Collections.Generic;
using System.Text;
using Content.Shared._CMU14.Chemistry.Effects.Negative;
using Content.Shared._CMU14.Chemistry.Effects.Neutral;
using Content.Shared._CMU14.Chemistry.Effects.Positive;
using Content.Shared._CMU14.Chemistry.Effects.Special;
using Content.Shared._CMU14.Chemistry.Effects.Reaction;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.GameTicking;
using Robust.Shared.Utility;
using System.Diagnostics;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Content.Shared._CMU14.Chemistry.Reagent;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Network;

namespace Content.Shared._AU14.Chemistry.Reagents;

public abstract partial class SharedReagentGeneratorSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private ILogManager _logMan = default!;
    [Dependency] private INetManager _netMan = default!;
    private ISawmill _sawmill = default!;
    [ViewVariables(VVAccess.ReadOnly)]
    protected HashSet<string> _generatedReagents = [];
    [ViewVariables(VVAccess.ReadOnly)]
    protected HashSet<string> _generatedRecipes = [];
    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<GenerateReagentEvent>(CreateReagent);
        //SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        _sawmill = _logMan.GetSawmill("reagent");
        
    }
    private void CreateReagent(GenerateReagentEvent args)
    {
        CreateReagent(args.Reagent);
    }
    protected void CreateReagent(GeneratedReagentData args)
    {
        var reagents = _protoMan.GetInstances<ReagentPrototype>();
        var properties = _protoMan.GetInstances<ReagentPropertyPrototype>();
        MappingDataNode reagent = [];
        reagent.Add("type", "reagent");
        reagent.Add("id", args.ID);
        reagent.Add("name", args.Name);
        reagent.Add("desc", "An unidentified chemical");
        reagent.Add("color", args.Color.ToHexNoAlpha());
        reagent.Add("unknown", "true");
        reagent.Add("group", "Generated");
        reagent.Add("class", "Ultra");
        reagent.Add("flags", "Scannable");
        reagent.Add("reward", args.ScanPointYield.ToString());
        reagent.Add("overdose", args.Overdose.ToString());
        reagent.Add("criticalOverdose", args.CriticalOverdose.ToString());
        reagent.Add("isCM", "true");
        reagent.Add("physicalDesc", "reagent-physical-desc-unidentifiable");
        reagent.Add("flavor", "flavor-base-horrible");

        SequenceDataNode effects = [];
        foreach (var effect in args.Effects)
        {
            MappingDataNode e = [];
            e.Tag = "!type:" + properties[effect.Key].EffectName;
            e.Add("potency", effect.Value.ToString());
            effects.Add(e);
        }
        MappingDataNode medicine = [];
        medicine.Add("metabolismRate", args.MetabolismRate.ToString());
        medicine.Add("effects", effects);
        MappingDataNode metabolisms = [];
        metabolisms.Add("Medicine", medicine);
        reagent.Add("metabolisms", metabolisms);
        if (_protoMan.TryLoadDynamic(reagent))
        {
            _generatedReagents.Add(args.ID);
            CreateRecipe(args);
            _generatedRecipes.Add(args.ID);
        }
    }
    protected void CreateRecipe(GeneratedReagentData args)
    {
        var reagents = _protoMan.GetInstances<ReagentPrototype>();
        var properties = _protoMan.GetInstances<ReagentPropertyPrototype>();
        MappingDataNode recipe = [];
        recipe.Add("type", "reaction");
        recipe.Add("id", args.ID);
        recipe.Add("priority", "99");
        MappingDataNode ingredients = [];
        foreach (var ingredient in args.Recipe)
        {
            var (am, cata) = ingredient.Value;
            MappingDataNode ing = [];
            ing.Add("amount", am.ToString());
            if (cata)
                ing.Add("catalyst", "true");
            ingredients.Add(reagents[ingredient.Key].ID, ing);
        }
        MappingDataNode product = [];
        product.Add(args.ID, Math.Max(1, args.RecipeYield).ToString());
        recipe.Add("products", product);
        recipe.Add("reactants", ingredients);
        _protoMan.TryLoadDynamic(recipe);
    }
}
