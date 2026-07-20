using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Reagents;

//WARNING: SEVERE EVENTSLOP AHEAD, PROCEED WITH CAUTION

/// <summary>
/// This is for telling the reagent generator to generate a specific reagent. Ideally this should be raised as both
/// a LocalEvent and a NetEvent on the server. Don't raise this on the client unless you REALLY want to mess things up.
/// </summary>
/// <param name="reagent"></param>
[Serializable, NetSerializable]
public sealed class GenerateReagentEvent(GeneratedReagentData reagent) : EntityEventArgs
{
    public GeneratedReagentData Reagent = reagent;
}
/// <summary>
/// This is for sending already generated reagent data to newly connected clients.
/// </summary>
/// <param name="Reagents"></param>

[Serializable, NetSerializable]
public sealed class SendReagentDataEvent(
    Dictionary<string, GeneratedReagentData> Reagents,
    HashSet<string> lockedDownChems,
    HashSet<string> knownProperties,
    Dictionary<string, int> identifiedChemicals,
    Dictionary<string,HashSet<string>> chemicalList,
    Dictionary<string, List<string>> combinations,
    List<List<string>> conflicts) : EntityEventArgs
{
    public Dictionary<string, GeneratedReagentData> Reagents = Reagents;
    public HashSet<string> LockedDownChems = lockedDownChems;
    public HashSet<string> KnownProperties = knownProperties;
    public Dictionary<string, int> IdentifiedChemicals = identifiedChemicals;
    public Dictionary<string, HashSet<string>> ChemicalList = chemicalList;
    public Dictionary<string, List<string>> Combinations = combinations;
    public List<List<string>> Conflicts = conflicts;
}
[Serializable, NetSerializable]
public sealed class RequestReagentGenerationEvent(GeneratedReagentData reagent) : CancellableEntityEventArgs
{
    public GeneratedReagentData Reagent = reagent;
}
/// <summary>
/// Tells the clientside research terminals what to display
/// </summary>
/// <param name="reagents"></param>
/// <param name="nextUpdate"></param>
[Serializable, NetSerializable]
public sealed class UpdateResearchConsoleEvent(List<GeneratedReagentData> reagents, TimeSpan nextUpdate) : EntityEventArgs
{
    public List<GeneratedReagentData> Reagents = reagents;
    public TimeSpan NextUpdate = nextUpdate;
}
[Serializable, NetSerializable]
public sealed class UpdateDataTerminalClearanceEvent(int clearance, int credits) : EntityEventArgs
{
    public int Clearance = clearance;
    public int Credits = credits;
}

[Serializable, NetSerializable]
public sealed class SyncKnownPropertiesEvent(HashSet<string> knownProperties) : EntityEventArgs
{
    public HashSet<string> KnownProperties = knownProperties;
}
[Serializable, NetSerializable]
public sealed class IdentifyChemicalEvent(string chem, int reward) : EntityEventArgs
{
    public string Chem = chem;
    public int Reward = reward;
}
[Serializable, NetSerializable]
public sealed class SyncGenClassesEvent(Dictionary<string,HashSet<string>> chemList) : EntityEventArgs
{
    public Dictionary<string, HashSet<string>> ChemList = chemList;
}


[Serializable, NetSerializable]
public sealed class ChemicalIdentifiedEvent(string id, int reward) : EntityEventArgs
{
    public string ID = id;
    public int Reward = reward;
}

[Serializable, NetSerializable]
public sealed class TerminalPickReagentEvent(string reagent) : EntityEventArgs
{
    public string Reagent = reagent;
}
[Serializable, NetSerializable]
public sealed class XRFScannedReagentEvent(string reagent, int samplenum, NetEntity scanner) : EntityEventArgs
{
    public string Reagent = reagent;
    public int SampleNum = samplenum;
    public NetEntity Scanner = scanner;
}

[Serializable, NetSerializable]
public sealed class XRFSpawnReportEvent(NetEntity scanner, bool result, string reason, string reagentID)
{
    public NetEntity Scanner = scanner;
    public bool Result = result;
    public string Reason = reason;
    public string ReagentID = reagentID;
}

public sealed class BeginChemSimulatorProcessEvent() : EntityEventArgs
{
}

public sealed class FinalizeChemSimulatorEvent() : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class RetroactiveLockdownEvent(HashSet<string> chems) : EntityEventArgs
{
    public HashSet<string> Chems = chems;
}

[Serializable, NetSerializable]
public sealed partial class DDIDiscoveredEvent() : EntityEventArgs
{

}

[Serializable, NetSerializable]
public sealed partial class XRFDoAfterEvent() : SimpleDoAfterEvent
{
}
