namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// Marks a counter objective ("murder N people") that <see cref="EWJoeObjectiveSystem"/> increments
/// when a kill resolves to the Early Warning Joe. Pair with CounterCondition and NumberObjective.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeKillCounterComponent : Component;
