//pretty please license this under the MIT license :) - MACMAN2003
using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Chemistry.Reagents;

[Serializable, NetSerializable]
public struct GeneratedReagentData
{
    [ViewVariables(VVAccess.ReadOnly)]
    public string ID;
    [ViewVariables(VVAccess.ReadOnly)]
    public string Name;
    /// <summary>
    /// the properties of the chemical, e.g. ("hemorrhaging", 3) being hemorrhaging level 3
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, int> Effects;
    /// <summary>
    /// the ingredients of the chemical, the int is the amount needed and the bool is for if it's a catalyst
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, (int, bool)> Recipe;
    /// <summary>
    /// how much of the chemial do you get when you make it
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public int RecipeYield;
    /// <summary>
    /// how many research credits you get when you scan the chemical in the XRF
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public int ScanPointYield;
    [ViewVariables(VVAccess.ReadOnly)]
    public Color Color;
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 Overdose;
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 CriticalOverdose;
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 MetabolismRate;
    /// <summary>
    /// easy intermediate hard or a secret fourth and fifth thing
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public int GenTier;
    /// <summary>
    /// should be one of the ingredients
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public string RecipeHint;
    /// <summary>
    /// should be one of the properties
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public string PropertyHint;
    /// <summary>
    /// if this is not string.Empty then it is a modified chemical
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public string OriginalID;
    /// <summary>
    /// if this is not null then this is all of its "children" chemicals that are based off of it
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<string> ModifiedChems;
    [ViewVariables(VVAccess.ReadOnly)]
    public ReagentClass Class;
    // TODO: effects on mix
    public GeneratedReagentData()
    {
        ID = string.Empty;
        Name = string.Empty;
        Effects = [];
        Recipe = [];
        RecipeHint = string.Empty;
        RecipeYield = 1;
        ScanPointYield = 2;
        Color = Color.Black;
        Overdose = 30;
        CriticalOverdose = 50;
        MetabolismRate = 0.1;
        GenTier = 1;
        Class = ReagentClass.None;
        PropertyHint = string.Empty;
        OriginalID = string.Empty;
        ModifiedChems = [];
    }
    
}

[Serializable, NetSerializable]
public struct ResearchReportData
{
    public string Name;
    public string Info;
    public bool Completed;
    public bool Valid;
    public ResearchReportIconEnum Icon;
}

[Serializable, NetSerializable]
public enum ResearchReportIconEnum
{
    None,
    Full,
    Partial,
    Synthesis
}
