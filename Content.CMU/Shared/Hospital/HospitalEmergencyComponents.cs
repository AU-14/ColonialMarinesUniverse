using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Body;

namespace Content.Shared.CMU14.Hospital;

[RegisterComponent]
public sealed partial class HospitalDropshipLandingZoneComponent : Component;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HospitalPatientComponent : Component
{
    public EntityUid SourceComputer;
    public bool IsVip;
    public bool DeathPenaltyApplied;
    public bool ArrivedWithFatalOutcome;
    [AutoPausedField] public TimeSpan NextPainLineAt;
    /// <summary>Original sites and organ capabilities; compatible replacements need not be the original entity or prototype.</summary>
    public readonly Dictionary<CMUMedicalBodyPartKey, List<HospitalAdmissionOrgan>> AdmissionAnatomy = new();
    /// <summary>Original part hierarchy, used to count one missing subtree rather than each absent descendant.</summary>
    public readonly Dictionary<CMUMedicalBodyPartKey, CMUMedicalBodyPartKey> AdmissionParents = new();
}

[Flags]
public enum HospitalOrganCapabilities : ushort
{
    None = 0,
    Health = 1 << 0,
    Heart = 1 << 1,
    Lungs = 1 << 2,
    Liver = 1 << 3,
    Kidneys = 1 << 4,
    Stomach = 1 << 5,
    Brain = 1 << 6,
    Eyes = 1 << 7,
    Ears = 1 << 8,
}

public readonly record struct HospitalAdmissionOrgan(
    string Slot,
    ProtoId<OrganCategoryPrototype>? Category,
    HospitalOrganCapabilities Capabilities);

public readonly record struct HospitalDischargeAssessment(
    int MissedInjuries,
    bool MissingAnatomy,
    bool FatalOutcome,
    bool IncompatibleOrgan = false,
    bool TreatmentPending = false)
{
    public bool EligibleForReward => !MissingAnatomy && !FatalOutcome && !IncompatibleOrgan;
    public bool Cleared => EligibleForReward && MissedInjuries == 0 && !TreatmentPending;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HospitalEmergencyComputerComponent : Component
{
    [DataField]
    public ResPath ShuttlePath = new("/Maps/CMU14/ShuttlesDropships/rmc_ert_pmc_shuttle.yml");

    [DataField]
    public EntProtoId PatientPrototype = "AU14HospitalPatient";

    [DataField]
    public EntProtoId ReturnDestinationPrototype = "CMDropshipDestinationThirdPartyReturn";

    [DataField]
    public EntProtoId CashPrototype = "RMCSpaceCash";

    [DataField]
    public int MinCasualties = 3;

    [DataField]
    public int MaxCasualties = 6;

    [DataField]
    public int BaseRewardPerPatient = 500;

    [DataField]
    public int SeverityRewardBonus = 250;

    [DataField]
    public int MissedInjuryPenalty = 75;

    [DataField]
    public int VipMissedInjuryPenalty = 750;

    [DataField]
    public int PermanentlyDeceasedPenalty = 750;

    [DataField]
    public TimeSpan FirstIncidentDelay = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan IncidentInterval = TimeSpan.FromSeconds(180);

    [DataField]
    public TimeSpan ManualUnloadWindow = TimeSpan.FromSeconds(120);

    [DataField]
    public TimeSpan PickupBoardingDelay = TimeSpan.FromSeconds(120);

    [DataField]
    public float ShuttleStartupTime = 3f;

    [DataField]
    public float ShuttleDepartureStartupTime = 10f;

    [DataField]
    public float ShuttleTravelTime = 15f;

    [DataField]
    public SoundSpecifier NotificationSound = new SoundPathSpecifier("/Audio/CMU14/Hospital/spo2_alarm.ogg");

    [DataField]
    public SoundSpecifier RewardSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public List<EntProtoId> PatientJumpsuits = new()
    {
        "RMCJumpsuitCivilian",
        "RMCJumpsuitCivilianBrown",
        "RMCJumpsuitCivilianGreen",
        "RMCJumpsuitCivilianBlue",
        "RMCJumpsuitNormCivilianGrey",
        "RMCJumpsuitNormCivilianBrown",
        "RMCJumpsuitBlueWorkwear",
        "RMCJumpsuitKhakiWorkwear",
        "RMCJumpsuitEMT",
        "RMCJumpsuitDoctor",
        "CMJumpsuitTShirtWhite",
        "CMJumpsuitTShirtGray",
        "CMJumpsuitTShirtRed",
        "CMJumpsuitColonist",
        "CMJumpsuitMarineMedic",
        "AU14JumpsuitVeteranPMCCorporate",
        "AU14JumpsuitCivilianVAIMAR40",
        "AU14JumpsuitCivilianBrownVAIM41A",
        "AU14JumpsuitCivilianBlueVAIWebbingSurgical",
        "AU14JumpsuitFORECON",
        "AU14JumpsuitArmyUPP",
    };

    [DataField]
    public List<EntProtoId> PatientShoes = new()
    {
        "CMBootsBlack",
        "CMBootsBrown",
        "CMBootsGrey",
        "CMBootsJungle",
        "RMCShoesBlack",
        "RMCShoesBrown",
        "RMCShoesLeather",
        "RMCShoesLaceup",
        "RMCBootsCorporate",
        "AU14BootsUSArmy",
        "AU14BootsJungle",
        "AU14LACNBoots",
    };

    [DataField]
    public List<EntProtoId> PatientOuterClothing = new()
    {
        "AU14CivilianJacketBlueParka",
        "AU14CivilianJacketGreenParka",
        "AU14CivilianJacketGrayPufferJacket",
        "AU14CivilianJacketKhakiPufferJacket",
        "AU14CivilianJacketBomberJacket",
        "AU14CivilianJacketOldCoat",
        "AU14CivilianTanTrenchCoat",
        "AU14CivilianGrayTrenchCoat",
    };

    [DataField]
    public List<EntProtoId> PatientHeadgear = new()
    {
        "RMCHeadBeanie",
        "RMCHeadBeanieTan",
        "RMCHeadBeanieGray",
        "RMCHeadCapGrey",
        "RMCHeadCapFlippable",
        "RMCHeadCapCargo",
        "CMHeadBandGreen",
        "CMHeadBandBrown",
        "CMHeadBandGray",
        "AU14HeadPithHelmet",
        "AU14HeadBeretRMC",
        "RMCHeadBeret",
        "CMHeadCap",
    };

    [DataField]
    public List<EntProtoId> PatientGloves = new()
    {
        "RMCHandsBlack",
        "RMCHandsCombat",
        "CMHandsBrown",
        "CMHandsLightBrown",
        "RMCHandsFingerlessMarine",
        "AU14PVEHandsFingerlessBlackGloves",
        "AU14PVEHandsFingerlessBrownGloves",
    };

    [DataField]
    public float PatientOuterClothingChance = 0.65f;

    [DataField]
    public float PatientHeadgearChance = 0.35f;

    [DataField]
    public float PatientGlovesChance = 0.3f;

    public HospitalEmergencyStatus Status = HospitalEmergencyStatus.Idle;
    public HospitalShuttlePurpose ShuttlePurpose = HospitalShuttlePurpose.None;
    [AutoPausedField] public TimeSpan NextIncidentAt;
    [AutoPausedField] public TimeSpan PhaseEndsAt;
    [AutoPausedField] public TimeSpan NextUiRefreshAt;
    [AutoPausedField] public TimeSpan NextLandingZoneRefreshAt;
    [AutoPausedField] public TimeSpan NextTransportRetryAt;
    public string TransportFailure = string.Empty;
    public int Casualties;
    public int Severity;
    public int Reward;
    public int LastPayout;
    public int LastMissedInjuries;
    public int LastVipPenalty;
    public int LastPermanentDeathPenalty;
    public string IncidentReport = string.Empty;
    public HospitalPatientClothingTheme PatientClothingTheme = HospitalPatientClothingTheme.Civilian;
    public EntityUid? LandingZone;
    public EntityUid? ActiveShuttle;
    public EntityUid? ReturnDestination;
    public EntityUid? ExpectedDestination;
    /// <summary>Maps and unparented entities created by this transport's load, never the hospital map.</summary>
    public readonly HashSet<EntityUid> TransportRoots = new();
    public EntityUid? VipPatient;
    public readonly List<EntityUid> Patients = new();
}

[Serializable, NetSerializable]
public enum HospitalEmergencyStatus : byte
{
    Idle,
    AwaitingApproval,
    Arriving,
    ManualUnloading,
    ShuttleDeparting,
    Treating,
    PickupInbound,
    PickupBoarding,
    RewardReady,
    WaitingForDeparture,
    WaitingForArrival,
}

public enum HospitalShuttlePurpose : byte
{
    None,
    InboundPatients,
    ReturningAfterManualUnload,
    PickupInbound,
    PickupReturning,
}

public enum HospitalPatientClothingTheme : byte
{
    Civilian,
    Worksite,
    Engineering,
    Medical,
    Military,
    LawEnforcement,
    Mining,
    Biohazard,
    Marines,
    Upp,
    Cmb,
    Nspa,
}
