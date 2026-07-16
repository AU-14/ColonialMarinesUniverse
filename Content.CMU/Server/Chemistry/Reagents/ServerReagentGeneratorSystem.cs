using Content.Server.GameTicking;
using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Dataset;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server._AU14.Chemistry.Reagents;

public sealed partial class ServerReagentGeneratorSystem : SharedReagentGeneratorSystem
{

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private ILogManager _logMan = default!;
    [Dependency] private IServerNetManager _netMan = default!;
    [Dependency] private SharedResearchDataTerminalSystem _researchdata = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private MetaDataSystem _mets = default!;
    [Dependency] private RMCReagentSystem _reagent = default!;

    private static readonly ProtoId<DatasetPrototype> _namePrefixes = "CMURandChemPrefix";
    private static readonly ProtoId<DatasetPrototype> _nameMiddles = "CMURandChemWordroot";
    private static readonly ProtoId<DatasetPrototype> _nameSuffixes = "CMURandChemSuffix";
    private static readonly ProtoId<DatasetPrototype> _combinations = "CMUCombiningProperties";
    //[ViewVariables(VVAccess.ReadOnly)]
    //private HashSet<string> _generatedReagents = [];
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, GeneratedReagentData> ProceduralReagentData = [];
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, GeneratedReagentData> ReagentData = [];
    //[ViewVariables(VVAccess.ReadOnly)]
    //private HashSet<string> _generatedRecipes = [];
    private ISawmill _sawmill = default!;
    
    
    private Dictionary<string, HashSet<string>> _propertiesList = [];
    public Dictionary<string, HashSet<string>> Properties { get => _propertiesList; }
    public Dictionary<string, HashSet<string>> _generatedPropertiesList = [];
    


    private int _legendaryCombineProperties = 3;
    private FixedPoint2 _defaultChemMetabolism = 0.1;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LoadingMapsEvent>(PreMapLoad);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        _netMan.Connected += OnClientConnected;
        _sawmill = _logMan.GetSawmill("reagent");
    }

    private async void OnClientConnected(object? sender, NetChannelArgs args)
    {
        var ev = new SendReagentDataEvent(ProceduralReagentData,
            _lockedDownChems,
            KnownProperties,
            IdentifiedChemicals,
            ChemicalGenClassesList,
            _unfoldedCombinations,
            _unfoldedConflicts);
        RaiseNetworkEvent(ev, args.Channel);
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
        ProceduralReagentData.Clear();
        KnownProperties.Clear();
        var props = _protoMan.EnumeratePrototypes<ReagentPropertyPrototype>();

        foreach (var prop in props)
        {
            if (prop.Starter)
            {
                KnownProperties.Add(prop.ID);
            }
        }
        _propertiesList.Clear();
        ReagentData.Clear();
        ChemicalGenClassesList.Clear();
        _generatedPropertiesList.Clear();
        _unfoldedCombinations.Clear();
        _unfoldedConflicts.Clear();
        IdentifiedChemicals.Clear();
        _lockedDownChems.Clear();
    }

    private void PreMapLoad(LoadingMapsEvent args)
    {
        //CreateEvilDex();
        Setup();
    }

    private void Setup()
    {
        _unfoldedConflicts = UnfoldConflicts();
        _unfoldedCombinations = UnfoldCombinations();
        PrepareProperties();
        PrepareChems();
        var knev = new SyncKnownPropertiesEvent(KnownProperties);
        RaiseNetworkEvent(knev);
    }
    
    #region Paperwork


    public ResearchReportData? CreateReport(string reagentID, bool infoOnly = false, int sampleNumber = 0, int? clearance = null)
    {
        if (reagentID == string.Empty || !_protoMan.GetInstances<ReagentPrototype>().TryGetValue(reagentID, out var chem))
            return null;
        ResearchReportData data;
        data.Name = string.Empty;
        data.Info = string.Empty;
        data.Valid = true;
        data.Completed = false;
        data.Icon = ResearchReportIconEnum.None;
        var props = _protoMan.GetInstances<ReagentPropertyPrototype>();
        var reacts = _protoMan.GetInstances<ReactionPrototype>();
        var reagents = _protoMan.GetInstances<ReagentPrototype>();
        data.Info += string.Format("[bold]ID:[/bold] [italic]{0}[/italic]\n", chem.LocalizedName);
        data.Info += Loc.GetString("research-database-details") + "\n";
        HashSet<ReagentPropertyPrototype> properties = [];
        int overdose = (int?)chem.Overdose ?? 0;
        int critoverdose = (int?)chem.CriticalOverdose ?? 1;
        if (chem.Class >= ReagentClass.Ultra)
        {
            if ((clearance ?? _researchdata.Clearance) >= chem.GenTier || infoOnly)
            {
                FixedPoint2 metabRate = 0.01f;
                if (chem.Metabolisms is not null)
                {
                    //evil fucking wretched awful horrible no good shit
                    foreach (var metab in chem.Metabolisms.Values)
                    {
                        metabRate = metab.MetabolismRate;
                        foreach (var effect in metab.Effects)
                        {
                            if (props.TryGetValue(effect.GetType().Name, out var prop))
                            {
                                var ef = effect as RMCChemicalEffect;
                                int num = -1;
                                if (ef is not null)
                                    num = (int)ef.Potency;
                                data.Name += " " + prop.Code + num;
                                properties.Add(prop);
                            }
                        }
                    }
                }
                //TODO: reaction indicators
                data.Info += Loc.GetString("research-report-reaction-header") +
                    "\nDATABASE ERROR. \nWY_FTP RETURN CODE 0b1000100110\n";
                metabRate = FixedPoint2.Max(0.01f, metabRate);
                data.Info += '\n';
                data.Info += chem.LocalizedDescription + "\n";
                if (chem.Metabolisms is not null)
                {
                    string fx = string.Empty;
                    foreach (var metab in chem.Metabolisms.Values)
                    {
                        foreach (var effect in metab.Effects)
                        {
                            if (props.TryGetValue(effect.GetType().Name, out var prop) && effect is RMCChemicalEffect ef)
                            {
                                fx += $"[bold]{prop.LocalizedName} Level {ef.Potency}[/bold]\n";
                                fx += $"{prop.LocalizedDescription}\n";
                            }
                        }
                    }
                    data.Info += fx;
                }
                data.Info += Loc.GetString("research-report-overdose", ("OD", overdose)) + '\n';
                data.Info += Loc.GetString("research-report-crit-overdose", ("COD", critoverdose)) + '\n';
                data.Info += Loc.GetString("research-report-metab-mult", ("MULT", _defaultChemMetabolism / metabRate)) + '\n';
                data.Completed = true;
                data.Icon = ResearchReportIconEnum.Full;
            }
            else
            {
                data.Info += Loc.GetString("research-report-clearance-insuf", ("CLEAR", chem.GenTier)) + '\n';
                data.Icon = ResearchReportIconEnum.Partial;
                data.Valid = false;
            }


        }
        else if (chem.Class == ReagentClass.Special && (clearance ?? _researchdata.Clearance) < 6 && !infoOnly)
        {
            data.Info += Loc.GetString("research-report-x-needed") + '\n';
            //data.Info += "Classified:[italic] Clearance level [bold]X[/bold] required to read the database entry.[/italic]\n";
            data.Icon = ResearchReportIconEnum.Partial;
            data.Valid = false;
        }
        else if (chem.LocalizedDescription != "")
        {
            data.Info += Loc.GetString("research-report-reaction-header") +
                    "\nDATABASE ERROR. \nWY_FTP RETURN CODE 0b1000100110\n";
            data.Info += chem.LocalizedDescription + "\n";
            if (chem.Metabolisms is not null)
            {
                string fx = string.Empty;
                foreach (var metab in chem.Metabolisms.Values)
                {
                    foreach (var effect in metab.Effects)
                    {
                        if (props.TryGetValue(effect.GetType().Name, out var prop) && effect is RMCChemicalEffect ef)
                        {
                            fx += $"[bold]{prop.LocalizedName} Level {ef.Potency}[/bold]\n";
                            fx += $"{prop.LocalizedDescription}\n";
                        }
                    }
                }
                data.Info += fx;
            }
            data.Info += Loc.GetString("research-report-overdose", ("OD", overdose)) + '\n';
            data.Info += Loc.GetString("research-report-crit-overdose", ("COD", critoverdose)) + '\n';
            FixedPoint2 metabRate = 0.01;
            if (chem.Metabolisms is not null)
            {
                metabRate = chem.Metabolisms.Values[0].MetabolismRate;
                foreach (var metab in chem.Metabolisms.Values)
                {
                    metabRate = metab.MetabolismRate;
                    foreach (var effect in metab.Effects)
                    {
                        if (props.TryGetValue(effect.GetType().Name, out var prop))
                        {
                            properties.Add(prop);
                        }
                    }
                }
            }
            metabRate = metabRate == 0 ? 0.1 : metabRate;
            data.Info += Loc.GetString("research-report-metab-mult", ("MULT", _defaultChemMetabolism / metabRate)) + '\n';
            data.Completed = true;
            data.Icon = ResearchReportIconEnum.Full;
        }
        else
        {
            data.Info += Loc.GetString("research-report-no-data");
            data.Icon = ResearchReportIconEnum.Synthesis;
            data.Valid = false;
        }
        if (chem.Class >= ReagentClass.Special && !IdentifiedChemicals.ContainsKey(chem.ID) && !infoOnly)
        {
            data.Info += Loc.GetString("research-report-spectrum-saved", ("NAME", chem.LocalizedName)) + '\n';
        }
        data.Info += Loc.GetString("research-report-composition-details") + '\n';
        Dictionary<string, int> ingredients = [];
        Dictionary<string, int> catalysts = [];
        if (reacts.TryGetValue(reagentID, out var reaction))
        {
            foreach(var ing in reaction.Reactants)
            {
                if (ing.Value.Catalyst)
                    catalysts.Add(ing.Key, (int)ing.Value.Amount);
                else
                {
                    ingredients.Add(ing.Key, (int)ing.Value.Amount);
                }
            }
        }
        if (ingredients.Count > 0)
        {
            foreach(var ing in ingredients.Keys)
            {
                var gredient = reagents[ing];
                if(gredient is null || (gredient.Class >= ReagentClass.Special &&
                    !IdentifiedChemicals.ContainsKey(gredient.ID) && !infoOnly && gredient.Class != ReagentClass.Hydro))
                {
                    data.Info += Loc.GetString("research-report-unknown-emission") + '\n';
                    data.Completed = false;
                    data.Valid = false;
                }
                else
                {
                    data.Info += Loc.GetString("research-report-ingredient", ("AMOUNT", ingredients[ing]),
                        ("NAME", gredient.LocalizedName)) + '\n';
                }
            }
            if (catalysts.Count > 0)
            {
                data.Info += Loc.GetString("research-report-catalyst-details") + '\n';
                foreach(var ing in catalysts.Keys)
                {
                    var cat = reagents[ing];
                    if (cat is null || (cat.Class >= ReagentClass.Special &&
                        !IdentifiedChemicals.ContainsKey(cat.ID) && !infoOnly))
                    {
                        data.Info += Loc.GetString("research-report-unknown-emission") + '\n';
                        data.Completed = false;
                    }
                    else
                    {
                        data.Info += Loc.GetString("research-report-ingredient", ("AMOUNT", catalysts[ing]),
                            ("NAME", cat.LocalizedName));
                    }
                }
            }
        }
        else if (ChemicalGenClassesList["C1"].Contains(reagentID))
        {
            data.Info += Loc.GetString("research-report-element", ("NAME", reagentID)) + '\n';
        }
        else
        {
            data.Info += Loc.GetString("research-report-unable-analyze") + '\n';
            data.Completed = false;
            data.Valid = false;
        }

        if (infoOnly)
        {
            data.Completed = true;
        }
        else
        {
            if (properties.Count == 0)
            {
                data.Completed = false;
                data.Valid = false;
            }
            if (chem.Class == ReagentClass.Special && _researchdata.Clearance >= 6)
            {
                data.Completed = true;
            }
        }
        return data;
    }
    #endregion
    #region Recipe Generation
    // "complexity" is unimplemented in CM13's code
    public bool GenerateRecipe(ref GeneratedReagentData data, HashSet<string> requiredReagents)
    {
        int modifier = _random.Next(0, 101);
        switch (modifier)
        {
            case <= 60:
                modifier = 1;
                break;
            case <= 75:
                modifier = 2;
                break;
            case <= 85:
                modifier = 3;
                break;
            case <= 92:
                modifier = 4;
                break;
            case <= 97:
                modifier = 5;
                break;
            default:
                modifier = 6;
                break;
        }

        int failedAttempts = 0;
        int desiredChems = _random.Next(3, Math.Max(Math.Min(data.GenTier * 2, 4), 3) + 1);
        HashSet<string> toAdd = requiredReagents;
        for (int i = 1; i <= desiredChems; i++)
        {
            if (i >= 2)
            {
                modifier = 1;
            }

            if (toAdd.Count > 0)
            {
                foreach (var iter in toAdd)
                {
                    if (i == 1)
                    {
                        AddChemical(ref data, iter, modifier, null);
                    }
                    else
                    {
                        AddChemical(ref data, iter, 1, null);
                    }
                    toAdd.Remove(iter);
                }
            }
            else
            {
                AddChemical(ref data, string.Empty, modifier, null);
            }
            if (i == desiredChems && (IsDuplicate(ref data, out _) || IsAllMedicine(ref data)))
            {
                data.Recipe.Clear();
                if (failedAttempts > 10)
                    return false;
                i = 0;
                toAdd = requiredReagents;
                failedAttempts++;
            }
        }
        if (_random.Prob(0.2f) && data.GenTier >= 2)
        {
            AddChemical(ref data, string.Empty, 5, null, true);
        }
        // TODO: reaction indicators
        return true;
    }
    //its called addcomponent in cm13's code, obviously not going to name it that here
    public string AddChemical(ref GeneratedReagentData data, string chem, int modifier, int? tier,
        bool catalyst = false, string cClass = "")
    {
        string chemid = "";
        int mod = 1;
        int useTier = data.GenTier;

        if (modifier != 0)
            mod = modifier;
        if (tier is not null)
            useTier = tier.Value;

        for (int i = 0; i < 1; i++)
        {
            if (chem != string.Empty)
                chemid = chem;
            else if (cClass != string.Empty)
                chemid = _random.Pick(ChemicalGenClassesList["C" + cClass]);
            else
            {
                int roll = _random.Next(0, 101);
                if (useTier == 0)
                {
                    chemid = _random.Pick(ChemicalGenClassesList["C"]);
                }
                else if (useTier == 1)
                {
                    if (roll <= 60)
                        chemid = _random.Pick(ChemicalGenClassesList["C1"]);
                    else if (roll <= 80)
                        chemid = _random.Pick(ChemicalGenClassesList["C2"]);
                    else
                        chemid = _random.Pick(ChemicalGenClassesList["C1"]);
                }
                else if (useTier == 2)
                {
                    if (roll <= 50)
                        chemid = _random.Pick(ChemicalGenClassesList["C2"]);
                    else if (roll <= 75)
                        chemid = _random.Pick(ChemicalGenClassesList["C3"]);
                    else
                        chemid = _random.Pick(ChemicalGenClassesList["C4"]);
                }
                else if (useTier == 3)
                {
                    List<string> cls = new List<string> { "C1", "C2" };
                    if (roll <= 80)
                        chemid = _random.Pick(ChemicalGenClassesList[_random.Pick(cls)]);
                    else
                        chemid = _random.Pick(ChemicalGenClassesList["H1"]);
                }
                else
                {
                    if (data.Recipe.Count == 0 || catalyst)
                    {
                        if (_random.Prob(0.5f))
                            chemid = _random.Pick(ChemicalGenClassesList["C5"]);
                        else
                            chemid = _random.Pick(ChemicalGenClassesList["C4"]);
                    }
                    else if (roll <= 25)
                        chemid = _random.Pick(ChemicalGenClassesList["C2"]);
                    else if (roll <= 45)
                        chemid = _random.Pick(ChemicalGenClassesList["C3"]);
                    else if (roll <= 65)
                        chemid = _random.Pick(ChemicalGenClassesList["C4"]);
                    else
                        chemid = _random.Pick(ChemicalGenClassesList["C5"]);
                }
            }

            if (data.Recipe.Count > 0 && data.Recipe.ContainsKey(chemid))
            {
                if (chem != string.Empty)
                    return bool.FalseString;
                else
                {
                    i--;
                    continue;
                }
            }
            // catalyst check unnecessary

            (int, bool) compmod = (mod, catalyst);
            data.Recipe.TryAdd(chemid, compmod);

        }
        return chemid;
    }



    #endregion
    #region Reagent Generation
    public bool GenerateStats(ref GeneratedReagentData data, bool noProperties = false)
    {
        if (_propertiesList.Count == 0 || ChemicalGenClassesList.Count == 0)
        {
            Setup();
        }
        if (!noProperties)
        {
            int GenValue = 0;
            int propertiesBuff = _random.Next(3, 5);
            if (data.GenTier == 2)
                propertiesBuff -= 2;
            var specificProperty = "none";
            for (int i = 1; i <= data.GenTier + propertiesBuff; i++)
            {
                if (i == 1)
                {
                    if (data.GenTier > 2)
                        GenValue = AddProperty(ref data, null, null, 0, "rare");
                    else if (data.GenTier > 1 && _random.Prob((20) / 100))
                    {
                        GenValue = AddProperty(ref data, null, null, 0, "rare", true);
                        specificProperty = "negative";
                    }
                    else
                    {
                        GenValue = AddProperty(ref data, null, null, 0, "none", true);
                    }
                }
                else if (GenValue == (data.GenTier * 2) + 2) // may be different, not sure if byond follows pemdas/bodmas
                    break;
                else if (data.GenTier < 3)
                {
                    GenValue += AddProperty(ref data, null, null, data.GenTier - GenValue - 1, specificProperty, true);
                }
                else
                {
                    GenValue += AddProperty(ref data, null, null, data.GenTier - GenValue - 1, specificProperty);
                }
            }
            while (data.Effects.Count < data.GenTier + 1)
                AddProperty(ref data, null, null);
        }

        data.Overdose = 5;
        int overdoseMult = 2;
        if (data.GenTier == 1)
            overdoseMult = _random.Next(data.GenTier, overdoseMult + 1);
        if (data.GenTier == 2)
        {
            overdoseMult = 6;
            overdoseMult = _random.Next(data.GenTier + 2, overdoseMult + 1);
        }
        else if (data.GenTier >= 3)
        {
            overdoseMult = 9;
            overdoseMult = _random.Next(data.GenTier + 3, overdoseMult + 1);
        }

        for (int i = 1; i <= overdoseMult; i++)
        {
            data.Overdose += 5;
        }
        data.CriticalOverdose = data.Overdose + 5;
        for (int i = 1; i <= _random.Next(1, 4); i++)
        {
            if (_random.Prob((20 + 2 * data.GenTier) / 100))
                data.CriticalOverdose += 5;
        }
        int ired = _random.Next(0, 256);
        byte red = Convert.ToByte(ired);
        int igreen = _random.Next(0, 256);
        byte green = Convert.ToByte(igreen);
        int iblue = _random.Next(0, 256);
        byte blue = Convert.ToByte(iblue);
        Color col = Color.FromHex("#" + red.ToString("x2") + green.ToString("x2") + blue.ToString("x2"));
        data.Color = col;

        //TODO: description
        return true;
    }

    private int AddProperty(ref GeneratedReagentData data, string? myProperty, int? myLevel,
        int valueOffset = 0, string typeToAdd = "none", bool track = false, int depth = 0)
    {
        var properties = _protoMan.GetInstances<ReagentPropertyPrototype>();
        if (depth > 5)
            return 0;
        int level = 0;
        if (myLevel is not null)
            level = (int)myLevel;
        else
        {
            level = _random.Next(0, 101);
            if (level <= 20)
                level = 1;
            else if (level <= 40)
                level = 2;
            else if (level <= 60)
                level = 3;
            else if (level <= 75)
                level = 4;
            else if (level <= 80)
                level = 5;
            else if (level <= 90)
                level = 6;
            else if (level <= 95)
                level = 7;
            else
                level = 8;

            level = Math.Min(level, data.GenTier + 3);
        }

        if (myProperty is not null)
            return Convert.ToInt32(InsertProperty(ref data, myProperty, level));

        string property = string.Empty;
        int roll = _random.Next(1, 101);
        if (typeToAdd != "none")
            property = _random.Pick<string>(_propertiesList[typeToAdd]);
        else if (valueOffset > 0)
            property = _random.Pick<string>(_propertiesList["positive"]);
        else if (valueOffset < 0)
        {
            if (roll <= data.GenTier * 10)
                property = _random.Pick<string>(_propertiesList["negative"]);
            else
                property = _random.Pick<string>(_propertiesList["neutral"]);
        }
        else
        {
            switch (data.GenTier)
            {
                case 1:
                    if (roll <= 40)
                        property = _random.Pick(_propertiesList["negative"]);
                    else if (roll <= 50)
                        property = _random.Pick(_propertiesList["neutral"]);
                    else
                        property = _random.Pick(_propertiesList["positive"]);
                    break;
                case 2:
                    if (roll <= 35)
                        property = _random.Pick(_propertiesList["negative"]);
                    else if (roll <= 45)
                        property = _random.Pick(_propertiesList["neutral"]);
                    else
                        property = _random.Pick(_propertiesList["positive"]);
                    break;
                case 3:
                    if (roll <= 15)
                        property = _random.Pick(_propertiesList["negative"]);
                    else if (roll <= 25)
                        property = _random.Pick(_propertiesList["neutral"]);
                    else
                        property = _random.Pick(_propertiesList["positive"]);
                    break;
                default:
                    if (roll <= 10)
                        property = _random.Pick(_propertiesList["negative"]);
                    else if (roll <= 15)
                        property = _random.Pick(_propertiesList["neutral"]);
                    else
                        property = _random.Pick(_propertiesList["positive"]);
                    break;
            }
        }

        if (track)
        {
            int checks = 0;
            while (!CheckGeneratedProperties(property) && checks < 4)
            {
                checks++;
                if (_propertiesList["negative"].Contains(property))
                    property = _random.Pick(_propertiesList["negative"]);
                else if (_propertiesList["neutral"].Contains(property))
                    property = _random.Pick(_propertiesList["neutral"]);
                else
                    property = _random.Pick(_propertiesList["positive"]);
            }
        }

        if (properties[property].Rarity == ReagentPropertyRarityEnum.Disabled ||
            properties[property].Rarity == ReagentPropertyRarityEnum.Admin)
            return AddProperty(ref data, myProperty, myLevel, valueOffset, typeToAdd, track, depth++);
        if (level > properties[property].MaxLevel)
            level = Math.Min(level, properties[property].MaxLevel);


        var value = 0;
        if (properties[property].Hint == ReagentPropertyHintEnum.Negative)
            value = -1 * level;
        else if (properties[property].Hint == ReagentPropertyHintEnum.Neutral)
            value = (int)Math.Floor(-1f * level / 2f);
        else
            value = level;

        InsertProperty(ref data, property, level);
        return value;
    }
    #endregion
    #region Name Generation
    public void GenerateName(ref GeneratedReagentData data)
    {
        _protoMan.TryIndex(_namePrefixes, out var prefs);
        _protoMan.TryIndex(_nameMiddles, out var mids);
        _protoMan.TryIndex(_nameSuffixes, out var sufs);
        if (prefs is null || mids is null || sufs is null)
            return;
        var prefixes = prefs.Values.ToList();
        var middles = mids.Values.ToList();
        var suffixes = sufs.Values.ToList();
        string empty = string.Empty;
#if (TOOLS || DEBUG)
        empty = "ProcgenReagent";
#endif
        string genName = empty;
        while (genName == empty) //i don't like this
        {
            genName += _random.Pick<string>(prefixes);
            genName += _random.Pick<string>(middles);
            genName += _random.Pick<string>(suffixes);
            if (_protoMan.GetInstances<ReagentPrototype>().ContainsKey(genName))
            {
                genName = empty;
            }
        }
        if (ChemicalGenClassesList.ContainsKey("TAU"))
            data.ID = "TAU-" + ChemicalGenClassesList["TAU"].Count + "-" + genName;
        else
            data.ID = "TAU-" + "ERROR" + "-" + genName;
        data.Name = genName;
    }
    #endregion
    #region Helpers
    public bool InsertProperty(ref GeneratedReagentData data, string property, int level)
    {
        var props = _protoMan.GetInstances<ReagentPropertyPrototype>();
        KeyValuePair<string, int>? match = null;
        string toUse = property;
        int useLevel = level;
        foreach (var prop in data.Effects)
        {
            if (prop.Key == property)
                match = prop;
            else
            {
                //combinations
                foreach (var kvp in _unfoldedCombinations)
                {
                    if (!kvp.Value.Contains(prop.Key) || !kvp.Value.Contains(property))
                        continue;
                    int pieces = 0;
                    foreach (var idx in kvp.Value)
                    {
                        if (idx == property || data.Effects.ContainsKey(idx))
                            pieces++;
                    }
                    if (pieces >= kvp.Value.Count)
                    {
                        toUse = kvp.Key;
                        useLevel = Math.Max(Math.Max(level - prop.Value, prop.Value - level), 1);
                        foreach (var otherprop in data.Effects)
                        {
                            if (kvp.Value.Contains(otherprop.Key) && !props[otherprop.Key].
                                Category.HasFlag(ReagentPropertyTypeEnum.Catalyst))
                            {
                                data.Effects[otherprop.Key] -= useLevel;
                                if (data.Effects[otherprop.Key] <= 0)
                                    data.Effects.Remove(otherprop.Key);
                            }
                        }
                        break;
                    }
                }
                // conflicts
                foreach (var list in _unfoldedConflicts)
                {
                    if (list[0] == toUse && data.Effects.ContainsKey(list[1]))
                    {
                        match = prop;
                        break;
                    }
                    else if (data.Effects.ContainsKey(list[0]) && list[1] == toUse)
                    {
                        match = prop;
                        break;
                    }
                }
            }

            if (match is not null)
            {
                if (match.Value.Value > useLevel)
                {
                    data.Effects[match.Value.Key] -= useLevel;
                    return false;
                }
                else if (match.Value.Value < useLevel)
                {
                    useLevel -= match.Value.Value;
                    data.Effects.Remove(match.Value.Key);
                }
                else
                {
                    data.Effects.Remove(match.Value.Key);
                    return false;
                }
                break;
            }
        }
        useLevel = Math.Min(props[toUse].MaxLevel, useLevel);
        data.Effects.TryAdd(toUse, useLevel);

        if (toUse != property)
        {
            if (props[property].Category == ReagentPropertyTypeEnum.Catalyst)
                data.Effects.TryAdd(property, useLevel);
        }
        return true;
    }

    public void RelevelProperty(ref GeneratedReagentData data, string propertyName, int newLevel = 1)
    {
        if (data.Effects.ContainsKey(propertyName))
        {
            data.Effects[propertyName] = newLevel;
        }
        if (data.Effects[propertyName] == 0)
        {
            data.Effects.Remove(propertyName);
        }
    }

    public void RemoveProperty(ref GeneratedReagentData data, string propertyName)
    {
        data.Effects.Remove(propertyName);
    }
    public void MakeAlike(ref GeneratedReagentData A, GeneratedReagentData B)
    {
        A.Class = B.Class;
        A.Color = B.Color;
        A.CriticalOverdose = B.CriticalOverdose;
        A.Effects.Clear();
        foreach (var effect in B.Effects)
        {
            A.Effects.TryAdd(effect.Key, effect.Value);
        }
        A.GenTier = B.GenTier;
        A.ID = B.ID;
        A.MetabolismRate = B.MetabolismRate;
        A.ModifiedChems.Clear();
        foreach (var mod in B.ModifiedChems)
        {
            A.ModifiedChems.Add(mod);
        }
        A.Name = B.Name;
        A.OriginalID = B.OriginalID;
        A.Overdose = B.Overdose;
        A.PropertyHint = B.PropertyHint;
        A.Recipe.Clear();
        foreach (var ing in B.Recipe)
        {
            A.Recipe.TryAdd(ing.Key, ing.Value);
        }
        A.RecipeHint = B.RecipeHint;
        A.RecipeYield = B.RecipeYield;
        A.ScanPointYield = B.ScanPointYield;
        return;
    }

    public void MakeAlike(ref GeneratedReagentData A, string B)
    {
        if(!(ProceduralReagentData.TryGetValue(B, out var other) || ReagentData.TryGetValue(B, out other)))
        {
            //this is where the "fun" begins
            if (_reagent.TryIndex(B, out var bproto))
            {
                other = ConvertToGRD(bproto);
            } // yeah, okay, there's no saving it if the reagent straight up does not exist.
            else { throw new InvalidOperationException($"The Reagent \'{B}\' must exist in some capacity.");}
        }
        //A = other; //doesn't work, needs a deep copy.
        A.Class = other.Class;
        A.Color = other.Color;
        A.CriticalOverdose = other.CriticalOverdose;
        A.Effects.Clear();
        foreach(var effect in other.Effects)
        {
            A.Effects.TryAdd(effect.Key, effect.Value);
        }
        A.GenTier = other.GenTier;
        A.ID = other.ID;
        A.MetabolismRate = other.MetabolismRate;
        A.ModifiedChems.Clear();
        var metabRate = other.MetabolismRate;
        if (metabRate == 0)
            metabRate = 0.1;
        foreach(var mod in other.ModifiedChems)
        {
            A.ModifiedChems.Add(mod);
        }
        if (A.Effects.ContainsKey("Intravenous"))
        {
            metabRate *= A.Effects["Intravenous"];
        }
        if (A.Effects.ContainsKey("Hypermetabolic"))
        {
            metabRate *= ((1 + 0.25) * A.Effects["Hypermetabolic"]);
        }
        if (A.Effects.ContainsKey("Hypometabolic"))
        {
            // 0.01 is as close as you can get to 0.005 with FixedPoint2
            metabRate = (FixedPoint2)MathF.Max((float)metabRate / ((1f + 0.35f) * A.Effects["Hypometabolic"]), 0.01f);
        }
        A.Name = other.Name;
        A.OriginalID = other.OriginalID;
        A.Overdose = other.Overdose;
        A.PropertyHint = other.PropertyHint;
        A.Recipe.Clear();
        foreach(var ing in other.Recipe)
        {
            A.Recipe.TryAdd(ing.Key, ing.Value);
        }
        A.RecipeHint = other.RecipeHint;
        A.RecipeYield = other.RecipeYield;
        A.ScanPointYield = other.ScanPointYield;
        return;
    }


    private Dictionary<string, List<string>> UnfoldCombinations()
    {
        _protoMan.TryIndex(_combinations, out var combs);
        if (combs is null)
            return [];
        var vals = combs.Values.ToList();
        var dict = new Dictionary<string, List<string>>();
        foreach (var val in vals)
        {
            var sublist = val.Split(',').ToList<string>();
            string name = sublist[0];
            sublist.RemoveAt(0);
            dict.TryAdd(name, sublist);
        }
        return dict;
    }
    private void PrepareProperties()
    {
        var props = _protoMan.GetInstances<ReagentPropertyPrototype>();
        _propertiesList.TryAdd("negative", []);
        _generatedPropertiesList.TryAdd("negative", []);
        _propertiesList.TryAdd("neutral", []);
        _generatedPropertiesList.TryAdd("neutral", []);
        _propertiesList.TryAdd("positive", []);
        _generatedPropertiesList.TryAdd("positive", []);
        _propertiesList.TryAdd("rare", []);
        foreach (var prop in props)
        {
            if (prop.Value.Rarity > ReagentPropertyRarityEnum.Disabled)
            {
                if (prop.Value.Rarity == ReagentPropertyRarityEnum.Rare)
                {
                    _propertiesList["rare"].Add(prop.Value.ID);
                }
                else if (prop.Value.Hint == ReagentPropertyHintEnum.Negative)
                    _propertiesList["negative"].Add(prop.Value.ID);
                else if (prop.Value.Hint == ReagentPropertyHintEnum.Neutral)
                    _propertiesList["neutral"].Add(prop.Value.ID);
                else if (prop.Value.Hint == ReagentPropertyHintEnum.Positive)
                    _propertiesList["positive"].Add(prop.Value.ID);
            }
        }
        //yup
        foreach (var prop in props)
        {
            if (prop.Value.Hint != ReagentPropertyHintEnum.Legendary)
                continue;
            var recipe = new List<string>();
            if ((prop.Value.Rarity == ReagentPropertyRarityEnum.Legendary &&
                !prop.Value.Category.HasFlag(ReagentPropertyTypeEnum.Anomalous)) ||
                prop.Value.ID == "Ciphering")
            {
                for (int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < _legendaryCombineProperties; j++) //the hardcoding is parity :godo:
                    {
                        List<string> picks = ["neutral", "positive", "negative"];
                        string pick = _random.Pick(picks);
                        string toaddpick = _random.Pick(_propertiesList[pick]);
                        recipe.Add(toaddpick);
                    }

                    if (recipe.Count == _legendaryCombineProperties)
                    {
                        if (prop.Value.ID == "Ciphering")
                            recipe[2] = "Encrypted";
                        break;
                    }
                }
                if (recipe.Count >= 3)
                    _unfoldedCombinations.TryAdd(prop.Value.ID, recipe);
            }
        }
    }

    public bool CheckGeneratedProperties(string property)
    {
        if (_propertiesList["positive"].Contains(property))
        {
            if (_generatedPropertiesList["positive"].Contains(property) &&
                _generatedPropertiesList["positive"].Count < _propertiesList["positive"].Count)
                return false;
            _generatedPropertiesList["positive"].Add(property);
        }
        else if (_propertiesList["negative"].Contains(property))
        {
            if (_generatedPropertiesList["negative"].Contains(property) &&
                _generatedPropertiesList["negative"].Count < _propertiesList["negative"].Count)
                return false;
            _generatedPropertiesList["negative"].Add(property);
        }
        else if (_propertiesList["neutral"].Contains(property))
        {
            if (_generatedPropertiesList["neutral"].Contains(property) &&
                _generatedPropertiesList["neutral"].Count < _propertiesList["neutral"].Count)
                return false;
            _generatedPropertiesList["neutral"].Add(property);
        }
        return true;
    }
    public void RetroactiveLockdown(GeneratedReagentData data)
    {
        HashSet<string> lockeddown = [];
        if (data.OriginalID == string.Empty)
        {
            lockeddown.Add(data.ID);
            RaiseNetworkEvent(new RetroactiveLockdownEvent(lockeddown));
            RaiseLocalEvent(new RetroactiveLockdownEvent(lockeddown));
            return;
        }
        var parentChem = ProceduralReagentData.ContainsKey(data.OriginalID) ? ProceduralReagentData[data.OriginalID] :
            ReagentData.ContainsKey(data.OriginalID) ? ReagentData[data.OriginalID] : data;
        lockeddown.Add(parentChem.ID);
        foreach (var chem in parentChem.ModifiedChems)
        {
            lockeddown.Add(chem);
        }
        lockeddown.Add(data.ID); //just in case
        RaiseNetworkEvent(new RetroactiveLockdownEvent(lockeddown));
        RaiseLocalEvent(new RetroactiveLockdownEvent(lockeddown));
    }

    public bool GetReagentData(string id, [NotNullWhen(true)] out GeneratedReagentData? data)
    {
        if (ProceduralReagentData.TryGetValue(id, out var prd))
        {
            data = prd;
            return true;
        }
        if (ReagentData.TryGetValue(id, out var rd))
        {
            data = rd;
            return true;
        }
        if (_reagent.TryIndex(id, out var reagent))
        {
            data = ConvertToGRD(reagent);
            ReagentData.TryAdd(id, data.Value);
            return true;
        }
        data = null;
        return false;
    }

    public bool IsAllMedicine(ref GeneratedReagentData data)
    {
        var reagents = _protoMan.GetInstances<ReagentPrototype>();
        foreach (var ingredient in data.Recipe)
        {
            if (!reagents[ingredient.Key].Flags.HasFlag(ReagentFlags.Medical))
                return false;
        }
        return true;
    }

    #endregion
}
