namespace Content.Server.CMU14.Fob;

/// <summary>Round-local observations, never inputs to gameplay or evacuation decisions.</summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(CMUFobReserveSystem))]
public sealed partial class CMUFobTrialComponent : Component
{
    [ViewVariables, AutoPausedField] public TimeSpan LastAccountedAt;
    [ViewVariables, AutoPausedField] public TimeSpan NextSummaryAt;
    [ViewVariables] public string Site = string.Empty;
    [ViewVariables] public int Offers;
    [ViewVariables] public int Completions;
    [ViewVariables] public int Expiries;
    [ViewVariables] public int Activations;
    [ViewVariables] public int Depletions;
    [ViewVariables] public double AwardedSeconds;
    [ViewVariables] public double CappedSeconds;
    [ViewVariables] public double StartupSeconds;
    [ViewVariables] public double ActiveSeconds;
    [ViewVariables] public double ControlLossChargeSeconds;
    [ViewVariables] public int Shots;
    [ViewVariables] public int GunBreaks;
    [ViewVariables] public double GunDamage;
    [ViewVariables] public int Departures;
    [ViewVariables] public int Landings;
    [ViewVariables] public int WarningMask;
    [ViewVariables] public int WarningCount;
    [ViewVariables] public bool Finalized;
}

[ByRefEvent]
public readonly record struct CMUFobMarkersChangedEvent;

/// <summary>Local warning observation, also used to verify threshold and comms behavior.</summary>
[ByRefEvent]
public readonly record struct CMUFobWarningEvent(int Seconds);
