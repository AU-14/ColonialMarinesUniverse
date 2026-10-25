//Pretty please license this under the MIT license :) - MACMAN2003
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Effects.Negative;
using Content.Shared.CMU14.Chemistry.Effects.Neutral;
using Content.Shared.CMU14.Chemistry.Effects.Positive;
using Content.Shared.CMU14.Chemistry.Effects.Reaction;
using Content.Shared.CMU14.Chemistry.Effects.Special;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Dataset;
using Content.Shared.EntityEffects;
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

namespace Content.Shared.CMU14.Chemistry.Reagents;

public abstract partial class SharedReagentGeneratorSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private ILogManager _logMan = default!;
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private SharedResearchDataTerminalSystem _researchdata = default!;
    private ISawmill _sawmill = default!;
    protected static readonly ProtoId<DatasetPrototype> _conflicts = "CMUConflictingProperties";
    /// <summary>Priority given to generated reactions; below the upstream default of 0.</summary>
    public const int GeneratedRecipePriority = -1;
    protected static readonly ProtoId<DatasetPrototype> _preciousMetals = "CMUPreciousMetals";
    /// <summary>Gen-class key for the precious-metal subset of Rare reagents.</summary>
    public const string PreciousMetalPool = "C4M";
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
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _effectToProperty = null);
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
        reagent.Add("flavor", "horrible");
        reagent.Add("genTier", args.GenTier.ToString());
        var worksOnTheDead = args.Effects.ContainsKey("Defibrillating") ||
                             args.Effects.ContainsKey("Neurocryogenic") ||
                             args.Effects.ContainsKey("Thanatometabolizing");
        // Regulating: no overdose thresholds at all. Alcoholic: counts as alcohol for metabolism/addiction code.
        var regulating = args.Effects.ContainsKey("Regulating");
        var alcoholic = args.Effects.ContainsKey("Alcoholic");
        if (worksOnTheDead)
            reagent.Add("worksOnTheDead", "true");

        SequenceDataNode effects = [];
        string effectyml = string.Empty;
        foreach (var effect in args.Effects)
        {
            string effectstr =
                $"      - !type:{properties[effect.Key].EffectName}\n" +
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
        var fire = ComputeFireStats(args.Effects, properties);
        MappingDataNode medicine = [];
        FixedPoint2 metabRate = 0.1f;
        if (args.Effects.ContainsKey("Intravenous"))
        {
            // Level 1 must still change something; a plain multiply by level was a no-op there.
            metabRate *= (1 + 0.5 * args.Effects["Intravenous"]);
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
        metabolisms.Add("Bloodstream", medicine);
        reagent.Add("metabolisms", metabolisms);
        //string yamlstr = reagent.ToString();
        string yamlstr =
            $"- type: reagent\n" +
            $"  id: {args.ID}\n" +
            $"  abstract: false\n" +
            $"  name: {args.Name}\n" +
            $"  desc: An experimental chemical\n" +
            $"  color: \"{args.Color.ToHexNoAlpha()}\"\n" +
            (regulating ? string.Empty :
                $"  overdose: {args.Overdose}\n" +
                $"  criticalOverdose: {args.CriticalOverdose}\n") +
            (alcoholic ? "  alcohol: true\n" : string.Empty) +
            fire +
            $"  isCM: true\n" +
            $"  generated: true\n" +
            $"  physicalDesc: reagent-physical-desc-unidentifiable\n" +
            $"  class: {args.Class.ToString()}\n" +
            $"  unknown: true\n" +
            $"  group: Generated\n" +
            $"  flags: Scannable\n" +
            $"  flavor: horrible\n" +
            $"  genTier: {args.GenTier}\n" +
            $"  reward: {args.ScanPointYield}\n" +
            (worksOnTheDead ? "  worksOnTheDead: true\n" : string.Empty) +
            $"  metabolisms:\n" +
            $"    Bloodstream:\n" +
            $"      metabolismRate: {metabRate.ToString()}\n" +
            $"      effects:\n{effectyml}";
        //_sawmill.Info(yamlstr);
        ChemYamls[args.ID] = yamlstr;
        _protoMan.LoadString(yamlstr, true);
        _generatedReagents.Add(args.ID);
        var recipeIds = CreateRecipe(args);
        _generatedRecipes.UnionWith(recipeIds);
        Dictionary<Type, HashSet<string>> mod = [];
        HashSet<string> hashy = [];
        hashy.Add(args.ID);
        mod.Add(typeof(ReagentPrototype), hashy);
        mod.Add(typeof(ReactionPrototype), recipeIds);
        _protoMan.ReloadPrototypes(mod);
        /* //UNCOMMENT WHEN https://github.com/space-wizards/RobustToolbox/pull/6609 IS MERGED
        if (_protoMan.TryLoadDynamic(reagent))
        {
            _generatedReagents.Add(args.ID);
            CreateRecipe(args);
            _generatedRecipes.Add(args.ID);
        }*/
    }
    /// <summary>Properties whose presence makes a generated chemical a flamer fuel in its own right.</summary>
    public static readonly string[] FuelProperties = ["Fueling", "Oxidizing", "Igniting", "Intensity", "Duration", "Radius", "Firepenetrating"];
    public const int BaseFuelIntensity = 15;
    public const int BaseFuelDuration = 20;
    public const int BaseFuelRadius = 4;

    /// <summary>
    /// Turns the catalogue's per-level fire numbers into the RMC <c>ReagentPrototype</c> fire fields. A chemical
    /// with a fuel property starts from a weak napalm baseline and every property's <c>*PerLevel</c> values are
    /// added per level; the <c>*ModPerLevel</c> values are always emitted so non-fuel chemicals (Flowing, Viscous)
    /// still shape a blend they are poured into. Returns YAML lines, or an empty string when nothing applies.
    /// </summary>
    public static string ComputeFireStats(Dictionary<string, int> effects, IReadOnlyDictionary<string, ReagentPropertyPrototype> properties)
    {
        var isFuel = false;
        foreach (var fuelProp in FuelProperties)
        {
            if (effects.ContainsKey(fuelProp))
                isFuel = true;
        }
        FixedPoint2 intensity = isFuel ? BaseFuelIntensity : 0, duration = isFuel ? BaseFuelDuration : 0, radius = isFuel ? BaseFuelRadius : 0;
        FixedPoint2 intensityMod = 0, durationMod = 0, radiusMod = 0;
        var oxidizing = 0;
        var fueling = 0;
        foreach (var (id, level) in effects)
        {
            if (!properties.TryGetValue(id, out var prop))
                continue;
            intensity += prop.IntensityPerLevel * level;
            duration += prop.DurationPerLevel * level;
            radius += prop.RangePerLevel * level;
            intensityMod += prop.IntensityModPerLevel * level;
            durationMod += prop.DurationModPerLevel * level;
            radiusMod += prop.RadiusModPerLevel * level;
            if (id == "Oxidizing")
                oxidizing = level;
            else if (id == "Fueling")
                fueling = level;
        }

        var yaml = string.Empty;
        if (isFuel)
        {
            var i = Math.Clamp((int) Math.Round(intensity.Float()), 1, 60);
            var d = Math.Clamp((int) Math.Round(duration.Float()), 1, 60);
            var r = Math.Clamp((int) Math.Round(radius.Float()), 1, 7);
            yaml += $"  intensity: {i}\n  duration: {d}\n  radius: {r}\n";
            if (oxidizing > fueling)
                yaml += "  fireEntity: RMCTileFireBlue\n";
            if (effects.ContainsKey("Flowing"))
                yaml += "  fireSpread: true\n";
            // Firepenetrating: the fire ignores heat resistance (SharedRMCFlammableSystem.DamageFromFire)
            // rather than simply burning hotter.
            if (effects.ContainsKey("Firepenetrating"))
                yaml += "  firePenetrating: true\n";
        }
        if (intensityMod != 0)
            yaml += $"  intensityMod: {intensityMod}\n";
        if (durationMod != 0)
            yaml += $"  durationMod: {durationMod}\n";
        if (radiusMod != 0)
            yaml += $"  radiusMod: {radiusMod}\n";
        return yaml;
    }

    protected HashSet<string> CreateRecipe(GeneratedReagentData args)
    {
        var reagents = _protoMan.GetInstances<ReagentPrototype>();
        var properties = _protoMan.GetInstances<ReagentPropertyPrototype>();
        MappingDataNode recipe = [];
        recipe.Add("type", "reaction");
        recipe.Add("id", args.ID);
        // Generated recipes always sit below every hand-authored reaction (priority 0) so they can never
        // shadow one, even when a colliding recipe is forced through by the simulator or an admin. In that
        // case the hand-authored reaction wins whenever its full reactant set is present.
        const int prio = GeneratedRecipePriority;
        recipe.Add("priority", prio.ToString());
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
        // Optimizing: each level adds one unit of product per reaction.
        var yield = Math.Max(1, args.RecipeYield) + (args.Effects.TryGetValue("Optimizing", out var optimizing) ? optimizing : 0);
        MappingDataNode product = [];
        product.Add(args.ID, yield.ToString());
        recipe.Add("products", product);
        recipe.Add("reactants", ingredients);
        HashSet<string> recipeIds = [];
        // Explosive: the reaction itself detonates, scaled by level. Sensitivity (spontaneous detonation
        // near the overdose threshold) is not modelled; the explosion fires on synthesis.
        var effectsYaml = string.Empty;
        if (args.Effects.TryGetValue("Explosive", out var explosive) && explosive > 0)
        {
            effectsYaml =
                "  effects:\n" +
                "  - !type:Explosion\n" +
                "    explosionType: RMC\n" +
                $"    maxIntensity: {10 * explosive}\n" +
                $"    intensityPerUnit: {(0.1f * explosive).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}\n" +
                "    intensitySlope: 2\n" +
                $"    maxTotalIntensity: {20 * explosive}\n";
        }
        LoadRecipe(args.ID, recipstr);

        // Sheets and uranium glass yield upstream Uranium, while RMC jugs and
        // generated contracts use RMCUranium. Both have the same displayed name.
        // Accept either source in research without changing other chemistry recipes.
        if (args.Recipe.ContainsKey("RMCUranium") && !args.Recipe.ContainsKey("Uranium"))
        {
            LoadRecipe($"CMUGeneratedUraniumAlternative{args.ID}",
                recipstr.Replace("    RMCUranium:\n", "    Uranium:\n"));
        }

        return recipeIds;

        void LoadRecipe(string id, string reactants)
        {
            var yamlstr =
                $"- type: reaction\n" +
                $"  id: {id}\n" +
                $"  abstract: false\n" +
                $"  priority: {prio}\n" +
                $"  reactants:\n{reactants}" +
                $"  products:\n" +
                $"    {args.ID}: {yield}\n" +
                effectsYaml;
            _protoMan.LoadString(yamlstr);
            RecipeYamls[id] = yamlstr;
            recipeIds.Add(id);
        }
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
            foreach (var metab in proto.Metabolisms.Metabolisms.Values)
            {
                metabs.Add(metab.MetabolismRate);
                foreach (var effect in metab.Effects)
                {
                    if (TryGetPropertyForEffect(effect, out var prop))
                    {
                        var ef = effect as RMCChemicalEffect;
                        int num = 1;
                        if (ef is not null)
                        {
                            num = (int)Math.Round(ef.Potency);
                        }
                        effects.TryAdd(prop.ID, num);
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

    private Dictionary<string, ReagentPropertyPrototype>? _effectToProperty;

    /// <summary>
    /// Maps a metabolism effect back to its property prototype by <see cref="ReagentPropertyPrototype.EffectName"/>,
    /// which is the field the generator emits. Property IDs and effect class names differ for some entries
    /// (e.g. <c>Intensity</c> → <c>CMUIntensity</c>), so keying by <c>GetType().Name</c> drops those.
    /// </summary>
    public bool TryGetPropertyForEffect(EntityEffect effect, [NotNullWhen(true)] out ReagentPropertyPrototype? prop)
    {
        if (_effectToProperty is null)
        {
            _effectToProperty = [];
            foreach (var candidate in _protoMan.EnumeratePrototypes<ReagentPropertyPrototype>())
            {
                if (candidate.Abstract || string.IsNullOrEmpty(candidate.EffectName))
                    continue;
                _effectToProperty.TryAdd(candidate.EffectName, candidate);
            }
        }
        return _effectToProperty.TryGetValue(effect.GetType().Name, out prop);
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
        // C4M: the precious-metal subset of the Rare class, reserved for Hard contracts.
        ChemicalGenClassesList.Add(PreciousMetalPool, []);
        ChemicalGenClassesList.Add("TAU", []);
        HashSet<string> precious = [];
        if (_protoMan.TryIndex(_preciousMetals, out var preciousDataset))
            precious.UnionWith(preciousDataset.Values);
        foreach (var chem in chems)
        {
            // Contract recipes and their hints use these pools. Keep water and sugar
            // available while excluding other food and drink reagents.
            if (chem.Value.Flags.HasFlag(ReagentFlags.NoGeneration) ||
                ((chem.Value.Group is "Foods" or "Drinks") &&
                 chem.Value.ID != "Water" &&
                 chem.Value.ID != "RMCSugar"))
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
                    if (precious.Contains(chem.Value.ID))
                        ChemicalGenClassesList[PreciousMetalPool].Add(chem.Value.ID);
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
    /// <summary>
    /// True when the generated recipe would collide with an existing reaction. Upstream resolves
    /// competing reactions by priority, so both containment directions matter: an existing recipe
    /// whose reactants are all in ours would fire alongside ours, and a generated recipe that is a
    /// subset of an existing one would steal that recipe's reactants whenever they are mixed.
    /// <paramref name="prio"/> is a priority that beats every colliding reaction, for callers that
    /// must load a colliding recipe anyway.
    /// </summary>
    public bool IsDuplicate(ref GeneratedReagentData data, [NotNullWhen(true)] out int? prio)
    {
        var reactions = _protoMan.GetInstances<ReactionPrototype>();
        prio = null;
        if (data.Recipe.Count == 0)
            return false;
        foreach (var reaction in reactions)
        {
            var reactants = reaction.Value.Reactants;
            if (reactants.Count == 0)
                continue;
            int matches = 0;
            foreach (var ingredient in reactants.Keys)
            {
                if (data.Recipe.ContainsKey(ingredient))
                    matches++;
            }
            // existing ⊆ generated, or generated ⊆ existing
            if (matches >= reactants.Count || matches >= data.Recipe.Count)
                prio = Math.Max(prio ?? int.MinValue, reaction.Value.Priority + 1);
        }
        return prio is not null;
    }
}
