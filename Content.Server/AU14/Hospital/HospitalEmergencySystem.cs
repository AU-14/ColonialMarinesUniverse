using Content.Server.Chat.Systems;
using Content.Server.Stack;
using Content.Server.Shuttles.Events;
using Content.Server._CMU14.Ops.ThirdParty;
using Content.Server._RMC14.Dropship;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._RMC14.Dropship;
using Content.Shared.AU14.Hospital;
using Content.Shared.AU14.Scenario;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.AU14.Hospital;

public sealed partial class HospitalEmergencySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedDropshipSystem _dropship = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedFractureSystem _fracture = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;
    [Dependency] private SharedCMUSurgicalTraitSystem _surgicalTraits = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private static readonly ProtoId<DamageTypePrototype> Piercing = "Piercing";
    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";
    private static readonly ProtoId<DamageTypePrototype> Cellular = "Cellular";
    private static readonly TimeSpan UiRefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LandingZoneRefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PatientCheckInterval = TimeSpan.FromSeconds(1);

    private readonly List<EntityUid> _bodyPartBuffer = new();
    private readonly List<(EntityUid Id, OrganComponent Component)> _organBuffer = new();
    private readonly List<EntityUid> _patientBuffer = new();
    private readonly List<EntityCoordinates> _spawnCoordinates = new();
    private TimeSpan _nextPatientCheck;
    private static readonly EntProtoId[] EmptyClothing = Array.Empty<EntProtoId>();

    private static readonly string[] ModeratePainLines =
    {
        "Please, something for the pain.",
        "I think something is broken.",
        "My side is killing me.",
        "Everything hurts.",
    };

    private static readonly string[] SeverePainLines =
    {
        "I can't breathe right.",
        "My chest hurts.",
        "Don't let me pass out.",
        "It burns. Please make it stop.",
    };

    private static readonly string[] ShockPainLines =
    {
        "I can't feel my hands.",
        "I'm getting so cold.",
        "I can't stay awake.",
        "Please, don't let me die.",
    };

    private static readonly HospitalIncidentTemplate[] SeverityOneIncidents =
    {
        new("Worksite accident. Patients are ambulatory but need full trauma clearance.", HospitalPatientClothingTheme.Worksite),
        new("Convoy rollover. Minor crush injuries and lacerations reported.", HospitalPatientClothingTheme.Civilian),
        new("Generator flashover. Burns and blunt trauma expected.", HospitalPatientClothingTheme.Engineering),
        new("Clinic oxygen fire. Medical staff and patients are inbound for burn and smoke exposure screening.", HospitalPatientClothingTheme.Medical),
        new("Dockyard loader collision. Cargo workers need evaluation for crush trauma and fractures.", HospitalPatientClothingTheme.Worksite),
        new("Security checkpoint brawl. CMB riot marshals have minor ballistic and blunt trauma.", HospitalPatientClothingTheme.Cmb),
    };

    private static readonly HospitalIncidentTemplate[] SeverityTwoIncidents =
    {
        new("Colonial Marine dropship decompression event. Multiple fractures and internal bleeding suspected.", HospitalPatientClothingTheme.Marines),
        new("Industrial collapse. Casualties are stable, but several require urgent surgical follow-up.", HospitalPatientClothingTheme.Engineering),
        new("Hostile wildlife incident. Deep tissue trauma and organ injuries likely.", HospitalPatientClothingTheme.Civilian),
        new("NSPA police shootout. Wounded constables are inbound with ballistic trauma.", HospitalPatientClothingTheme.Nspa),
        new("Chemical plant accident. Biohazard teams report toxic exposure, burns, and organ complications.", HospitalPatientClothingTheme.Biohazard),
        new("Mining drill cave-in. Miners are inbound with crush injuries, fractures, and internal bleeding.", HospitalPatientClothingTheme.Mining),
    };

    private static readonly HospitalIncidentTemplate[] SeverityThreeIncidents =
    {
        new("Mass casualty distress call. Critical patients inbound with compound fractures and internal bleeding.", HospitalPatientClothingTheme.Civilian),
        new("UPP combat evacuation. Naval infantry casualties have heavy trauma, organ damage, and severe blood loss.", HospitalPatientClothingTheme.Upp),
        new("Mining station breach. Patients are unstable and require complete trauma reconstruction.", HospitalPatientClothingTheme.Mining),
        new("CBRN containment failure. Biohazard casualties are inbound with severe burns and organ failure.", HospitalPatientClothingTheme.Biohazard),
        new("CMB bureau raid gone wrong. Riot team casualties have critical ballistic and blast trauma.", HospitalPatientClothingTheme.Cmb),
        new("Orbital refinery explosion. Engineering crews are inbound with crush trauma, eschars, and internal bleeding.", HospitalPatientClothingTheme.Engineering),
    };

    private static readonly PatientClothingProfile WorksiteClothing = new(
        new EntProtoId[] { "RMCJumpsuitBlueWorkwear", "RMCJumpsuitKhakiWorkwear", "CMJumpsuitTShirtGray", "CMJumpsuitColonist" },
        new EntProtoId[] { "CMBootsBlack", "CMBootsBrown", "RMCBootsCorporate" },
        new EntProtoId[] { "RMCHazardVest", "RMCHazardVestYellow", "RMCHazardVestBlue", "AU14CivilianHazardVestSanitation" },
        new EntProtoId[] { "RMCHardhatOrange", "RMCHardhatWhite", "RMCHeadCapCargo" },
        new EntProtoId[] { "RMCHandsBlack", "CMHandsBrown", "AU14PVEHandsFingerlessBlackGloves" },
        EmptyClothing,
        0.9f,
        0.75f,
        0.45f,
        0f);

    private static readonly PatientClothingProfile EngineeringClothing = new(
        new EntProtoId[] { "CMJumpsuitMarineEngineer", "CMJumpsuitChiefEngineer", "RMCJumpsuitBlueWorkwear", "RMCJumpsuitKhakiWorkwear" },
        new EntProtoId[] { "CMBootsBlack", "CMBootsBrown", "CMBootsGrey" },
        new EntProtoId[] { "RMCHazardVest", "RMCHazardVestBlack", "RMCHazardVestYellow", "RMCHazardVestBlue" },
        new EntProtoId[] { "CMHeadBeretEngineer", "RMCHardhatWhite", "RMCHardhatOrange", "RMCHeadCapFlippable" },
        new EntProtoId[] { "RMCHandsCombat", "RMCHandsBlack", "CMHandsBrown" },
        new EntProtoId[] { "CMMaskGas" },
        0.85f,
        0.75f,
        0.55f,
        0.25f);

    private static readonly PatientClothingProfile MedicalClothing = new(
        new EntProtoId[] { "RMCJumpsuitDoctor", "RMCJumpsuitEMT", "CMJumpsuitMarineMedic" },
        new EntProtoId[] { "RMCShoesBlack", "RMCShoesLaceup", "CMBootsBlack" },
        new EntProtoId[] { "AU14CivilianHazardVestParamedicWhite", "AU14CivilianHazardVestParamedicGreen", "RMCHazardVestEMT", "RMCHazardVestEMTGreen" },
        new EntProtoId[] { "CMHeadCapSurgBlue", "CMHeadCapSurgGreen", "CMHeadCapSurgOrange", "CMHeadCapCMO" },
        new EntProtoId[] { "RMCHandsBlack", "CMHandsLightBrown" },
        new EntProtoId[] { "CMMaskGasMedical" },
        0.6f,
        0.55f,
        0.6f,
        0.25f);

    private static readonly PatientClothingProfile MarineClothing = new(
        new EntProtoId[] { "JumpsuitMarine", "CMJumpsuitMarineMedic", "CMJumpsuitMarineEngineer" },
        new EntProtoId[] { "CMBootsBlack", "CMBootsBrown", "CMBootsJungle" },
        new EntProtoId[] { "CMArmorM3Medium", "CMArmorM3Light", "CMArmorM3Heavy" },
        new EntProtoId[] { "ArmorHelmetM10", "CMArmorHelmetM10Medic", "CMArmorHelmetM10MP", "CMArmorHelmetM10Tech" },
        new EntProtoId[] { "CMHandsBlackMarine", "RMCHandsCombat", "RMCHandsFingerlessMarine" },
        new EntProtoId[] { "CMMaskGas" },
        1f,
        0.95f,
        0.85f,
        0.35f);

    private static readonly PatientClothingProfile UppClothing = new(
        new EntProtoId[] { "AU14FatiguesUPP", "AU14JumpsuitArmyUPP" },
        new EntProtoId[] { "RMCBootsSPPBlack" },
        new EntProtoId[] { "AU14UPPArmor", "AU14UPPArmorMinimalistic", "AU14UPPArmorBulky" },
        new EntProtoId[] { "AU14UPPNavalInfantryHelmet", "AU14UPPPatrolCap", "AU14UPPBoonie" },
        new EntProtoId[] { "RMCHandsBlack" },
        new EntProtoId[] { "CMMaskGas" },
        1f,
        0.95f,
        0.85f,
        0.35f);

    private static readonly PatientClothingProfile CmbClothing = new(
        new EntProtoId[] { "AU14CMBUniform", "RMCSwatCMBUniform", "RMCMarshalCMBUniform" },
        new EntProtoId[] { "CMBootsBlack", "CMBootsGrey" },
        new EntProtoId[] { "CMArmorRiot" },
        new EntProtoId[] { "ArmorHelmetRiot" },
        new EntProtoId[] { "RMCHandsBlack", "RMCHandsCombat" },
        new EntProtoId[] { "CMMaskGas" },
        1f,
        0.95f,
        0.8f,
        0.3f);

    private static readonly PatientClothingProfile NspaClothing = new(
        new EntProtoId[] { "RMCJumpsuitTSEPA" },
        new EntProtoId[] { "CMBootsBlack" },
        new EntProtoId[] { "RMCArmorVestTSEPA", "RMCArmourM4TSEPA", "RMCArmourM4TSEPAChief" },
        new EntProtoId[] { "RMCHeadCapTSEPA", "RMCHeadCapTSEPAPeaked", "RMCHeadCapTSEPAPeakedGold" },
        new EntProtoId[] { "RMCHandsBlack", "RMCHandsCombat" },
        new EntProtoId[] { "CMMaskGas" },
        1f,
        0.85f,
        0.65f,
        0.2f);

    private static readonly PatientClothingProfile MiningClothing = new(
        new EntProtoId[] { "AU14CivilianKellandMiningClothes", "RMCJumpsuitMercenaryMiner", "RMCJumpsuitKhakiWorkwear", "RMCJumpsuitBlueWorkwear" },
        new EntProtoId[] { "RMCBootsCorporate", "CMBootsBrown", "CMBootsBlack" },
        new EntProtoId[] { "AU14CivilianHazardVestKellandMiningCorporation", "RMCArmorMercenaryMiner", "RMCHazardVestYellow", "RMCHazardVest" },
        new EntProtoId[] { "RMCHardhatOrange", "RMCHardhatWhite", "RMCArmorHelmetMercenaryMiner", "RMCArmorHelmetTMCCMiner" },
        new EntProtoId[] { "RMCHandsBlack", "CMHandsBrown", "RMCHandsCombat" },
        new EntProtoId[] { "CMMaskGas" },
        0.95f,
        0.9f,
        0.55f,
        0.25f);

    private static readonly PatientClothingProfile BiohazardClothing = new(
        new EntProtoId[] { "AU14JoeHazmat", "RMCJumpsuitDoctor", "RMCJumpsuitEMT", "CMJumpsuitTShirtWhite" },
        new EntProtoId[] { "CMBootsBlack", "RMCShoesBlack", "RMCBootsCorporate" },
        new EntProtoId[] { "RMCSuitBioGeneral", "RMCSuitBioScientist", "RMCSuitBioMedical", "RMCSuitBioSecurity", "AU14SuitBioWeYu", "RMCSuitRadiation" },
        new EntProtoId[] { "RMCHoodBioGeneral", "RMCHoodBioScientist", "RMCHoodBioMedical", "RMCHoodBioSecurity", "RMCHoodBioWeYaAlt", "RMCHeadRadiationHood" },
        new EntProtoId[] { "RMCHandsBlack", "RMCHandsCombat" },
        new EntProtoId[] { "CMMaskGasMedical", "CMMaskGas" },
        1f,
        1f,
        0.9f,
        0.65f);

    private sealed record HospitalIncidentTemplate(string Report, HospitalPatientClothingTheme ClothingTheme);

    private readonly record struct PatientClothingProfile(
        IReadOnlyList<EntProtoId> Jumpsuits,
        IReadOnlyList<EntProtoId> Shoes,
        IReadOnlyList<EntProtoId> OuterClothing,
        IReadOnlyList<EntProtoId> Headgear,
        IReadOnlyList<EntProtoId> Gloves,
        IReadOnlyList<EntProtoId> Masks,
        float OuterClothingChance,
        float HeadgearChance,
        float GlovesChance,
        float MaskChance);

    public override void Initialize()
    {
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, HospitalEmergencyApproveLandingMsg>(OnApproveLanding);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, HospitalEmergencySkipContractMsg>(OnSkipContract);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, HospitalEmergencyRequestPickupMsg>(OnRequestPickup);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, HospitalEmergencyReleaseShuttleMsg>(OnReleaseShuttle);
        SubscribeLocalEvent<HospitalPatientComponent, MobStateChangedEvent>(OnPatientMobStateChanged);
        SubscribeLocalEvent<RottingComponent, ComponentInit>(OnPatientRottingInit);
        SubscribeLocalEvent<UnrevivableComponent, ComponentInit>(OnPatientUnrevivableInit);
        SubscribeLocalEvent<FTLCompletedEvent>(OnDropshipFtlCompleted);
    }

    private void OnMapInit(Entity<HospitalEmergencyComputerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextIncidentAt = _timing.CurTime + ent.Comp.FirstIncidentDelay;
        ent.Comp.Status = HospitalEmergencyStatus.Idle;
        ent.Comp.LandingZone = FindLandingZone(ent);
        ent.Comp.NextLandingZoneRefreshAt = _timing.CurTime + LandingZoneRefreshInterval;
    }

    private void OnUiOpened(Entity<HospitalEmergencyComputerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    public int SetNextIncidentDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;

        var now = _timing.CurTime;
        var updated = 0;
        var query = EntityQueryEnumerator<HospitalEmergencyComputerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Status is not (HospitalEmergencyStatus.Idle or HospitalEmergencyStatus.RewardReady))
                continue;

            comp.NextIncidentAt = now + delay;
            comp.NextUiRefreshAt = now;
            UpdateUi((uid, comp));
            updated++;
        }

        return updated;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HospitalEmergencyComputerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);

            EnsureLandingZone(ent, now);

            switch (comp.Status)
            {
                case HospitalEmergencyStatus.Idle:
                    if (comp.NextIncidentAt != TimeSpan.Zero && now >= comp.NextIncidentAt)
                        CreateIncident(ent);
                    break;

                case HospitalEmergencyStatus.ManualUnloading:
                    if (now >= comp.PhaseEndsAt)
                        BeginManualUnloadDeparture(ent);
                    break;

                case HospitalEmergencyStatus.PickupBoarding:
                    if (now >= comp.PhaseEndsAt)
                        FinishPickup(ent);
                    break;

                case HospitalEmergencyStatus.RewardReady:
                    if (comp.NextIncidentAt != TimeSpan.Zero && now >= comp.NextIncidentAt)
                        CreateIncident(ent);
                    break;
            }

            if (now >= comp.NextUiRefreshAt)
            {
                comp.NextUiRefreshAt = now + UiRefreshInterval;
                UpdateUi(ent);
            }
        }

        if (now >= _nextPatientCheck)
        {
            _nextPatientCheck = now + PatientCheckInterval;
            UpdatePatients(now);
        }
    }

    private void OnApproveLanding(Entity<HospitalEmergencyComputerComponent> ent, ref HospitalEmergencyApproveLandingMsg args)
    {
        if (ent.Comp.Status != HospitalEmergencyStatus.AwaitingApproval)
            return;

        EnsureLandingZone(ent, _timing.CurTime, true);

        if (ent.Comp.LandingZone == null)
        {
            _popup.PopupEntity("No hospital dropship landing zone is available.", ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        if (!TryLaunchShuttle(ent, ent.Comp.LandingZone.Value, args.Actor, HospitalShuttlePurpose.InboundPatients))
            return;

        ent.Comp.Status = HospitalEmergencyStatus.Arriving;
        UpdateUi(ent);
    }

    private void OnSkipContract(Entity<HospitalEmergencyComputerComponent> ent, ref HospitalEmergencySkipContractMsg args)
    {
        if (ent.Comp.Status != HospitalEmergencyStatus.AwaitingApproval)
            return;

        ClearPendingIncident(ent.Comp);
        ent.Comp.Status = HospitalEmergencyStatus.Idle;
        ent.Comp.NextIncidentAt = _timing.CurTime + ent.Comp.IncidentInterval;
        ent.Comp.NextUiRefreshAt = _timing.CurTime;
        UpdateUi(ent);
    }

    private void OnRequestPickup(Entity<HospitalEmergencyComputerComponent> ent, ref HospitalEmergencyRequestPickupMsg args)
    {
        if (ent.Comp.Status != HospitalEmergencyStatus.Treating)
            return;

        EnsureLandingZone(ent, _timing.CurTime, true);

        if (ent.Comp.LandingZone == null)
        {
            _popup.PopupEntity("No hospital dropship landing zone is available.", ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        if (ent.Comp.Patients.Count == 0)
        {
            _popup.PopupEntity("There are no evacuation patients to release.", ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        if (!TryLaunchShuttle(ent, ent.Comp.LandingZone.Value, args.Actor, HospitalShuttlePurpose.PickupInbound))
            return;

        ent.Comp.Status = HospitalEmergencyStatus.PickupInbound;
        UpdateUi(ent);
    }

    private void OnReleaseShuttle(Entity<HospitalEmergencyComputerComponent> ent, ref HospitalEmergencyReleaseShuttleMsg args)
    {
        switch (ent.Comp.Status)
        {
            case HospitalEmergencyStatus.ManualUnloading:
                BeginManualUnloadDeparture(ent);
                break;

            case HospitalEmergencyStatus.PickupBoarding:
                FinishPickup(ent);
                break;
        }
    }

    private void OnPatientMobStateChanged(Entity<HospitalPatientComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            ent.Comp.ArrivedWithFatalOutcome = false;
    }

    private void OnPatientRottingInit(Entity<RottingComponent> ent, ref ComponentInit args)
    {
        TryApplyPermanentDeathPenalty(ent.Owner);
    }

    private void OnPatientUnrevivableInit(Entity<UnrevivableComponent> ent, ref ComponentInit args)
    {
        TryApplyPermanentDeathPenalty(ent.Owner);
    }

    private void OnDropshipFtlCompleted(ref FTLCompletedEvent args)
    {
        var query = EntityQueryEnumerator<HospitalEmergencyComputerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ActiveShuttle != args.Entity)
                continue;

            var computer = (uid, comp);
            switch (comp.ShuttlePurpose)
            {
                case HospitalShuttlePurpose.InboundPatients:
                    comp.Status = HospitalEmergencyStatus.ManualUnloading;
                    comp.PhaseEndsAt = _timing.CurTime + comp.ManualUnloadWindow;
                    comp.ShuttlePurpose = HospitalShuttlePurpose.ReturningAfterManualUnload;
                    break;

                case HospitalShuttlePurpose.ReturningAfterManualUnload:
                    CleanupShuttle(computer);
                    comp.Status = HospitalEmergencyStatus.Treating;
                    break;

                case HospitalShuttlePurpose.PickupInbound:
                    comp.Status = HospitalEmergencyStatus.PickupBoarding;
                    comp.PhaseEndsAt = _timing.CurTime + comp.PickupBoardingDelay;
                    comp.ShuttlePurpose = HospitalShuttlePurpose.PickupReturning;
                    break;

                case HospitalShuttlePurpose.PickupReturning:
                    CleanupShuttle(computer);
                    break;
            }

            UpdateUi(computer);
            return;
        }
    }

    private void CreateIncident(Entity<HospitalEmergencyComputerComponent> ent)
    {
        var comp = ent.Comp;
        comp.Casualties = _random.Next(comp.MinCasualties, comp.MaxCasualties + 1);
        comp.Severity = _random.Next(1, 4);
        comp.Reward = comp.Casualties * (comp.BaseRewardPerPatient + comp.SeverityRewardBonus * comp.Severity);
        var incident = PickIncident(comp.Severity);
        comp.IncidentReport = $"{incident.Report} One casualty is flagged VIP; unresolved VIP injuries add a ${comp.VipMissedInjuryPenalty} audit penalty.";
        comp.PatientClothingTheme = incident.ClothingTheme;
        comp.LastPayout = 0;
        comp.LastMissedInjuries = 0;
        comp.LastVipPenalty = 0;
        comp.LastPermanentDeathPenalty = 0;
        comp.VipPatient = null;
        comp.Patients.Clear();
        comp.Status = HospitalEmergencyStatus.AwaitingApproval;
        comp.NextIncidentAt = TimeSpan.Zero;
        comp.NextUiRefreshAt = _timing.CurTime;

        _audio.PlayPvs(comp.NotificationSound, ent);
        UpdateUi(ent);
    }

    private static void ClearPendingIncident(HospitalEmergencyComputerComponent comp)
    {
        comp.Casualties = 0;
        comp.Severity = 0;
        comp.Reward = 0;
        comp.IncidentReport = string.Empty;
        comp.PatientClothingTheme = HospitalPatientClothingTheme.Civilian;
        comp.VipPatient = null;
        comp.Patients.Clear();
    }

    private HospitalIncidentTemplate PickIncident(int severity)
    {
        return severity switch
        {
            1 => _random.Pick(SeverityOneIncidents),
            2 => _random.Pick(SeverityTwoIncidents),
            _ => _random.Pick(SeverityThreeIncidents),
        };
    }

    private bool TryLaunchShuttle(
        Entity<HospitalEmergencyComputerComponent> ent,
        EntityUid destination,
        EntityUid actor,
        HospitalShuttlePurpose purpose)
    {
        if (ent.Comp.ActiveShuttle != null && !Deleted(ent.Comp.ActiveShuttle))
            return false;

        if (!TryLoadShuttle(ent, out var shuttle, out var nav, out var returnDestination))
        {
            _popup.PopupEntity("The hospital shuttle could not be prepared.", ent, actor);
            return false;
        }

        ent.Comp.ActiveShuttle = shuttle;
        ent.Comp.ReturnDestination = returnDestination;
        ent.Comp.ShuttlePurpose = purpose;

        if (purpose == HospitalShuttlePurpose.InboundPatients)
            LoadPatientsOntoShuttle(ent, shuttle);

        if (!_dropship.FlyTo(nav, destination, actor, startupTime: ent.Comp.ShuttleStartupTime, hyperspaceTime: ent.Comp.ShuttleTravelTime))
        {
            if (purpose == HospitalShuttlePurpose.InboundPatients)
            {
                foreach (var patient in ent.Comp.Patients)
                {
                    if (!Deleted(patient))
                        QueueDel(patient);
                }

                ent.Comp.Patients.Clear();
            }

            CleanupShuttle(ent);
            return false;
        }

        return true;
    }

    private bool TryLoadShuttle(
        Entity<HospitalEmergencyComputerComponent> ent,
        out EntityUid shuttle,
        out Entity<DropshipNavigationComputerComponent> navigationComputer,
        out EntityUid returnDestination)
    {
        shuttle = default;
        navigationComputer = default;
        returnDestination = default;

        if (!_mapLoader.TryLoadGeneric(
                ent.Comp.ShuttlePath,
                out var result,
                new MapLoadOptions
                {
                    DeserializationOptions = DeserializationOptions.Default with
                    {
                        InitializeMaps = true,
                        LogOrphanedGrids = false,
                    },
                }))
        {
            return false;
        }

        foreach (var grid in result.Grids)
        {
            shuttle = grid;
            break;
        }

        if (shuttle == default || !TryFindNavigationComputer(shuttle, out navigationComputer))
        {
            QueueDel(shuttle);
            return false;
        }

        var returnCoords = Transform(shuttle).Coordinates;
        returnDestination = Spawn(ent.Comp.ReturnDestinationPrototype, returnCoords);

        var returnComp = EnsureComp<ThirdPartyDropshipReturnDestinationComponent>(returnDestination);
        returnComp.Shuttle = shuttle;
        Dirty(returnDestination, returnComp);

        _dropship.SetDestinationShip(returnDestination, shuttle);
        _dropship.SetDestinationHome(returnDestination, true);
        EnsureComp<DropshipComponent>(shuttle);
        _dropship.SetDropshipDestination(shuttle, returnDestination);

        return true;
    }

    private bool TryFindNavigationComputer(EntityUid shuttle, out Entity<DropshipNavigationComputerComponent> navigationComputer)
    {
        var query = EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != shuttle && xform.ParentUid != shuttle)
                continue;

            navigationComputer = (uid, comp);
            return true;
        }

        navigationComputer = default;
        return false;
    }

    private void BeginManualUnloadDeparture(Entity<HospitalEmergencyComputerComponent> ent)
    {
        ent.Comp.Status = HospitalEmergencyStatus.ShuttleDeparting;
        ent.Comp.PhaseEndsAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ShuttleDepartureStartupTime);
        ReturnShuttle(ent, HospitalEmergencyStatus.Treating);
        UpdateUi(ent);
    }

    private void LoadPatientsOntoShuttle(Entity<HospitalEmergencyComputerComponent> ent, EntityUid shuttle)
    {
        ent.Comp.Patients.Clear();
        ent.Comp.VipPatient = null;
        GetShuttlePatientSpawnCoordinates(shuttle, _spawnCoordinates);
        var vipIndex = ent.Comp.Casualties > 0
            ? _random.Next(ent.Comp.Casualties)
            : -1;

        for (var i = 0; i < ent.Comp.Casualties; i++)
        {
            var coordinates = _spawnCoordinates.Count > 0
                ? _spawnCoordinates[i % _spawnCoordinates.Count].Offset(_random.NextVector2(0.05f, 0.25f))
                : new EntityCoordinates(shuttle, _random.NextVector2(0.5f, 2.5f));

            var patient = Spawn(ent.Comp.PatientPrototype, coordinates);
            PrepareHospitalPatient(patient);

            var patientComp = EnsureComp<HospitalPatientComponent>(patient);
            patientComp.SourceComputer = ent;
            patientComp.IsVip = i == vipIndex;
            patientComp.DeathPenaltyApplied = false;
            patientComp.ArrivedWithFatalOutcome = false;
            patientComp.NextPainLineAt = _timing.CurTime + RandomPainLineDelay(initial: true);

            if (patientComp.IsVip)
            {
                ent.Comp.VipPatient = patient;
                _meta.SetEntityName(patient, $"{Name(patient)} (VIP)");
            }

            OutfitPatient(ent, patient);
            ApplyPatientInjuries(patient, ent.Comp.Severity);
            patientComp.ArrivedWithFatalOutcome = HasFatalOutcome(patient);
            ent.Comp.Patients.Add(patient);
        }
    }

    private void GetShuttlePatientSpawnCoordinates(EntityUid shuttle, List<EntityCoordinates> coordinates)
    {
        coordinates.Clear();
        var query = EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var marker, out var xform))
        {
            if (xform.GridUid != shuttle && xform.ParentUid != shuttle)
                continue;

            if (!marker.Tags.Contains("force:third-party"))
                continue;

            coordinates.Add(xform.Coordinates);
        }

        coordinates.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));
    }

    private void PrepareHospitalPatient(EntityUid patient)
    {
        RemComp<SSDIndicatorComponent>(patient);
        _statusEffects.TryRemoveStatusEffect(patient, SSDIndicatorSystem.StatusEffectSSDSleeping);
        RemComp<SleepingComponent>(patient);
    }

    private void ReturnShuttle(Entity<HospitalEmergencyComputerComponent> ent, HospitalEmergencyStatus statusIfUnable)
    {
        if (ent.Comp.ActiveShuttle == null ||
            Deleted(ent.Comp.ActiveShuttle) ||
            ent.Comp.ReturnDestination == null ||
            Deleted(ent.Comp.ReturnDestination))
        {
            CleanupShuttle(ent);
            ent.Comp.Status = statusIfUnable;
            ent.Comp.PhaseEndsAt = TimeSpan.Zero;
            return;
        }

        if (!TryFindNavigationComputer(ent.Comp.ActiveShuttle.Value, out var nav))
        {
            CleanupShuttle(ent);
            ent.Comp.Status = statusIfUnable;
            ent.Comp.PhaseEndsAt = TimeSpan.Zero;
            return;
        }

        if (!_dropship.FlyTo(
            nav,
            ent.Comp.ReturnDestination.Value,
            ent,
            startupTime: ent.Comp.ShuttleDepartureStartupTime,
            hyperspaceTime: ent.Comp.ShuttleTravelTime))
        {
            CleanupShuttle(ent);
            ent.Comp.Status = statusIfUnable;
            ent.Comp.PhaseEndsAt = TimeSpan.Zero;
        }
    }

    private void OutfitPatient(Entity<HospitalEmergencyComputerComponent> computer, EntityUid patient)
    {
        var profile = GetPatientClothingProfile(computer.Comp);

        TryEquipRandomPatientItem(patient, profile.Jumpsuits, "jumpsuit");
        TryEquipRandomPatientItem(patient, profile.Shoes, "shoes");

        if (_random.Prob(profile.OuterClothingChance))
            TryEquipRandomPatientItem(patient, profile.OuterClothing, "outerClothing");

        if (_random.Prob(profile.HeadgearChance))
            TryEquipRandomPatientItem(patient, profile.Headgear, "head");

        if (_random.Prob(profile.GlovesChance))
            TryEquipRandomPatientItem(patient, profile.Gloves, "gloves");

        if (_random.Prob(profile.MaskChance))
            TryEquipRandomPatientItem(patient, profile.Masks, "mask");
    }

    private PatientClothingProfile GetPatientClothingProfile(HospitalEmergencyComputerComponent comp)
    {
        return comp.PatientClothingTheme switch
        {
            HospitalPatientClothingTheme.Worksite => WorksiteClothing,
            HospitalPatientClothingTheme.Engineering => EngineeringClothing,
            HospitalPatientClothingTheme.Medical => MedicalClothing,
            HospitalPatientClothingTheme.Military or HospitalPatientClothingTheme.Marines => MarineClothing,
            HospitalPatientClothingTheme.Upp => UppClothing,
            HospitalPatientClothingTheme.Cmb or HospitalPatientClothingTheme.LawEnforcement => CmbClothing,
            HospitalPatientClothingTheme.Nspa => NspaClothing,
            HospitalPatientClothingTheme.Mining => MiningClothing,
            HospitalPatientClothingTheme.Biohazard => BiohazardClothing,
            _ => new PatientClothingProfile(
                comp.PatientJumpsuits,
                comp.PatientShoes,
                comp.PatientOuterClothing,
                comp.PatientHeadgear,
                comp.PatientGloves,
                EmptyClothing,
                comp.PatientOuterClothingChance,
                comp.PatientHeadgearChance,
                comp.PatientGlovesChance,
                0f),
        };
    }

    private void TryEquipRandomPatientItem(EntityUid patient, IReadOnlyList<EntProtoId> prototypes, string slot)
    {
        if (prototypes.Count == 0)
            return;

        var item = Spawn(_random.Pick(prototypes), Transform(patient).Coordinates);
        if (!_inventory.TryEquip(patient, item, slot, silent: true, force: true))
            QueueDel(item);
    }

    private void ApplyPatientInjuries(EntityUid patient, int severity)
    {
        var damage = new DamageSpecifier();
        var trauma = severity switch
        {
            1 => _random.NextFloat(55f, 80f),
            2 => _random.NextFloat(95f, 135f),
            _ => _random.NextFloat(130f, 185f),
        };

        damage.DamageDict[Blunt] = trauma * 0.52f;
        damage.DamageDict[Slash] = trauma * 0.12f;
        damage.DamageDict[Piercing] = trauma * 0.16f;

        if (severity >= 2)
            damage.DamageDict[Heat] = trauma * 0.2f;

        _damage.TryChangeDamage(patient, damage, true);

        _bodyPartBuffer.Clear();
        foreach (var part in _body.GetBodyChildren(patient))
        {
            _bodyPartBuffer.Add(part.Id);
        }

        if (_bodyPartBuffer.Count == 0)
            return;

        ApplyPatientFractures(_bodyPartBuffer, severity);
        ApplyPatientInternalBleeds(_bodyPartBuffer, severity);
        ApplyPatientEschars(_bodyPartBuffer, severity);
        ApplyPatientOrganInjuries(patient, severity);
    }

    private void ApplyPatientFractures(IReadOnlyList<EntityUid> bodyParts, int severity)
    {
        _patientBuffer.Clear();
        _patientBuffer.AddRange(bodyParts);

        var fractureCount = severity switch
        {
            1 => PickPatientInjuryCount(_patientBuffer.Count, 2, 3),
            2 => PickPatientInjuryCount(_patientBuffer.Count, 3, 4),
            _ => PickPatientInjuryCount(_patientBuffer.Count, 4, 6),
        };

        for (var i = 0; i < fractureCount; i++)
        {
            var part = _random.PickAndTake(_patientBuffer);
            var fracture = EnsureComp<FractureComponent>(part);
            var fractureSeverity = severity switch
            {
                1 => FractureSeverity.Compound,
                2 => _random.Prob(0.7f) ? FractureSeverity.Compound : FractureSeverity.Shattered,
                _ => _random.Prob(0.85f) ? FractureSeverity.Shattered : FractureSeverity.Compound,
            };

            _fracture.SetSeverity((part, fracture), fractureSeverity);
            _surgicalTraits.RemoveTrait(part, CMUSurgicalTrait.ContaminatedWound);
        }
    }

    private void ApplyPatientInternalBleeds(IReadOnlyList<EntityUid> bodyParts, int severity)
    {
        _patientBuffer.Clear();
        _patientBuffer.AddRange(bodyParts);

        var bleedCount = severity switch
        {
            1 => PickPatientInjuryCount(_patientBuffer.Count, 1, 2),
            2 => PickPatientInjuryCount(_patientBuffer.Count, 3, 4),
            _ => PickPatientInjuryCount(_patientBuffer.Count, 4, 6),
        };

        for (var i = 0; i < bleedCount; i++)
        {
            var part = _random.PickAndTake(_patientBuffer);
            var rate = severity switch
            {
                1 => _random.NextFloat(0.28f, 0.45f),
                2 => _random.NextFloat(0.55f, 0.85f),
                _ => _random.NextFloat(0.85f, 1.25f),
            };

            _wounds.SeedInternalBleed(part, "hospital shuttle internal trauma", rate);
        }
    }

    private void ApplyPatientEschars(IReadOnlyList<EntityUid> bodyParts, int severity)
    {
        _patientBuffer.Clear();
        _patientBuffer.AddRange(bodyParts);

        var escharCount = severity switch
        {
            1 => _random.Prob(0.35f) ? 1 : 0,
            2 => PickPatientInjuryCount(_patientBuffer.Count, 1, 2),
            _ => PickPatientInjuryCount(_patientBuffer.Count, 2, 4),
        };

        for (var i = 0; i < escharCount; i++)
        {
            var part = _random.PickAndTake(_patientBuffer);
            var eschar = EnsureComp<CMUEscharComponent>(part);
            eschar.AppliedAt = _timing.CurTime;
            Dirty(part, eschar);
        }
    }

    private void ApplyPatientOrganInjuries(EntityUid patient, int severity)
    {
        _organBuffer.Clear();
        foreach (var organ in _body.GetBodyOrgans(patient))
        {
            if (HasComp<OrganHealthComponent>(organ.Id))
                _organBuffer.Add(organ);
        }

        if (_organBuffer.Count == 0)
            return;

        var organCount = severity switch
        {
            1 => PickPatientInjuryCount(_organBuffer.Count, 1, 1),
            2 => PickPatientInjuryCount(_organBuffer.Count, 2, 3),
            _ => PickPatientInjuryCount(_organBuffer.Count, 3, 5),
        };

        var criticalOrganQueued = severity >= 3;
        if (organCount > 0 && severity >= 3 && TryPickHeartOrgan(_organBuffer, out var heart))
        {
            DamagePatientOrgan(patient, heart, _random.NextFloat(45f, 48f));
            organCount--;
            criticalOrganQueued = false;
        }

        for (var i = 0; i < organCount; i++)
        {
            var organ = _random.PickAndTake(_organBuffer).Id;
            var amount = criticalOrganQueued
                ? _random.NextFloat(45f, 48f)
                : severity switch
                {
                    1 => _random.NextFloat(16f, 24f),
                    2 => _random.NextFloat(28f, 38f),
                    _ => _random.NextFloat(38f, 46f),
                };

            DamagePatientOrgan(patient, organ, amount);
            criticalOrganQueued = false;
        }
    }

    private bool TryPickHeartOrgan(List<(EntityUid Id, OrganComponent Component)> organs, out EntityUid heart)
    {
        for (var i = 0; i < organs.Count; i++)
        {
            var organ = organs[i].Id;
            if (!HasComp<HeartComponent>(organ))
                continue;

            heart = organ;
            organs.RemoveAt(i);
            return true;
        }

        heart = default;
        return false;
    }

    private void DamagePatientOrgan(EntityUid patient, EntityUid organ, float cellularDamage)
    {
        var organDamage = new DamageSpecifier();
        organDamage.DamageDict[Cellular] = cellularDamage;

        var ev = new OrganDamagedEvent(patient, organ, organDamage, OrganDamageSource.Direct);
        RaiseLocalEvent(organ, ref ev);
    }

    private int PickPatientInjuryCount(int available, int minInclusive, int maxInclusive)
    {
        if (available <= 0)
            return 0;

        var min = Math.Min(available, minInclusive);
        var max = Math.Min(available, maxInclusive);
        return _random.Next(min, max + 1);
    }

    private void UpdatePatients(TimeSpan now)
    {
        var query = EntityQueryEnumerator<HospitalPatientComponent>();
        while (query.MoveNext(out var uid, out var patient))
        {
            if (!patient.DeathPenaltyApplied && HasPermanentFatalOutcome(uid))
                TryApplyPermanentDeathPenalty((uid, patient));

            if (patient.NextPainLineAt == TimeSpan.Zero)
            {
                patient.NextPainLineAt = now + RandomPainLineDelay(initial: true);
                continue;
            }

            if (now < patient.NextPainLineAt)
                continue;

            patient.NextPainLineAt = now + RandomPainLineDelay();
            TrySpeakPainLine(uid);
        }
    }

    private TimeSpan RandomPainLineDelay(bool initial = false)
    {
        var min = initial ? 8f : 18f;
        var max = initial ? 18f : 42f;
        return TimeSpan.FromSeconds(_random.NextFloat(min, max));
    }

    private void TrySpeakPainLine(EntityUid patient)
    {
        if (!_mobState.IsAlive(patient) ||
            HasComp<SleepingComponent>(patient) ||
            HasPatientPainSuppression(patient))
        {
            return;
        }

        var tier = PainTier.Moderate;
        if (TryComp<PainShockComponent>(patient, out var pain))
            tier = _pain.GetEffectiveTier(patient, pain);

        if (tier < PainTier.Moderate)
            return;

        if (!HasAnyMissedInjury(patient))
            return;

        _chat.TrySendInGameICMessage(
            patient,
            _random.Pick(GetPainLines(tier)),
            InGameICChatType.Speak,
            ChatTransmitRange.Normal,
            hideLog: true,
            checkRadioPrefix: false,
            ignoreActionBlocker: true);
    }

    private bool HasPatientPainSuppression(EntityUid patient)
    {
        return _statusEffects.HasEffectComp<PainNumbnessStatusEffectComponent>(patient) ||
            _pain.GetAccumulationSuppression(patient) >= 0.5f ||
            _pain.GetTierSuppression(patient) >= 2;
    }

    private static IReadOnlyList<string> GetPainLines(PainTier tier)
    {
        return tier switch
        {
            PainTier.Shock => ShockPainLines,
            PainTier.Severe => SeverePainLines,
            _ => ModeratePainLines,
        };
    }

    private void FinishPickup(Entity<HospitalEmergencyComputerComponent> ent)
    {
        var missed = 0;
        var vipPenalty = 0;
        var boardedPatients = 0;
        foreach (var patient in ent.Comp.Patients)
        {
            if (Deleted(patient))
                continue;

            if (!IsPatientOnActiveShuttle(ent, patient))
            {
                RemComp<HospitalPatientComponent>(patient);
                continue;
            }

            if (!TryComp<HospitalPatientComponent>(patient, out var patientComp))
            {
                QueueDel(patient);
                continue;
            }

            var fatalOutcome = HasFatalOutcome(patient);
            var permanentOutcome = TryApplyPermanentDeathPenalty((patient, patientComp), ent, updateUi: false);
            var fatalOutcomeExempt = IsArrivalFatalOutcomeExempt(patientComp, fatalOutcome);
            var patientMissed = fatalOutcomeExempt
                ? 0
                : CountMissedInjuries(patient);
            var isVip = ent.Comp.VipPatient == patient || patientComp.IsVip;

            if (isVip && !fatalOutcomeExempt && (patientMissed > 0 || permanentOutcome))
                vipPenalty += ent.Comp.VipMissedInjuryPenalty;

            if (!fatalOutcomeExempt)
            {
                boardedPatients++;
                missed += patientMissed;
            }

            QueueDel(patient);
        }

        var permanentDeathPenalty = ent.Comp.LastPermanentDeathPenalty;
        ent.Comp.Patients.Clear();
        ent.Comp.VipPatient = null;
        ent.Comp.LastMissedInjuries = missed;
        ent.Comp.LastVipPenalty = vipPenalty;
        ent.Comp.LastPermanentDeathPenalty = permanentDeathPenalty;
        ent.Comp.LastPayout = Math.Max(
            0,
            boardedPatients * GetRewardPerPatient(ent.Comp) -
            missed * ent.Comp.MissedInjuryPenalty -
            vipPenalty -
            permanentDeathPenalty);

        if (ent.Comp.LastPayout > 0)
        {
            _stack.SpawnMultipleNextToOrDrop(ent.Comp.CashPrototype, ent.Comp.LastPayout, ent.Owner);
            _audio.PlayPvs(ent.Comp.RewardSound, ent);
        }

        ent.Comp.Status = HospitalEmergencyStatus.RewardReady;
        ent.Comp.NextIncidentAt = _timing.CurTime + ent.Comp.IncidentInterval;
        ReturnShuttle(ent, HospitalEmergencyStatus.RewardReady);
        UpdateUi(ent);
    }

    private int GetRewardPerPatient(HospitalEmergencyComputerComponent comp)
    {
        return comp.BaseRewardPerPatient + comp.SeverityRewardBonus * comp.Severity;
    }

    private bool IsPatientOnActiveShuttle(Entity<HospitalEmergencyComputerComponent> ent, EntityUid patient)
    {
        if (ent.Comp.ActiveShuttle is not { } shuttle ||
            Deleted(shuttle))
        {
            return false;
        }

        var xform = Transform(patient);
        return xform.GridUid == shuttle || xform.ParentUid == shuttle;
    }

    private bool HasFatalOutcome(EntityUid patient)
    {
        return _mobState.IsDead(patient) || HasPermanentFatalOutcome(patient);
    }

    private bool HasPermanentFatalOutcome(EntityUid patient)
    {
        return HasComp<RottingComponent>(patient) || HasComp<UnrevivableComponent>(patient);
    }

    private bool TryApplyPermanentDeathPenalty(EntityUid patient)
    {
        if (!TryComp<HospitalPatientComponent>(patient, out var hospitalPatient))
            return false;

        return TryApplyPermanentDeathPenalty((patient, hospitalPatient));
    }

    private bool TryApplyPermanentDeathPenalty(Entity<HospitalPatientComponent> patient, bool updateUi = true)
    {
        var computerUid = patient.Comp.SourceComputer;
        if (Deleted(computerUid) ||
            !TryComp<HospitalEmergencyComputerComponent>(computerUid, out var computer) ||
            !computer.Patients.Contains(patient.Owner))
        {
            UpdateFatalOutcomeExemption(patient);
            return HasPermanentFatalOutcome(patient.Owner);
        }

        return TryApplyPermanentDeathPenalty(patient, (computerUid, computer), updateUi);
    }

    private bool TryApplyPermanentDeathPenalty(
        Entity<HospitalPatientComponent> patient,
        Entity<HospitalEmergencyComputerComponent> computer,
        bool updateUi = true)
    {
        if (patient.Comp.DeathPenaltyApplied)
            return HasPermanentFatalOutcome(patient.Owner);

        var fatalOutcome = UpdateFatalOutcomeExemption(patient);
        if (!HasPermanentFatalOutcome(patient.Owner))
            return false;

        if (IsArrivalFatalOutcomeExempt(patient.Comp, fatalOutcome))
            return true;

        patient.Comp.DeathPenaltyApplied = true;
        computer.Comp.LastPermanentDeathPenalty += computer.Comp.PermanentlyDeceasedPenalty;
        computer.Comp.NextUiRefreshAt = _timing.CurTime;

        if (updateUi)
            UpdateUi(computer);

        return true;
    }

    private bool UpdateFatalOutcomeExemption(Entity<HospitalPatientComponent> patient)
    {
        var fatalOutcome = HasFatalOutcome(patient.Owner);
        if (!fatalOutcome)
            patient.Comp.ArrivedWithFatalOutcome = false;

        return fatalOutcome;
    }

    private static bool IsArrivalFatalOutcomeExempt(HospitalPatientComponent patient, bool fatalOutcome)
    {
        return fatalOutcome && patient.ArrivedWithFatalOutcome;
    }

    private int CountMissedInjuries(EntityUid patient, bool stopAtFirst = false)
    {
        var missed = 0;

        if (TryComp<DamageableComponent>(patient, out var damageable))
        {
            foreach (var damage in _damage.GetAllDamage((patient, damageable)).DamageDict.Values)
            {
                if (damage > 0)
                {
                    missed++;
                    if (stopAtFirst)
                        return missed;
                }
            }
        }

        foreach (var part in _body.GetBodyChildren(patient))
        {
            if (TryComp<FractureComponent>(part.Id, out var fracture) &&
                fracture.Severity != FractureSeverity.None)
            {
                missed++;
                if (stopAtFirst)
                    return missed;
            }

            if (HasComp<InternalBleedingComponent>(part.Id))
            {
                missed++;
                if (stopAtFirst)
                    return missed;
            }

            if (HasComp<CMUEscharComponent>(part.Id))
            {
                missed++;
                if (stopAtFirst)
                    return missed;
            }

            if (TryComp<BodyPartWoundComponent>(part.Id, out var bodyPartWounds))
            {
                var wounds = _woundLedger.CountUntreatedWounds(bodyPartWounds);
                missed += wounds;
                if (stopAtFirst && wounds > 0)
                    return missed;
            }
        }

        foreach (var organ in _body.GetBodyOrgans(patient))
        {
            if (TryComp<OrganHealthComponent>(organ.Id, out var organHealth) &&
                organHealth.Current < organHealth.Max)
            {
                missed++;
                if (stopAtFirst)
                    return missed;
            }
        }

        return missed;
    }

    private (int Active, int FullyHealed) CountPatientStates(HospitalEmergencyComputerComponent comp)
    {
        var active = 0;
        var healed = 0;
        var countHealed = comp.Status is HospitalEmergencyStatus.Treating
            or HospitalEmergencyStatus.PickupInbound
            or HospitalEmergencyStatus.PickupBoarding;

        foreach (var patient in comp.Patients)
        {
            if (Deleted(patient))
                continue;

            active++;
            if (countHealed && !HasAnyMissedInjury(patient))
                healed++;
        }

        return (active, healed);
    }

    private bool HasAnyMissedInjury(EntityUid patient)
    {
        return CountMissedInjuries(patient, true) > 0;
    }

    private EntityUid? FindLandingZone(Entity<HospitalEmergencyComputerComponent> ent)
    {
        EntityUid? fallback = null;
        EntityUid? nearest = null;
        var nearestDistance = float.MaxValue;
        var computerCoords = _transform.GetMapCoordinates(ent);
        var computerMap = computerCoords.MapId;

        var query = EntityQueryEnumerator<HospitalDropshipLandingZoneComponent, DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            fallback ??= uid;
            var zoneCoords = _transform.GetMapCoordinates(uid, xform);
            if (zoneCoords.MapId != computerMap)
                continue;

            var distance = (zoneCoords.Position - computerCoords.Position).LengthSquared();
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = uid;
        }

        return nearest ?? fallback;
    }

    private bool EnsureLandingZone(Entity<HospitalEmergencyComputerComponent> ent, TimeSpan now, bool force = false)
    {
        if (ent.Comp.LandingZone is { } landingZone && !Deleted(landingZone))
            return true;

        if (!force && now < ent.Comp.NextLandingZoneRefreshAt)
            return false;

        var foundLandingZone = FindLandingZone(ent);
        var changed = ent.Comp.LandingZone != foundLandingZone;
        ent.Comp.LandingZone = foundLandingZone;
        ent.Comp.NextLandingZoneRefreshAt = now + LandingZoneRefreshInterval;
        if (changed)
            UpdateUi(ent);

        return ent.Comp.LandingZone != null && !Deleted(ent.Comp.LandingZone);
    }

    private void CleanupShuttle(Entity<HospitalEmergencyComputerComponent> ent)
    {
        if (ent.Comp.ActiveShuttle != null && !Deleted(ent.Comp.ActiveShuttle))
            QueueDel(ent.Comp.ActiveShuttle);

        if (ent.Comp.ReturnDestination != null && !Deleted(ent.Comp.ReturnDestination))
            QueueDel(ent.Comp.ReturnDestination);

        ent.Comp.ActiveShuttle = null;
        ent.Comp.ReturnDestination = null;
        ent.Comp.ShuttlePurpose = HospitalShuttlePurpose.None;
    }

    private void UpdateUi(Entity<HospitalEmergencyComputerComponent> ent)
    {
        var comp = ent.Comp;
        var remaining = GetSecondsRemaining(comp);
        var (activePatients, fullyHealedPatients) = CountPatientStates(comp);
        var hasLandingZone = comp.LandingZone != null && !Deleted(comp.LandingZone);

        var state = new HospitalEmergencyComputerBuiState(
            GetStatusText(comp, remaining),
            comp.IncidentReport,
            comp.Casualties,
            comp.Severity,
            comp.Reward,
            activePatients,
            fullyHealedPatients,
            comp.LastPayout,
            comp.LastMissedInjuries,
            comp.LastVipPenalty,
            comp.LastPermanentDeathPenalty,
            remaining,
            hasLandingZone,
            comp.Status == HospitalEmergencyStatus.AwaitingApproval && hasLandingZone,
            comp.Status == HospitalEmergencyStatus.AwaitingApproval,
            comp.Status == HospitalEmergencyStatus.Treating && activePatients > 0,
            comp.Status is HospitalEmergencyStatus.ManualUnloading or HospitalEmergencyStatus.PickupBoarding);

        _ui.SetUiState(ent.Owner, HospitalEmergencyComputerUi.Key, state);
    }

    private int GetSecondsRemaining(HospitalEmergencyComputerComponent comp)
    {
        var now = _timing.CurTime;
        var target = comp.PhaseEndsAt;
        if (comp.Status is HospitalEmergencyStatus.Idle or HospitalEmergencyStatus.RewardReady &&
            comp.NextIncidentAt != TimeSpan.Zero)
        {
            target = comp.NextIncidentAt;
        }

        return target > now
            ? (int) Math.Ceiling((target - now).TotalSeconds)
            : 0;
    }

    private static string GetStatusText(HospitalEmergencyComputerComponent comp, int secondsRemaining)
    {
        return comp.Status switch
        {
            HospitalEmergencyStatus.Idle => comp.NextIncidentAt == TimeSpan.Zero
                ? "Standing by"
                : $"Standing by. Next orbital alert in {secondsRemaining} seconds.",
            HospitalEmergencyStatus.AwaitingApproval => "Hospital shuttle in orbit. Landing approval required.",
            HospitalEmergencyStatus.Arriving => "Hospital shuttle approved and inbound.",
            HospitalEmergencyStatus.ManualUnloading => "Hospital shuttle landed. Manually unload casualties.",
            HospitalEmergencyStatus.ShuttleDeparting => "Hospital shuttle departure sequence active.",
            HospitalEmergencyStatus.Treating => "Casualties are in hospital care.",
            HospitalEmergencyStatus.PickupInbound => "Recovery shuttle inbound for patient release.",
            HospitalEmergencyStatus.PickupBoarding => "Recovered patients are boarding the pickup shuttle.",
            HospitalEmergencyStatus.RewardReady => "Audit complete. Payment has been dispensed.",
            _ => "Standing by",
        };
    }
}
