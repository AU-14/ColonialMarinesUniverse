using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Monitor;

[Serializable, NetSerializable]
public enum CMUPatientMonitorUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CMUMonitorRhythm : byte
{
    /// <summary>Leads off.</summary>
    None,
    Sinus,
    SinusTachycardia,
    SinusBradycardia,
    Irregular,
    VentricularFibrillation,
    Asystole,
}

[Serializable, NetSerializable]
public enum CMUMonitorPatientStatus : byte
{
    None,
    Conscious,
    Unresponsive,
    Pulseless,
    Dead,
}

[Serializable, NetSerializable]
public readonly record struct CMUMonitorLogEntry(string Time, string Text, bool Critical);

[Serializable, NetSerializable]
public sealed class CMUPatientMonitorBuiState : BoundUserInterfaceState
{
    public string? PatientName;
    public CMUMonitorPatientStatus Status;
    public CMUMonitorRhythm Rhythm;

    /// <summary>Beats per minute; 0 with no pulse.</summary>
    public int HeartRate;

    /// <summary>Oxygen saturation in percent, or null when there's no pleth to read.</summary>
    public int? SpO2;

    public int RespiratoryRate;

    /// <summary>End-tidal CO2 in mmHg.</summary>
    public int EtCO2;

    public float? TemperatureC;

    /// <summary>The heart's damage stage, like the health analyzer shows. Null with no heart.</summary>
    public OrganDamageStage? HeartStage;
    public int HeartCurrent;
    public int HeartMax;
    public int? NibpSystolic;
    public int? NibpDiastolic;
    public string? NibpTime;

    /// <summary>Time since the leads were attached.</summary>
    public string Elapsed = string.Empty;

    public string Clock = string.Empty;
    public int Joules;
    public bool Armed;

    /// <summary>Battery charge in percent, or null with no battery.</summary>
    public int? Battery;

    public bool AlarmSilenced;
    public bool Analyzing;

    /// <summary>Switched off, the screen is dark even with leads on.</summary>
    public bool PoweredOn;
    public bool LeadsAttached;
    public bool Charging;
    public bool Charged;
    public List<CMUMonitorLogEntry> Log = new();
}

[Serializable, NetSerializable]
public sealed class CMUPatientMonitorAnalyzeMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUPatientMonitorChargeMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUPatientMonitorShockMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUPatientMonitorEnergyMsg(bool raise) : BoundUserInterfaceMessage
{
    public readonly bool Raise = raise;
}

[Serializable, NetSerializable]
public sealed class CMUPatientMonitorNibpMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUPatientMonitorSilenceMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUPatientMonitorDisconnectMsg : BoundUserInterfaceMessage;

public static class CMUMonitorRhythms
{
    public static string LocId(CMUMonitorRhythm rhythm)
    {
        return rhythm switch
        {
            CMUMonitorRhythm.Sinus => "cmu-monitor-rhythm-sinus",
            CMUMonitorRhythm.SinusTachycardia => "cmu-monitor-rhythm-tachycardia",
            CMUMonitorRhythm.SinusBradycardia => "cmu-monitor-rhythm-bradycardia",
            CMUMonitorRhythm.Irregular => "cmu-monitor-rhythm-irregular",
            CMUMonitorRhythm.VentricularFibrillation => "cmu-monitor-rhythm-vfib",
            CMUMonitorRhythm.Asystole => "cmu-monitor-rhythm-asystole",
            _ => "cmu-monitor-rhythm-none",
        };
    }

    public static bool IsLethal(CMUMonitorRhythm rhythm)
    {
        return rhythm is CMUMonitorRhythm.VentricularFibrillation or CMUMonitorRhythm.Asystole;
    }
}
