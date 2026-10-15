using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Monitor;

/// <summary>
/// A defibrillator that doubles as a patient monitor. Set it down near a patient and drag it onto them to attach
/// the leads, like an IV stand; its screen then shows their ECG, vitals and a code summary of shocks delivered.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPatientMonitorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? AttachedTo;

    /// <summary>
    /// How far the leads reach before they pull off.
    /// </summary>
    [DataField]
    public float Range = 2f;

    [DataField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> SkillRequired = new() { ["RMCSkillMedical"] = 1 };

    /// <summary>
    /// Heart rate and rhythm shown on the screen, networked so each client can beep along with the QRS.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DisplayHeartRate;

    [DataField, AutoNetworkedField]
    public CMUMonitorRhythm DisplayRhythm = CMUMonitorRhythm.None;

    [DataField]
    public SoundSpecifier AttachSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");

    /// <summary>
    /// The QRS beep, played on the client once per beat.
    /// </summary>
    [DataField]
    public SoundSpecifier? BeatSound;

    /// <summary>
    /// Repeats while the patient is in ventricular fibrillation.
    /// </summary>
    [DataField]
    public SoundSpecifier? LethalAlarmSound;

    [DataField]
    public TimeSpan LethalAlarmInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The continuous flatline tone, looped while the patient is in asystole.
    /// </summary>
    [DataField]
    public SoundSpecifier? FlatlineSound;

    /// <summary>
    /// Repeats for low oxygen saturation or blood pressure.
    /// </summary>
    [DataField]
    public SoundSpecifier? WarningAlarmSound;

    [DataField]
    public TimeSpan WarningAlarmInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long an analysis takes after "analyzing now".
    /// </summary>
    [DataField]
    public TimeSpan AnalyzeDuration = TimeSpan.FromSeconds(3);


    [DataField]
    public TimeSpan NibpInterval = TimeSpan.FromMinutes(2);

    // Everything below is only tracked by the server and sent through the UI state.

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan NextAlarm;

    /// <summary>
    /// Alarms, and the heartbeat beep, stay off until someone turns them back on. Clients need it for the beep.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AlarmsSilenced;

    [ViewVariables]
    public EntityUid? FlatlineStream;

    /// <summary>
    /// The alarm currently sounding, so a higher priority one can cut it off.
    /// </summary>
    [ViewVariables]
    public EntityUid? AlarmStream;

    [ViewVariables]
    public bool AlarmStreamLethal;

    /// <summary>
    /// Alarms hold off until the unit has finished speaking, shocking or chiming.
    /// </summary>
    [ViewVariables]
    public TimeSpan AlarmsQuietUntil;

    /// <summary>
    /// Whether low oxygen or blood pressure should be warned about, from the last readout.
    /// </summary>
    [ViewVariables]
    public bool WarningActive;

    [ViewVariables]
    public TimeSpan? AnalyzeAt;

    /// <summary>
    /// A charge dumps itself if it isn't delivered in time, like the real unit.
    /// </summary>
    [DataField]
    public TimeSpan ChargeDumpTime = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan? ChargedAt;

    [ViewVariables]
    public TimeSpan? ChargeExpiresAt;

    /// <summary>
    /// Client only: recent beats in real time. The beep plays on each one and the traces draw a complex at each.
    /// </summary>
    [ViewVariables]
    public List<TimeSpan> BeatTimes = new();

    /// <summary>
    /// Client only: how much longer or shorter than the heart rate says the next beat comes, for irregular rhythms.
    /// </summary>
    [ViewVariables]
    public float BeatJitter = 1f;

    [ViewVariables]
    public TimeSpan AttachedAt;

    [ViewVariables]
    public TimeSpan NextNibp;

    [ViewVariables]
    public int? NibpSystolic;

    [ViewVariables]
    public int? NibpDiastolic;

    [ViewVariables]
    public string? NibpTime;

    [ViewVariables]
    public CMUMonitorRhythm LastRhythm = CMUMonitorRhythm.None;

    [ViewVariables]
    public List<CMUMonitorLogEntry> Log = new();
}
