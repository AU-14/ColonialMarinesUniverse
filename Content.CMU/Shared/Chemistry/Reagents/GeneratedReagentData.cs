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
    public string ID;
    public string Name;
    public Dictionary<string, int> Effects;
    public Dictionary<string, (int, bool)> Recipe;
    public int RecipeYield;
    public int ScanPointYield;
    public int Difficulty;
    public Color Color;
    public FixedPoint2 Overdose;
    public FixedPoint2 CriticalOverdose;
    public FixedPoint2 MetabolismRate;
    public int GenTier;
    // TODO: effects on mix

}
