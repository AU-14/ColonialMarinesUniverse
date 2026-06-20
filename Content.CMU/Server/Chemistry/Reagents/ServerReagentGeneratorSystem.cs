using Content.Server.GameTicking;
using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Dataset;
using Content.Shared.GameTicking;
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

    private static readonly ProtoId<DatasetPrototype> _namePrefixes = "CMURandChemPrefix";
    private static readonly ProtoId<DatasetPrototype> _nameMiddles = "CMURandChemWordroot";
    private static readonly ProtoId<DatasetPrototype> _nameSuffixes = "CMURandChemSuffix";
    private static readonly ProtoId<DatasetPrototype> _conflicts = "CMUConflictingProperties";
    private static readonly ProtoId<DatasetPrototype> _combinations = "CMUCombiningProperties";
    [ViewVariables(VVAccess.ReadOnly)]
    private HashSet<string> _generatedReagents = [];
    private Dictionary<string, GeneratedReagentData> _generatedReagentData = [];
    [ViewVariables(VVAccess.ReadOnly)]
    private HashSet<string> _generatedRecipes = [];
    private ISawmill _sawmill = default!;
    private HashSet<ReagentPropertyPrototype> _knownProperties = [];
    private HashSet<ReagentPropertyPrototype> _generatedProperties = [];
    private HashSet<ReagentPrototype> _scannedReagents = [];
    private List<List<string>> _unfoldedConflicts = [];
    private Dictionary<string, List<string>> _unfoldedCombinations = [];
    private Dictionary<string, HashSet<string>> _propertiesList = [];
    public Dictionary<string, HashSet<string>> Properties { get => _propertiesList; }
    private Dictionary<string, HashSet<string>> _generatedPropertiesList = [];
    public Dictionary<string, HashSet<string>> GeneratedProperties { get => _generatedPropertiesList; }
    private Dictionary<string, HashSet<string>> _chemicalGenClassesList = [];
    public Dictionary<string, HashSet<string>> GenClasses { get => _chemicalGenClassesList; }

    private int _legendaryCombineProperties = 3;
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
        var ev = new GeneratedReagentDataEvent(_generatedReagentData);
        RaiseNetworkEvent(ev, args.Channel);
    }

    private void OnCleanup(RoundRestartCleanupEvent args)
    {
        if (_generatedRecipes.Count > 0)
        {
            _sawmill.Info("Clearing procedural reagent recipes.");
            foreach (var recipe in _generatedRecipes)
            {
                _protoMan.TryDelete<ReactionPrototype>(recipe);
                _generatedRecipes.Remove(recipe);
            }
            DebugTools.Assert(_generatedRecipes.Count == 0);
        }
        if (_generatedReagents.Count > 0)
        {
            _sawmill.Info("Clearing procedural reagents.");
            foreach (var reagent in _generatedReagents)
            {
                _protoMan.TryDelete<ReagentPrototype>(reagent);
                _generatedReagents.Remove(reagent);
            }
            DebugTools.Assert(_generatedReagents.Count == 0);
        }
        _generatedReagentData.Clear();
        _knownProperties.Clear();
        var props = _protoMan.EnumeratePrototypes<ReagentPropertyPrototype>();

        foreach (var prop in props)
        {
            if (prop.Starter)
            {
                _knownProperties.Add(prop);
            }
        }
        _generatedProperties.Clear();
        _scannedReagents.Clear();
        _propertiesList.Clear();
        _chemicalGenClassesList.Clear();
        _generatedPropertiesList.Clear();
        _unfoldedCombinations.Clear();
        _unfoldedConflicts.Clear();
    }

    private void PreMapLoad(LoadingMapsEvent args)
    {
        //CreateEvilDex();
        _unfoldedConflicts = UnfoldConflicts();
        _unfoldedCombinations = UnfoldCombinations();
        PrepareProperties();
        PrepareChems();
    }

    public void LegalizeChem(GeneratedReagentData chem)
    {

    }


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
            if (i == desiredChems && (IsDuplicate(ref data) || IsAllMedicine(ref data)))
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
    private string AddChemical(ref GeneratedReagentData data, string chem, int modifier, int? tier,
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
                chemid = _random.Pick(_chemicalGenClassesList["C" + cClass]);
            else
            {
                int roll = _random.Next(0, 101);
                if (useTier == 0)
                {
                    chemid = _random.Pick(_chemicalGenClassesList["C"]);
                }
                else if (useTier == 1)
                {
                    if (roll <= 60)
                        chemid = _random.Pick(_chemicalGenClassesList["C1"]);
                    else if (roll <= 80)
                        chemid = _random.Pick(_chemicalGenClassesList["C2"]);
                    else
                        chemid = _random.Pick(_chemicalGenClassesList["C1"]);
                }
                else if (useTier == 2)
                {
                    if (roll <= 50)
                        chemid = _random.Pick(_chemicalGenClassesList["C2"]);
                    else if (roll <= 75)
                        chemid = _random.Pick(_chemicalGenClassesList["C3"]);
                    else
                        chemid = _random.Pick(_chemicalGenClassesList["C4"]);
                }
                else if (useTier == 3)
                {
                    List<string> cls = new List<string> { "C1", "C2" };
                    if (roll <= 80)
                        chemid = _random.Pick(_chemicalGenClassesList[_random.Pick(cls)]);
                    else
                        chemid = _random.Pick(_chemicalGenClassesList["H1"]);
                }
                else
                {
                    if (data.Recipe.Count == 0 || catalyst)
                    {
                        if (_random.Prob(0.5f))
                            chemid = _random.Pick(_chemicalGenClassesList["C5"]);
                        else
                            chemid = _random.Pick(_chemicalGenClassesList["C4"]);
                    }
                    else if (roll <= 25)
                        chemid = _random.Pick(_chemicalGenClassesList["C2"]);
                    else if (roll <= 45)
                        chemid = _random.Pick(_chemicalGenClassesList["C3"]);
                    else if (roll <= 65)
                        chemid = _random.Pick(_chemicalGenClassesList["C4"]);
                    else
                        chemid = _random.Pick(_chemicalGenClassesList["C5"]);
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
            data.Recipe.Add(chemid, compmod);

        }
        return chemid;
    }



    #endregion
    #region Reagent Generation
    public bool GenerateStats(ref GeneratedReagentData data, bool noProperties = false)
    {
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
        Color col = Color.FromHex("#" + red.ToString("x") + green.ToString("x") + blue.ToString("x"));
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
        data.ID = "TAU-" + _chemicalGenClassesList["TAU"].Count + "-" + genName;
        data.Name = genName;
    }
    #endregion
    #region Helpers
    private bool InsertProperty(ref GeneratedReagentData data, string property, int level)
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
                        if (idx == prop.Key || data.Effects.ContainsKey(idx))
                            pieces++;
                    }
                    if (pieces >= kvp.Value.Count())
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
        data.Effects.Add(toUse, useLevel);

        if (toUse != property)
        {
            if (props[property].Category == ReagentPropertyTypeEnum.Catalyst)
                data.Effects.Add(property, useLevel);
        }
        return true;
    }

    //"unfold" :kekw:
    //thank GOD robust toolbox is not in C or C++
    private List<List<string>> UnfoldConflicts()
    {
        _protoMan.TryIndex(_conflicts, out var confs);
        if (confs is null)
            return [];
        var vals = confs.Values.ToList();
        var list = new List<List<string>>();
        foreach(var val in vals)
        {
            var sublist = val.Split(',').ToList<string>();
            list.Add(sublist);
        }
        return list;
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
            dict.Add(name, sublist);
        }
        return dict;
    }
    private void PrepareProperties()
    {
        var props = _protoMan.GetInstances<ReagentPropertyPrototype>();
        _propertiesList.Add("negative", []);
        _generatedPropertiesList.Add("negative", []);
        _propertiesList.Add("neutral", []);
        _generatedPropertiesList.Add("neutral", []);
        _propertiesList.Add("positive", []);
        _generatedPropertiesList.Add("positive", []);
        _propertiesList.Add("rare", []);
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

                    if (recipe.Count() == _legendaryCombineProperties)
                    {
                        if (prop.Value.ID == "Ciphering")
                            recipe[2] = "Encrypted";
                        break;
                    }
                }
                if (recipe.Count() >= 3)
                    _unfoldedCombinations.Add(prop.Value.ID, recipe);
            }
        }
    }

    private bool CheckGeneratedProperties(string property)
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


    private void PrepareChems()
    {
        var chems = _protoMan.GetInstances<ReagentPrototype>();
        _chemicalGenClassesList.Add("C", []);
        _chemicalGenClassesList.Add("C1", []);
        _chemicalGenClassesList.Add("C2", []);
        _chemicalGenClassesList.Add("C3", []);
        _chemicalGenClassesList.Add("C4", []);
        _chemicalGenClassesList.Add("C5", []);
        _chemicalGenClassesList.Add("C6", []);
        _chemicalGenClassesList.Add("H1", []);
        //_chemicalGenClassesList.Add("T1", []);
        //_chemicalGenClassesList.Add("T2", []);
        //_chemicalGenClassesList.Add("T3", []);
        //_chemicalGenClassesList.Add("T4", []);
        //_chemicalGenClassesList.Add("T5", []);
        _chemicalGenClassesList.Add("TAU", []);
        foreach (var chem in chems)
        {
            if (chem.Value.Flags.HasFlag(ReagentFlags.NoGeneration))
                continue;
            switch (chem.Value.Class)
            {
                case ReagentClass.Basic:
                    _chemicalGenClassesList["C1"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Common:
                    _chemicalGenClassesList["C2"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Uncommon:
                    _chemicalGenClassesList["C3"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Rare:
                    _chemicalGenClassesList["C4"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Special:
                    _chemicalGenClassesList["C5"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Ultra:
                    _chemicalGenClassesList["C6"].Add(chem.Value.ID);
                    break;
                case ReagentClass.Hydro:
                    _chemicalGenClassesList["H1"].Add(chem.Value.ID);
                    break;
                default:
                    break;
            }
            if (chem.Value.Class != ReagentClass.None)
            {
                _chemicalGenClassesList["C"].Add(chem.Value.ID);
            }
        }
    }

    private bool IsDuplicate(ref GeneratedReagentData data)
    {
        var reactions = _protoMan.GetInstances<ReactionPrototype>();
        //this fucking sucks
        foreach (var reaction in reactions)
        {
            int matches = 0;
            foreach (var ingredient in reaction.Value.Reactants)
            {
                if (data.Recipe.ContainsKey(ingredient.Key))
                    matches++;
                if (matches >= reaction.Value.Reactants.Count)
                    return true;
            }
        }
        return false;
    }
    private bool IsAllMedicine(ref GeneratedReagentData data)
    {
        var reagents = _protoMan.GetInstances<ReagentPrototype>();
        foreach (var ingredient in data.Recipe)
        {
            if (!reagents[ingredient.Key].Flags.HasFlag(ReagentFlags.Medical))
                return false;
        }
        return true;
    }
    private void CreateEvilDex()
    {
        var evildex = new GeneratedReagentData();
        evildex.Name = "Evil Dexalin";
        evildex.ID = "Evildex";
        Dictionary<string, int> effects = [];
        var propes = _protoMan.GetInstances<ReagentPropertyPrototype>();
        var regs = _protoMan.GetInstances<ReagentPrototype>();
        var bio = "Biocidic";
        var carc = "Carcinogenic";
        var hem = "Hemorrhaging";
        effects.Add(bio, 5);
        effects.Add(carc, 3);
        effects.Add(hem, 4);
        evildex.Effects = effects;
        evildex.Color = Color.Chartreuse;
        evildex.CriticalOverdose = 10;
        evildex.Overdose = 7;
        evildex.MetabolismRate = 0.1;
        evildex.Difficulty = 3;
        evildex.ScanPointYield = 2;
        evildex.RecipeYield = 1;
        Dictionary<string, (int, bool)> recip = [];
        recip.Add("CMDexalin", (1, false));
        recip.Add("RMCIron", (1, false));
        recip.Add("RMCCarbon", (1, false));
        recip.Add("RMCRadium", (5, true));
        evildex.Recipe = recip;
        _generatedReagentData.Add(evildex.ID, evildex);
        var ev = new GenerateReagentEvent(evildex);

        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
    }





    #endregion
}
