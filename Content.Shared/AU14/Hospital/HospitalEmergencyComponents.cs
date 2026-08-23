using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.AU14.Hospital;

[RegisterComponent]
public sealed partial class HospitalDropshipLandingZoneComponent : Component;

[RegisterComponent]
public sealed partial class HospitalPatientComponent : Component
{
    public EntityUid SourceComputer;
    public bool IsVip;
    public bool DeathPenaltyApplied;
    public bool ArrivedWithFatalOutcome;
    public TimeSpan NextPainLineAt;
}

[RegisterComponent]
public sealed partial class HospitalEmergencyComputerComponent : Component
{
    [DataField]
    public ResPath ShuttlePath = new("/Maps/_AU14/ShuttlesDropships/rmc_ert_pmc_shuttle.yml");

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
    public int MissedInjuryPenalty = 100;

    [DataField]
    public int VipMissedInjuryPenalty = 1000;

    [DataField]
    public int PermanentlyDeceasedPenalty = 1000;

    [DataField]
    public TimeSpan FirstIncidentDelay = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan IncidentInterval = TimeSpan.FromMinutes(12);

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
    public SoundSpecifier NotificationSound = new SoundPathSpecifier("/Audio/_CMU14/Hospital/spo2_alarm.ogg");

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
    public TimeSpan NextIncidentAt;
    public TimeSpan PhaseEndsAt;
    public TimeSpan NextUiRefreshAt;
    public TimeSpan NextLandingZoneRefreshAt;
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
}
