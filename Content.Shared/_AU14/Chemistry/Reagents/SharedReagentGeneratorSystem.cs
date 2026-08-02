//Pretty please license this under the MIT license :) - MACMAN2003
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared._CMU14.Chemistry.Effects.Negative;
using Content.Shared._CMU14.Chemistry.Effects.Neutral;
using Content.Shared._CMU14.Chemistry.Effects.Positive;
using Content.Shared._CMU14.Chemistry.Effects.Reaction;
using Content.Shared._CMU14.Chemistry.Effects.Special;
using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Dataset;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Reagents;

public abstract partial class SharedReagentGeneratorSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private ILogManager _logMan = default!;
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private SharedResearchDataTerminalSystem _researchdata = default!;
    private ISawmill _sawmill = default!;
    protected static readonly ProtoId<DatasetPrototype> _conflicts = "CMUConflictingProperties";
    protected List<List<string>> _unfoldedConflicts = [];
    public List<List<string>> UnfoldedConflicts { get => _unfoldedConflicts; }
    protected Dictionary<string, List<string>> _unfoldedCombinations = [];
    public Dictionary<string, List<string>> UnfoldedCombinations { get => _unfoldedCombinations; }
    [ViewVariables(VVAccess.ReadOnly)]
    protected HashSet<string> _generatedReagents = [];
    public HashSet<string> GeneratedReagents { get => _generatedReagents; }
    [ViewVariables(VVAccess.ReadOnly)]
    protected HashSet<string> _generatedRecipes = [];
    public HashSet<string> GeneratedRecipes { get => _generatedRecipes; }
    [ViewVariables(VVAccess.ReadOnly)]
    protected HashSet<string> _lockedDownChems = [];
    public HashSet<string> LockedDownChems { get => _lockedDownChems; }
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<string> KnownProperties = [];
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, int> IdentifiedChemicals = [];
    public Dictionary<string, HashSet<string>> ChemicalGenClassesList = [];

    protected Dictionary<string, string> ChemYamls = [];
    protected Dictionary<string, string> RecipeYamls = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<GenerateReagentEvent>(CreateReagent);
        SubscribeAllEvent<RetroactiveLockdownEvent>(OnRetroactiveLockdown);
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
        string description = string.Empty;
        MappingDataNode reagent = [];
        reagent.Add("type", "reagent");
        reagent.Add("id", args.ID);
        reagent.Add("name", args.Name);
        //reagent.Add("desc", "An unidentified chemical");
        reagent.Add("color", args.Color.ToHexNoAlpha());
        reagent.Add("unknown", "true");
        reagent.Add("group", "Generated");
        reagent.Add("class", args.Class.ToString());
        reagent.Add("flags", "Scannable");
        reagent.Add("reward", args.ScanPointYield.ToString());
        reagent.Add("overdose", args.Overdose.ToString());
        reagent.Add("criticalOverdose", args.CriticalOverdose.ToString());
        reagent.Add("isCM", "true");
        reagent.Add("generated", "true");
        reagent.Add("physicalDesc", "reagent-physical-desc-unidentifiable");
        reagent.Add("flavor", "flavor-base-horrible");
        reagent.Add("genTier", args.GenTier.ToString());

        SequenceDataNode effects = [];
        string effectyml = string.Empty;
        foreach (var effect in args.Effects)
        {
            string effectstr =
                $"      - !type:{properties[effect.Key].ID}\n" +
                $"        potency: {effect.Value}\n";
            effectyml += effectstr;
            MappingDataNode e = [];
            e.Tag = "!type:" + properties[effect.Key].EffectName;
            e.Add("potency", effect.Value.ToString());
            effects.Add(e);
            description += string.Format("[bold]{0} Level {1}[/bold]\n",
                properties[effect.Key].LocalizedName, effect.Value.ToString());
            description += properties[effect.Key].LocalizedDescription + '\n';
        }
        reagent.Add("desc", description);
        MappingDataNode medicine = [];
        FixedPoint2 metabRate = 0.1f;
        if (args.Effects.ContainsKey("Intravenous"))
        {
            metabRate *= args.Effects["Intravenous"];
        }
        if (args.Effects.ContainsKey("Hypermetabolic"))
        {
            metabRate *= ((1 + 0.25) * args.Effects["Hypermetabolic"]);
        }
        if (args.Effects.ContainsKey("Hypometabolic"))
        {
            // 0.01 is as close as you can get to 0.005 with FixedPoint2
            metabRate = (FixedPoint2)MathF.Max((float)metabRate / ((1f + 0.35f) * args.Effects["Hypometabolic"]), 0.01f);
        }
        medicine.Add("metabolismRate", metabRate.ToString());
        medicine.Add("effects", effects);
        MappingDataNode metabolisms = [];
        metabolisms.Add("Medicine", medicine);
        reagent.Add("metabolisms", metabolisms);
        //string yamlstr = reagent.ToString();
        string yamlstr =
            $"- type: reagent\n" +
            $"  id: {args.ID}\n" +
            $"  abstract: false\n" +
            $"  name: {args.Name}\n" +
            $"  desc: An experimental chemical\n" +
            $"  color: \"{args.Color.ToHexNoAlpha()}\"\n" +
            $"  overdose: {args.Overdose}\n" +
            $"  criticalOverdose: {args.CriticalOverdose}\n" +
            $"  isCM: true\n" +
            $"  generated: true\n" +
            $"  physicalDesc: reagent-physical-desc-unidentifiable\n" +
            $"  class: {args.Class.ToString()}\n" +
            $"  unknown: true\n" +
            $"  group: Generated\n" +
            $"  flags: Scannable\n" +
            $"  flavor: flavor-base-horrible\n" +
            $"  genTier: {args.GenTier}\n" +
            $"  reward: {args.ScanPointYield}\n" +
            $"  metabolisms:\n" +
            $"    Medicine:\n" +
            $"      metabolismRate: {metabRate.ToString()}\n" +
            $"      effects:\n{effectyml}";
        //_sawmill.Info(yamlstr);
        ChemYamls.Add(args.ID, yamlstr);
        _protoMan.LoadString(yamlstr, true);
        _generatedReagents.Add(args.ID);
        CreateRecipe(args);
        _generatedRecipes.Add(args.ID);
        Dictionary<Type, HashSet<string>> mod = [];
        HashSet<string> hashy = [];
        hashy.Add(args.ID);
        mod.Add(typeof(ReagentPrototype), hashy);
        mod.Add(typeof(ReactionPrototype), hashy);
        _protoMan.ReloadPrototypes(mod);
        /* //UNCOMMENT WHEN https://github.com/space-wizards/RobustToolbox/pull/6609 IS MERGED
        if (_protoMan.TryLoadDynamic(reagent))
        {
            _generatedReagents.Add(args.ID);
            CreateRecipe(args);
            _generatedRecipes.Add(args.ID);
        }*/
    }
    protected void CreateRecipe(GeneratedReagentData args)
    {
        var reagents = _protoMan.GetInstances<ReagentPrototype>();
        var properties = _protoMan.GetInstances<ReagentPropertyPrototype>();
        MappingDataNode recipe = [];
        recipe.Add("type", "reaction");
        recipe.Add("id", args.ID);
        IsDuplicate(ref args, out int? prio);
        prio ??= 0;
        recipe.Add("priority", (prio.Value + 1).ToString());
        MappingDataNode ingredients = [];
        string recipstr = string.Empty;
        foreach (var ingredient in args.Recipe)
        {
            var (am, cata) = ingredient.Value;
            recipstr +=
                $"    {ingredient.Key}:\n" +
                $"      amount: {am}\n";
            MappingDataNode ing = [];
            ing.Add("amount", am.ToString());
            if (cata)
                recipstr += "      catalyst: true\n";
                //ing.Add("catalyst", "true");
            ingredients.Add(reagents[ingredient.Key].ID, ing);
        }
        MappingDataNode product = [];
        product.Add(args.ID, Math.Max(1, args.RecipeYield).ToString());
        recipe.Add("products", product);
        recipe.Add("reactants", ingredients);
        string yamlstr =
            $"- type: reaction\n" +
            $"  id: {args.ID}\n" +
            $"  abstract: false\n" +
            $"  priority: {prio.Value + 1}\n" +
            $"  reactants:\n{recipstr}" +
            $"  products:\n" +
            $"    {args.ID}: {args.RecipeYield}\n";
        //_sawmill.Info(yamlstr);
        _protoMan.LoadString(yamlstr);
        RecipeYamls.Add(args.ID, yamlstr);
        /* UNCOMMENT WHEN https://github.com/space-wizards/RobustToolbox/pull/6609 IS MERGED
        _protoMan.TryLoadDynamic(recipe);
        */
    }

    /// <summary>
    /// this is so that prototyped chems can be used in the research machines
    /// </summary>
    /// <param name="proto"></param>
    /// <returns></returns>
    public GeneratedReagentData ConvertToGRD(ReagentPrototype proto)
    {
        var props = _protoMan.GetInstances<ReagentPropertyPrototype>();
        var recips = _protoMan.GetInstances<ReactionPrototype>();
        GeneratedReagentData working = new();
        working.ID = proto.ID;
        working.Name = proto.LocalizedName;
        working.Class = proto.Class;
        Dictionary<string, int> effects = [];
        FixedPoint2 meanMetab = 0.1;
        List<FixedPoint2> metabs = [];
        if (proto.Metabolisms is not null)
        {
            foreach (var metab in proto.Metabolisms.Values)
            {
                metabs.Add(metab.MetabolismRate);
                foreach (var effect in metab.Effects)
                {

                    if (props.TryGetValue(effect.GetType().Name, out var prop))
                    {
                        var ef = effect as RMCChemicalEffect;
                        int num = 1;
                        if (ef is not null)
                        {
                            num = (int)Math.Round(ef.Potency);
                        }
                        effects.Add(effect.GetType().Name, num);
                    }
                }
            }
        }
        working.Effects = effects;
        if (metabs.Count != 0)
        {
            FixedPoint2 sum = 0;
            foreach (var idx in metabs)
            {
                sum += idx;
            }
            meanMetab = sum / metabs.Count;
        }
        working.MetabolismRate = meanMetab;
        working.ScanPointYield = proto.Reward;
        working.GenTier = proto.GenTier;
        working.Color = proto.SubstanceColor;
        working.Overdose = proto.Overdose ?? 5;
        working.CriticalOverdose = proto.CriticalOverdose ?? 10;
        Dictionary<string, (int, bool)> recipe = [];
        if (recips.TryGetValue(proto.ID, out var react))
        {
            foreach (var ing in react.Reactants)
            {
                recipe.Add(ing.Key, ((int)ing.Value.Amount, ing.Value.Catalyst));
            }
        }
        working.Recipe = recipe;
        return working;
    }

    public void SaveNewProperties(HashSet<ReagentPropertyPrototype> props)
    {
        foreach (var prop in props)
        {
            if (prop.Category.HasFlag(ReagentPropertyTypeEnum.Unadjustable)
                || prop.Category.HasFlag(ReagentPropertyTypeEnum.Anomalous))
                continue;
            KnownProperties.Add(prop.ID);
        }
        if (_netMan.IsClient)
            return;
        var ev = new SyncKnownPropertiesEvent(KnownProperties);
        RaiseNetworkEvent(ev);
    }

    protected List<List<string>> UnfoldConflicts()
    {
        _protoMan.TryIndex(_conflicts, out var confs);
        if (confs is null)
            return [];
        var vals = confs.Values.ToList();
        var list = new List<List<string>>();
        foreach (var val in vals)
        {
            var sublist = val.Split(',').ToList<string>();
            list.Add(sublist);
        }
        return list;
    }
    private void OnRetroactiveLockdown(RetroactiveLockdownEvent args)
    {
        _lockedDownChems.UnionWith(args.Chems);
    }
    protected void PrepareChems()
    {
        var chems = _protoMan.GetInstances<ReagentPrototype>();
        ChemicalGenClassesList.Add("C", []);
        ChemicalGenClassesList.Add("C1", []);
        ChemicalGenClassesList.Add("C2", []);
        ChemicalGenClassesList.Add("C3", []);
        ChemicalGenClassesList.Add("C4", []);
        ChemicalGenClassesList.Add("C5", []);
        ChemicalGenClassesList.Add("C6", []);
        ChemicalGenClassesList.Add("H1", []);
        //_chemicalGenClassesList.Add("T1", []);
        //_chemicalGenClassesList.Add("T2", []);
        //_chemicalGenClassesList.Add("T3", []);
        //_chemicalGenClassesList.Add("T4", []);
        //_chemicalGenClassesList.Add("T5", []);
        ChemicalGenClassesList.Add("TAU", []);
        foreach (var chem in chems)
        {
            if (chem.Value.Flags.HasFlag(ReagentFlags.NoGeneration))
                continue;
            switch (chem.Value.Class)
            {
                case ReagentClass.Basic:
                    ChemicalGenClassesList["C1"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Common:
                    ChemicalGenClassesList["C2"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Uncommon:
                    ChemicalGenClassesList["C3"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Rare:
                    ChemicalGenClassesList["C4"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Special:
                    ChemicalGenClassesList["C5"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Ultra:
                    ChemicalGenClassesList["C6"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Hydro:
                    ChemicalGenClassesList["H1"].Add(chem.Value.ID);
                    break;
                default:
                    break;
            }
            if (chem.Value.Class != ReagentClass.None)
            {
                ChemicalGenClassesList["C"].Add(chem.Value.ID);
            }
        }
        if (_netMan.IsClient)
            return;
        var ev = new SyncGenClassesEvent(ChemicalGenClassesList);
        RaiseNetworkEvent(ev);
    }
    public bool IsDuplicate(ref GeneratedReagentData data, [NotNullWhen(true)] out int? prio)
    {
        var reactions = _protoMan.GetInstances<ReactionPrototype>();
        prio = null;
        //this fucking sucks
        foreach (var reaction in reactions)
        {
            int matches = 0;
            foreach (var ingredient in reaction.Value.Reactants)
            {
                if (data.Recipe.ContainsKey(ingredient.Key))
                    matches++;
                if (matches >= reaction.Value.Reactants.Count)
                {
                    prio = reaction.Value.Priority + 1;
                    return true;
                }

            }
        }
        return false;
    }
}
