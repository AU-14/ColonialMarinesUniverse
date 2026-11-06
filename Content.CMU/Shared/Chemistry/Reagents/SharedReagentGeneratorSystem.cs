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
    /// <summary>Oxidizing level at which a generated fuel burns blue (and only when it outweighs Fueling).</summary>
    public const int BlueFireOxidizingLevel = 3;

    /// <summary>The RMC fire numbers a generated chemical's properties add up to.</summary>
    public readonly record struct GeneratedFireStats(
        bool IsFuel,
        int Intensity,
        int Duration,
        int Radius,
        bool BlueFire,
        FixedPoint2 IntensityMod,
        FixedPoint2 DurationMod,
        FixedPoint2 RadiusMod);

    /// <summary>
    /// Adds up the catalogue's per-level fire numbers. A chemical with a fuel property starts from a weak napalm
    /// baseline and every property's <c>*PerLevel</c> values are added per level. Intensity and duration are
    /// clamped to 1-60 and radius to 1-7; all three are zero for a non-fuel.
    /// </summary>
    public static GeneratedFireStats ComputeFireValues(Dictionary<string, int> effects, IReadOnlyDictionary<string, ReagentPropertyPrototype> properties)
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

        if (!isFuel)
            return new GeneratedFireStats(false, 0, 0, 0, false, intensityMod, durationMod, radiusMod);

        return new GeneratedFireStats(
            true,
            Math.Clamp((int) Math.Round(intensity.Float()), 1, 60),
            Math.Clamp((int) Math.Round(duration.Float()), 1, 60),
            Math.Clamp((int) Math.Round(radius.Float()), 1, 7),
            // FLOW-04: blue fire only from a strong oxidizer that outweighs its fuel.
            oxidizing >= BlueFireOxidizingLevel && oxidizing > fueling,
            intensityMod,
            durationMod,
            radiusMod);
    }

    /// <summary>
    /// Turns the catalogue's per-level fire numbers into the RMC <c>ReagentPrototype</c> fire fields. A chemical
    /// with a fuel property starts from a weak napalm baseline and every property's <c>*PerLevel</c> values are
    /// added per level; the <c>*ModPerLevel</c> values are always emitted so non-fuel chemicals (Flowing, Viscous)
    /// still shape a blend they are poured into. Returns YAML lines, or an empty string when nothing applies.
    /// </summary>
    public static string ComputeFireStats(Dictionary<string, int> effects, IReadOnlyDictionary<string, ReagentPropertyPrototype> properties)
    {
        var stats = ComputeFireValues(effects, properties);
        var yaml = string.Empty;
        if (stats.IsFuel)
        {
            yaml += $"  intensity: {stats.Intensity}\n  duration: {stats.Duration}\n  radius: {stats.Radius}\n";
            // FLOW-22: no fireEntity. Colour comes from the effective intensity tier (blue from 40, which only
            // specialist tanks reach), so a strong oxidizer no longer forces the blue fire entity.
            if (effects.ContainsKey("Flowing"))
                yaml += "  fireSpread: true\n";
            // Firepenetrating: the fire ignores heat resistance (SharedRMCFlammableSystem.DamageFromFire)
            // rather than simply burning hotter; from level 3 it melts structures (FLOW-23).
            if (effects.TryGetValue("Firepenetrating", out var penetrating) && penetrating > 0)
                yaml += $"  firePenetrating: true\n  firePenetratingLevel: {penetrating}\n";
        }
        if (stats.IntensityMod != 0)
            yaml += $"  intensityMod: {stats.IntensityMod}\n";
        if (stats.DurationMod != 0)
            yaml += $"  durationMod: {stats.DurationMod}\n";
        if (stats.RadiusMod != 0)
            yaml += $"  radiusMod: {stats.RadiusMod}\n";
        return yaml;
    }

    /// <summary>Properties that make a generated chemical ignite, and possibly detonate, once heated.</summary>
    public static readonly string[] IgnitionProperties = ["Explosive", "Oxidizing", "Fueling", "Igniting"];
    /// <summary>Suffix of the generated reaction that ignites a combustible chemical: <c>{ID}Ignition</c>.</summary>
    public const string IgnitionRecipeSuffix = "Ignition";
    public const float IgnitionBaseTemperature = 500f;
    public const float IgnitionTemperaturePerLevel = 15f;
    /// <summary>
    /// No chemical ignites below this. It sits above <see cref="SynthesisMaxTemperature"/>, so heat from a
    /// Hyperthermic synthesis can never set its own product off.
    /// </summary>
    public const float IgnitionMinTemperature = 410f;
    /// <summary>Temperature change per Hyperthermic/Hypothermic level for a fully converted synthesis.</summary>
    public const float SynthesisKelvinPerLevel = 12f;
    public const float SynthesisMinTemperature = 250f;
    public const float SynthesisMaxTemperature = 400f;
    /// <summary>Mixing category that Hard (GenTier 3+) generated recipes need: the lid-closed "Mix" verb.</summary>
    public const string GeneratedMixingCategory = "CMUMix";
    public const int MixGatedGenTier = 3;
    public const int ExplosiveLevelCap = 10;
    /// <summary>Strong-IED ceiling (AU14IEDLarge): about 10.5 tiles of radius and 15.7 peak intensity.</summary>
    public const float MaxExplosionRadius = 10.5f;
    public const float MaxExplosionPeak = 15.7f;
    /// <summary>Units of an explosive chemical that reach the total-intensity cap.</summary>
    public const float ExplosionUnitsToCap = 5f;

    public static string IgnitionRecipeId(string reagentId) => reagentId + IgnitionRecipeSuffix;

    /// <summary>The temperature (K) at which a chemical whose strongest combustion property has this level ignites.</summary>
    public static float IgnitionTemperature(int level)
        => MathF.Max(IgnitionMinTemperature, IgnitionBaseTemperature - IgnitionTemperaturePerLevel * level);

    /// <summary>
    /// FLOW-01: level-scaled explosion for the Explosive property. With Lc = min(L, 10) the blast has radius
    /// <c>r = min(1.05 Lc, 10.5)</c> and peak <c>P = min(4 + 1.2 Lc, 15.7)</c>, so L10 matches AU14IEDLarge.
    /// Total intensity is the cone volume <c>π/3 · P · r²</c>, reached with <see cref="ExplosionUnitsToCap"/> units.
    /// </summary>
    public static void ComputeExplosion(int level, out float slope, out float max, out float total, out float perUnit)
    {
        var lc = Math.Clamp(level, 1, ExplosiveLevelCap);
        var radius = MathF.Min(1.05f * lc, MaxExplosionRadius);
        max = MathF.Min(4f + 1.2f * lc, MaxExplosionPeak);
        slope = max / radius;
        total = MathF.PI / 3f * max * radius * radius;
        perUnit = total / ExplosionUnitsToCap;
    }

    private static string FormatFloat(float value)
        => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

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
        // ARCH-01 P1: Hyperthermic heats and Hypothermic cools the synthesis. The delta is per unit reaction and
        // scales with yield so a fully converted batch moves about SynthesisKelvinPerLevel K per level whatever
        // its size. The clamp stays below IgnitionMinTemperature so a synthesis never sets its product off.
        // The Explosive detonation is not here any more: it lives on the {ID}Ignition reaction below.
        var effectsYaml = string.Empty;
        var heatLevel = (args.Effects.TryGetValue("Hyperthermic", out var hyper) ? hyper : 0) -
                        (args.Effects.TryGetValue("Hypothermic", out var hypo) ? hypo : 0);
        if (heatLevel != 0)
        {
            effectsYaml =
                "  effects:\n" +
                "  - !type:AdjustSolutionThermalEnergy\n" +
                $"    delta: {FormatFloat(SynthesisKelvinPerLevel * heatLevel * yield)}\n" +
                $"    minTemp: {FormatFloat(SynthesisMinTemperature)}\n" +
                $"    maxTemp: {FormatFloat(SynthesisMaxTemperature)}\n";
        }
        // ARCH-04: Hard recipes need the lid-closed Mix verb on top of the right reagents.
        var mixYaml = args.GenTier >= MixGatedGenTier
            ? $"  requiredMixerCategories:\n  - {GeneratedMixingCategory}\n"
            : string.Empty;
        LoadRecipe(args.ID, recipstr);

        // Sheets and uranium glass yield upstream Uranium, while RMC jugs and
        // generated contracts use RMCUranium. Both have the same displayed name.
        // Accept either source in research without changing other chemistry recipes.
        if (args.Recipe.ContainsKey("RMCUranium") && !args.Recipe.ContainsKey("Uranium"))
        {
            LoadRecipe($"CMUGeneratedUraniumAlternative{args.ID}",
                recipstr.Replace("    RMCUranium:\n", "    Uranium:\n"));
        }

        LoadIgnitionRecipe(args, properties, recipeIds);

        return recipeIds;

        void LoadRecipe(string id, string reactants)
        {
            // conserveEnergy: false. Generated recipes turn several units into one, and upstream energy
            // conservation would multiply the solution's temperature by that ratio (3:1 at room temperature
            // is ~880 K), which would set off every ignition reaction on synthesis.
            var yamlstr =
                $"- type: reaction\n" +
                $"  id: {id}\n" +
                $"  abstract: false\n" +
                $"  priority: {prio}\n" +
                $"  conserveEnergy: false\n" +
                mixYaml +
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
    /// ARCH-01 P2: a chemical with Explosive, Oxidizing, Fueling or Igniting gets a second reaction, <c>{ID}Ignition</c>,
    /// that consumes it once the solution is heated to <see cref="IgnitionTemperature"/>. Explosive detonates
    /// with the FLOW-01 parameters; any fuel sets the floor around the holder on fire with the fire spec of the
    /// whole solution (FLOW-22: CMUSolutionTileFire, a circle by √units, no fixed stats). The engine
    /// re-checks reactions on every temperature change, so no watcher is needed. Not mix-gated.
    /// </summary>
    private void LoadIgnitionRecipe(
        GeneratedReagentData args,
        IReadOnlyDictionary<string, ReagentPropertyPrototype> properties,
        HashSet<string> recipeIds)
    {
        var level = 0;
        foreach (var ignitionProp in IgnitionProperties)
        {
            if (args.Effects.TryGetValue(ignitionProp, out var propLevel))
                level = Math.Max(level, propLevel);
        }
        if (level <= 0)
            return;

        var id = IgnitionRecipeId(args.ID);
        var yamlstr =
            $"- type: reaction\n" +
            $"  id: {id}\n" +
            $"  abstract: false\n" +
            $"  priority: {GeneratedRecipePriority}\n" +
            $"  minTemp: {FormatFloat(IgnitionTemperature(level))}\n" +
            // Burning the chemical away must not heat what is left and chain further ignitions.
            $"  conserveEnergy: false\n" +
            $"  reactants:\n" +
            $"    {args.ID}:\n" +
            $"      amount: 1\n" +
            $"  effects:\n";

        if (args.Effects.TryGetValue("Explosive", out var explosive) && explosive > 0)
        {
            ComputeExplosion(explosive, out var slope, out var max, out var total, out var perUnit);
            yamlstr +=
                "  - !type:Explosion\n" +
                "    explosionType: RMC\n" +
                $"    maxIntensity: {FormatFloat(max)}\n" +
                $"    intensityPerUnit: {FormatFloat(perUnit)}\n" +
                $"    intensitySlope: {FormatFloat(slope)}\n" +
                $"    maxTotalIntensity: {FormatFloat(total)}\n";
        }

        // FLOW-22: every fuel (any fuel property, so its intensity/duration/radius are emitted) burns with the fire
        // spec resolved from the whole reacting solution, exactly as it would in a flamer; nothing is fixed here.
        var fire = ComputeFireValues(args.Effects, properties);
        var combustible = (args.Effects.TryGetValue("Fueling", out var fuelLevel) && fuelLevel > 0) ||
                          (args.Effects.TryGetValue("Oxidizing", out var oxLevel) && oxLevel > 0) ||
                          (args.Effects.TryGetValue("Igniting", out var ignLevel) && ignLevel > 0);
        if (combustible && fire.IsFuel)
        {
            yamlstr +=
                "  - !type:CMUSolutionTileFire\n" +
                $"    reagent: {args.ID}\n";
        }

        _protoMan.LoadString(yamlstr);
        RecipeYamls[id] = yamlstr;
        recipeIds.Add(id);
    }

    /// <summary>True for a generated <c>{ID}Ignition</c> reaction (see <see cref="LoadIgnitionRecipe"/>).</summary>
    public bool IsIgnitionRecipe(string reactionId)
        => reactionId.EndsWith(IgnitionRecipeSuffix, StringComparison.Ordinal) && _generatedRecipes.Contains(reactionId);

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
            // A generated chemical's ignition consumes only itself and only when hot, so a recipe that uses
            // that chemical as an ingredient does not collide with it.
            if (IsIgnitionRecipe(reaction.Key))
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
