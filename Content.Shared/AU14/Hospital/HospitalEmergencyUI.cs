using Robust.Shared.Serialization;

namespace Content.Shared.AU14.Hospital;

[Serializable, NetSerializable]
public enum HospitalEmergencyComputerUi
{
    Key,
}

[Serializable, NetSerializable]
public sealed class HospitalEmergencyComputerBuiState : BoundUserInterfaceState
{
    public string StatusText { get; }
    public string IncidentReport { get; }
    public int Casualties { get; }
    public int Severity { get; }
    public int Reward { get; }
    public int ActivePatients { get; }
    public int FullyHealedPatients { get; }
    public int LastPayout { get; }
    public int LastMissedInjuries { get; }
    public int LastVipPenalty { get; }
    public int LastPermanentDeathPenalty { get; }
    public int SecondsRemaining { get; }
    public bool HasLandingZone { get; }
    public bool CanApproveLanding { get; }
    public bool CanRequestPickup { get; }
    public bool CanReleaseShuttle { get; }

    public HospitalEmergencyComputerBuiState(
        string statusText,
        string incidentReport,
        int casualties,
        int severity,
        int reward,
        int activePatients,
        int fullyHealedPatients,
        int lastPayout,
        int lastMissedInjuries,
        int lastVipPenalty,
        int lastPermanentDeathPenalty,
        int secondsRemaining,
        bool hasLandingZone,
        bool canApproveLanding,
        bool canRequestPickup,
        bool canReleaseShuttle)
    {
        StatusText = statusText;
        IncidentReport = incidentReport;
        Casualties = casualties;
        Severity = severity;
        Reward = reward;
        ActivePatients = activePatients;
        FullyHealedPatients = fullyHealedPatients;
        LastPayout = lastPayout;
        LastMissedInjuries = lastMissedInjuries;
        LastVipPenalty = lastVipPenalty;
        LastPermanentDeathPenalty = lastPermanentDeathPenalty;
        SecondsRemaining = secondsRemaining;
        HasLandingZone = hasLandingZone;
        CanApproveLanding = canApproveLanding;
        CanRequestPickup = canRequestPickup;
        CanReleaseShuttle = canReleaseShuttle;
    }
}

[Serializable, NetSerializable]
public sealed class HospitalEmergencyApproveLandingMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class HospitalEmergencyRequestPickupMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class HospitalEmergencyReleaseShuttleMsg : BoundUserInterfaceMessage;
