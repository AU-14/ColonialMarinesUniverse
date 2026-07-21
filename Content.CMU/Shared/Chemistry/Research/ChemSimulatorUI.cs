using Content.Shared._AU14.Chemistry.Reagents;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[Serializable, NetSerializable]
public enum ChemSimulatorUI
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ChemSimulatorPickTargetPropertyBuiMsg(string prop) : BoundUserInterfaceMessage
{
    public readonly string Property = prop;
}
[Serializable, NetSerializable]
public sealed class ChemSimulatorPickReferencePropertyBuiMsg(string prop) : BoundUserInterfaceMessage
{
    public readonly string Property = prop;
}
[Serializable, NetSerializable]
public sealed class ChemSimulatorPickModeBuiMsg(ChemSimulatorMode mode) : BoundUserInterfaceMessage
{
    public readonly ChemSimulatorMode Mode = mode;
}
[Serializable, NetSerializable]
public sealed class ChemSimulatorAttemptSimulateBuiMsg() : BoundUserInterfaceMessage;
[Serializable, NetSerializable]
public sealed class ChemSimulatorToggleOverrideBuiMsg() : BoundUserInterfaceMessage;
[Serializable, NetSerializable]
public sealed class ChemSimulatorEjectBuiMsg(bool reference, NetEntity? player) : BoundUserInterfaceMessage
{
    public readonly bool Reference = reference;
    public readonly NetEntity? Player = player;
}
[Serializable, NetSerializable]
public sealed class ChemSimulatorPickRecipeChemBuiMsg(string pick) : BoundUserInterfaceMessage
{
    public readonly string Pick = pick;
}
[Serializable, NetSerializable]
public sealed class ChemSimulatorFinalizeBuiMsg() : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ChemSimulatorBuiState(
    GeneratedReagentData? target,
    GeneratedReagentData? reference,
    ChemSimulatorMode mode,
    ChemSimulatorStage stage,
    Dictionary<string, int> costs,
    bool ready,
    bool ovrride,
    string statusBar,
    string? targprop,
    string? refprop,
    int credits,
    int? overdose,
    string? recipePicked,
    List<(string,int, bool, bool)> recipeOptions,
    int? cost
    ) : BoundUserInterfaceState
{
    public readonly GeneratedReagentData? Target = target;
    public readonly GeneratedReagentData? Reference = reference;
    public readonly ChemSimulatorMode Mode = mode;
    public readonly ChemSimulatorStage Stage = stage;
    public readonly Dictionary<string, int> Costs = costs;
    public readonly bool Ready = ready;
    public readonly bool Override = ovrride;
    public readonly string StatusBar = statusBar;
    public readonly string? TargetProp = targprop;
    public readonly string? ReferenceProp = refprop;
    public readonly int Credits = credits;
    public readonly int? Overdose = overdose;
    public readonly string? RecipePicked = recipePicked;
    public readonly List<(string,int, bool, bool)> RecipeOptions = recipeOptions;
    public readonly int? Cost = cost;
}

