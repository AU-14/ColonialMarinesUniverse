using Content.Shared._AU14.Chemistry.Reagents;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent, AutoGenerateComponentState, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class ChemSimulatorComponent : Component
{
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public ChemSimulatorMode Mode = ChemSimulatorMode.Amplify;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public ChemSimulatorStage Stage = ChemSimulatorStage.Off;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public bool Ready = false;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public string StatusBar = string.Empty;
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public bool Override = false;
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, int> PropertyCosts = [];
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public string? TargetProperty;
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public string? ReferenceProperty;
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public string? PickedRecipeChem;
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public List<(string,int, bool, bool)> RecipeChemOptions = []; //i hate this too
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public int? SimulationCost = null;
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public int? Overdose = null;
    [AutoNetworkedField, AutoPausedField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextProcess = TimeSpan.Zero;
    [AutoNetworkedField, DataField, ViewVariables(VVAccess.ReadOnly)]
    public float SecondsPerProcess = 1.5f;
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public GeneratedReagentData? ChemCache = null;
    [AutoNetworkedField]
    public float InsertTimeRemaining = 0f;
    [AutoNetworkedField]
    public float InsertTime = 1f;
    [AutoNetworkedField]
    public float PrintTimeRemaining = 0f;
    [AutoNetworkedField]
    public float PrintTime = 1f;
}
[Serializable, NetSerializable]
public enum ChemSimulatorMode
{
    Amplify = 1,
    Suppress = 2,
    Relate = 3,
    Add = 4
}

[Serializable, NetSerializable]
public enum ChemSimulatorStage
{
    Failure = -1,
    Off = 0,
    Final = 1,
    Wait = 2,
    Stage3 = 3,
    Stage4 = 4,
    Begin = 5
}

[Serializable, NetSerializable]
public enum ChemSimulatorVisuals
{
    Sim,
}

[Serializable, NetSerializable]
public enum ChemSimulatorVisState
{
    Normal,
    Off,
    Reading,
    Running,
    Ready,
    Printing
}
