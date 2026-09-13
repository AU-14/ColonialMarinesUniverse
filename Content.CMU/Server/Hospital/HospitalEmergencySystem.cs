using Content.Server.Chat.Systems;
using Content.Server.Stack;
using Content.Server.Shuttles.Events;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server._RMC14.Dropship;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Ears;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Hospital;
using Content.Shared.CMU14.Round;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Content.Shared.Shuttles.Components;
using Content.Shared.Verbs;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Hospital;

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
    [Dependency] private SharedBoneSystem _bone = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;
    [Dependency] private SharedCMUSurgicalTraitSystem _surgicalTraits = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

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
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, ComponentShutdown>(OnComputerShutdown);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, HospitalEmergencyApproveLandingMsg>(OnApproveLanding);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, HospitalEmergencySkipContractMsg>(OnSkipContract);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, HospitalEmergencyRequestPickupMsg>(OnRequestPickup);
        SubscribeLocalEvent<HospitalEmergencyComputerComponent, HospitalEmergencyReleaseShuttleMsg>(OnReleaseShuttle);
        SubscribeLocalEvent<HospitalPatientComponent, MobStateChangedEvent>(OnPatientMobStateChanged);
        SubscribeLocalEvent<RottingComponent, ComponentInit>(OnPatientRottingInit);
        SubscribeLocalEvent<UnrevivableComponent, ComponentInit>(OnPatientUnrevivableInit);
        SubscribeLocalEvent<FTLCompletedEvent>(OnDropshipFtlCompleted);
        SubscribeLocalEvent<DropshipNavigationComputerComponent, GetVerbsEvent<AlternativeVerb>>(OnTransportRecoveryVerb);
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

    private void OnComputerShutdown(Entity<HospitalEmergencyComputerComponent> ent, ref ComponentShutdown args)
    {
        // Destroying a console is not authorization to destroy a shuttle or its passengers.
        foreach (var patient in ent.Comp.Patients)
        {
            if (!Deleted(patient) && TryComp<HospitalPatientComponent>(patient, out var owned) && owned.SourceComputer == ent.Owner)
                RemComp<HospitalPatientComponent>(patient);
        }

        if (ent.Comp.ActiveShuttle is { } shuttle && TryGetTransportLease(shuttle, out var lease) &&
            lease.Comp.Computer == ent.Owner && ReferenceEquals(lease.Comp.Controller, ent.Comp))
        {
            lease.Comp.Computer = null;
            lease.Comp.Controller = null;
            lease.Comp.Retiring = true;
            lease.Comp.NextAction = _timing.CurTime;
            ClearComputerTransport(ent.Comp);
            return;
        }

        CleanupShuttle(ent);
    }

    private void ReconcileTransport(Entity<HospitalEmergencyComputerComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.ActiveShuttle is not { } shuttle)
            return;

        if (!TryGetTransportLease(shuttle, out var lease) || lease.Comp.Computer != ent.Owner ||
            !ReferenceEquals(lease.Comp.Controller, comp))
        {
            ClearComputerTransport(comp);
            comp.Status = HospitalEmergencyStatus.Treating;
            comp.TransportFailure = "Hospital transport ownership was lost. Other transports and their occupants remain untouched.";
            return;
        }

        if (Deleted(shuttle))
        {
            CleanupShuttle(ent);
            comp.Status = HospitalEmergencyStatus.Treating;
            comp.TransportFailure = "Hospital shuttle was lost. Surviving patients remain assigned to this hospital.";
            FinishEmptyIncident(ent);
            return;
        }

        if (comp.Status is not (HospitalEmergencyStatus.ShuttleDeparting or HospitalEmergencyStatus.Arriving or HospitalEmergencyStatus.PickupInbound or HospitalEmergencyStatus.WaitingForArrival) ||
            HasComp<FTLComponent>(shuttle) || _timing.CurTime < comp.PhaseEndsAt ||
            _timing.CurTime < comp.NextTransportRetryAt)
            return;

        if (comp.Status is HospitalEmergencyStatus.Arriving or HospitalEmergencyStatus.PickupInbound or HospitalEmergencyStatus.WaitingForArrival)
        {
            ReconcileUnfinishedHospitalFlight(lease);
            comp.ExpectedDestination = null;
            comp.NextTransportRetryAt = _timing.CurTime + UiRefreshInterval;
            EnsureLandingZone(ent, _timing.CurTime, true);
            if (comp.LandingZone is { } landingZone &&
                TryStartHospitalFlight(lease, landingZone, null, comp.ShuttleStartupTime, comp.ShuttlePurpose))
            {
                comp.ExpectedDestination = landingZone;
                comp.Status = comp.ShuttlePurpose == HospitalShuttlePurpose.PickupInbound
                    ? HospitalEmergencyStatus.PickupInbound
                    : HospitalEmergencyStatus.Arriving;
                comp.PhaseEndsAt = _timing.CurTime + TimeSpan.FromSeconds(comp.ShuttleStartupTime + comp.ShuttleTravelTime + 30);
                comp.TransportFailure = string.Empty;
            }
            else
            {
                comp.Status = HospitalEmergencyStatus.WaitingForArrival;
                comp.PhaseEndsAt = _timing.CurTime;
                comp.TransportFailure = "Hospital shuttle arrival failed. Restore its navigation and hospital landing zone; arrival will be retried.";
            }
            return;
        }

        // Cancellation or external rerouting cannot be accepted as a successful return.
        comp.ExpectedDestination = null;
        ReconcileUnfinishedHospitalFlight(lease);
        comp.Status = HospitalEmergencyStatus.WaitingForDeparture;
        comp.TransportFailure = "Hospital shuttle did not reach its return destination. Retrying departure.";
    }

    private void FinishEmptyIncident(Entity<HospitalEmergencyComputerComponent> ent)
    {
        for (var i = ent.Comp.Patients.Count - 1; i >= 0; i--)
        {
            var patient = ent.Comp.Patients[i];
            if (Deleted(patient) || !TryComp<HospitalPatientComponent>(patient, out var patientComp) ||
                patientComp.SourceComputer != ent.Owner)
                ent.Comp.Patients.RemoveAt(i);
        }

        if (ent.Comp.Patients.Count != 0)
            return;

        ent.Comp.LastPayout = 0;
        ent.Comp.VipPatient = null;
        ent.Comp.Status = HospitalEmergencyStatus.RewardReady;
        ent.Comp.NextIncidentAt = _timing.CurTime + ent.Comp.IncidentInterval;
    }

    private void RemoveReturnedPatients(Entity<HospitalEmergencyComputerComponent> ent)
    {
        for (var i = ent.Comp.Patients.Count - 1; i >= 0; i--)
        {
            var patient = ent.Comp.Patients[i];
            if (!Deleted(patient) && !IsPatientOnActiveShuttle(ent, patient))
                continue;

            if (!Deleted(patient) && TryComp<HospitalPatientComponent>(patient, out var patientComp) &&
                patientComp.SourceComputer == ent.Owner)
                QueueDel(patient);
            ent.Comp.Patients.RemoveAt(i);
        }
    }

    public int SetNextIncidentDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;

        var now = _timing.CurTime;
        if (delay > TimeSpan.MaxValue - now)
            delay = TimeSpan.MaxValue - now;
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
            ReconcileTransport(ent);

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

                case HospitalEmergencyStatus.WaitingForDeparture:
                    if (now >= comp.NextTransportRetryAt)
                        TryReturnShuttle(ent);
                    break;

                case HospitalEmergencyStatus.Treating:
                    FinishEmptyIncident(ent);
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
        UpdateTransportLeases(now);
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
        if (!TryGetTransportLease(args.Entity, out var lease) || !IsExpectedHospitalArrival(lease, args))
            return;
        var flight = lease.Comp.Flight!;
        // Consume before callbacks or billing so duplicate completions are inert.
        lease.Comp.Flight = null;
        lease.Comp.Failure = string.Empty;
        if (!TryGetTransportController(lease, out var computer))
        {
            lease.Comp.Retiring = true;
            lease.Comp.NextAction = _timing.CurTime;
            return;
        }
        var comp = computer.Comp;
        if (comp.ExpectedDestination != flight.Destination || comp.ShuttlePurpose != flight.Purpose)
            return;
        comp.ExpectedDestination = null;
        comp.TransportFailure = string.Empty;
        switch (flight.Purpose)
        {
            case HospitalShuttlePurpose.InboundPatients:
                comp.Status = HospitalEmergencyStatus.ManualUnloading;
                comp.PhaseEndsAt = _timing.CurTime + comp.ManualUnloadWindow;
                break;
            case HospitalShuttlePurpose.ReturningAfterManualUnload:
                RemoveReturnedPatients(computer);
                CleanupShuttle(computer);
                comp.Status = HospitalEmergencyStatus.Treating;
                FinishEmptyIncident(computer);
                break;
            case HospitalShuttlePurpose.PickupInbound:
                comp.Status = HospitalEmergencyStatus.PickupBoarding;
                comp.PhaseEndsAt = _timing.CurTime + comp.PickupBoardingDelay;
                break;
            case HospitalShuttlePurpose.PickupReturning:
                SettlePickup(computer);
                CleanupShuttle(computer);
                break;
        }
        UpdateUi(computer);
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
        comp.TransportFailure = string.Empty;
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
        if (ent.Comp.ActiveShuttle is { } existing && !TransportUnavailable(existing))
            return false;
        if (!TryLoadShuttle(ent, out var shuttle, out _, out var returnDestination) ||
            !TryGetTransportLease(shuttle, out var lease))
        {
            _popup.PopupEntity("The hospital shuttle could not be prepared.", ent, actor);
            return false;
        }
        var comp = ent.Comp;
        comp.ActiveShuttle = shuttle;
        comp.ReturnDestination = returnDestination;
        comp.ShuttlePurpose = purpose;
        comp.ExpectedDestination = null;
        comp.Status = HospitalEmergencyStatus.WaitingForArrival;
        comp.PhaseEndsAt = _timing.CurTime;
        comp.NextTransportRetryAt = _timing.CurTime + UiRefreshInterval;
        try
        {
            if (purpose == HospitalShuttlePurpose.InboundPatients && !LoadPatientsOntoShuttle(ent, shuttle))
            {
                RequestHospitalTransportRecovery(shuttle);
                return false;
            }
        }
        catch (Exception exception)
        {
            Log.Warning($"Hospital patient preparation failed; preserving its tracked transport and patients: {exception}");
            RequestHospitalTransportRecovery(shuttle);
            return false;
        }
        if (!TryStartHospitalFlight(lease, destination, actor, comp.ShuttleStartupTime, purpose))
        {
            comp.TransportFailure = lease.Comp.Failure;
            UpdateUi(ent);
            return false;
        }
        if (!TryGetTransportController(lease, out var controller) || controller.Owner != ent.Owner ||
            !ReferenceEquals(controller.Comp, comp))
            return false;
        comp.ExpectedDestination = destination;
        comp.PhaseEndsAt = _timing.CurTime + TimeSpan.FromSeconds(comp.ShuttleStartupTime + comp.ShuttleTravelTime + 30);
        comp.TransportFailure = string.Empty;
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
        if (Transform(ent).MapUid is not { } hospitalMap ||
            !TryComp<MapComponent>(hospitalMap, out var hospitalMapComponent) || ent.Comp.LandingZone is not { } hospitalDestination)
            return false;
        var leaseUid = Spawn(null, MapCoordinates.Nullspace);
        var lease = AddComp<HospitalTransportLeaseComponent>(leaseUid);
        lease.Computer = ent.Owner;
        lease.Controller = ent.Comp;
        lease.HospitalMap = hospitalMap;
        lease.HospitalMapComponent = hospitalMapComponent;
        lease.HospitalDestination = hospitalDestination;
        lease.StartupTime = ent.Comp.ShuttleStartupTime;
        lease.TravelTime = ent.Comp.ShuttleTravelTime;
        // MapInit can move an existing foreign object onto the new map. Current
        // map membership is insufficient provenance for deleting it later.
        var preexisting = new HashSet<EntityUid>();
        var existing = EntityManager.AllEntityQueryEnumerator<TransformComponent>();
        while (existing.MoveNext(out var existingUid, out _))
            preexisting.Add(existingUid);
        try
        {
            if (!_mapLoader.TryLoadGeneric(ent.Comp.ShuttlePath, out var result, new MapLoadOptions
                {
                    DeserializationOptions = DeserializationOptions.Default with
                    {
                        InitializeMaps = true,
                        LogOrphanedGrids = false,
                    },
                }))
            {
                QueueDel(leaseUid);
                return false;
            }
            lease.Roots.UnionWith(result.RootNodes);
            lease.AuthoredEntities.UnionWith(result.Entities);
            foreach (var map in result.Maps)
            {
                lease.Roots.Add(map.Owner);
                if (TryComp<MapComponent>(map.Owner, out var mapComponent))
                    lease.Maps.Add(map.Owner, mapComponent);
            }
            // Include newly spawned MapInit equipment, but never claim existing
            // visitors/property that an initialization callback moved onto it.
            var authored = EntityManager.AllEntityQueryEnumerator<TransformComponent>();
            while (authored.MoveNext(out var entity, out var transform))
                if (!preexisting.Contains(entity) && transform.MapUid is { } authoredMap && lease.Maps.ContainsKey(authoredMap))
                    lease.AuthoredEntities.Add(entity);
            foreach (var grid in result.Grids)
            {
                shuttle = grid;
                break;
            }
            lease.Shuttle = shuttle;
            if (!IsCurrentHospitalComputer(ent) || shuttle == default || !TryFindNavigationComputer(shuttle, out navigationComputer))
            {
                lease.Retiring = true;
                lease.Computer = null;
                lease.Controller = null;
                TryReclaimTransport((leaseUid, lease));
                return false;
            }
            EnsureComp<HospitalTransportShuttleComponent>(shuttle).Lease = leaseUid;
            ent.Comp.ActiveShuttle = shuttle;
            ent.Comp.TransportRoots.UnionWith(lease.Roots);

            // Spawn the anchored marker through its map position so its initial grid
            // agrees with the engine's anchoring contract. NoFTL leaves it behind.
            var returnCoords = _transform.ToMapCoordinates(Transform(shuttle).Coordinates);
            returnDestination = Spawn(ent.Comp.ReturnDestinationPrototype, returnCoords);
            lease.ReturnDestination = returnDestination;
            if (!IsCurrentHospitalComputer(ent))
            {
                RequestHospitalTransportRecovery(shuttle);
                return false;
            }
            ent.Comp.ReturnDestination = returnDestination;
            EnsureComp<ThirdPartyDropshipReturnDestinationComponent>(returnDestination).Shuttle = shuttle;
            if (TryComp<WhitelistedShuttleComponent>(navigationComputer.Owner, out var whitelist))
                whitelist.AutoReturn = false;
            _dropship.SetDestinationShip(returnDestination, shuttle);
            _dropship.SetDestinationHome(returnDestination, true);
            EnsureComp<DropshipComponent>(shuttle);
            _dropship.SetDropshipDestination(shuttle, returnDestination);
            return true;
        }
        catch (Exception exception)
        {
            Log.Warning($"Hospital shuttle preparation failed: {exception}");
            lease.Retiring = true;
            lease.Computer = null;
            lease.Controller = null;
            ClearComputerTransport(ent.Comp);
            TryReclaimTransport((leaseUid, lease));
            return false;
        }
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
        ent.Comp.ShuttlePurpose = HospitalShuttlePurpose.ReturningAfterManualUnload;
        ent.Comp.Status = HospitalEmergencyStatus.WaitingForDeparture;
        TryReturnShuttle(ent);
        UpdateUi(ent);
    }

    private bool LoadPatientsOntoShuttle(Entity<HospitalEmergencyComputerComponent> ent, EntityUid shuttle)
    {
        ent.Comp.Patients.Clear();
        ent.Comp.VipPatient = null;
        GetShuttlePatientSpawnCoordinates(shuttle, _spawnCoordinates);
        var vipIndex = ent.Comp.Casualties > 0
            ? _random.Next(ent.Comp.Casualties)
            : -1;

        for (var i = 0; i < ent.Comp.Casualties; i++)
        {
            if (!IsCurrentHospitalComputer(ent) || TransportUnavailable(shuttle))
                return false;
            var coordinates = _spawnCoordinates.Count > 0
                ? _spawnCoordinates[i % _spawnCoordinates.Count].Offset(_random.NextVector2(0.05f, 0.25f))
                : new EntityCoordinates(shuttle, _random.NextVector2(0.5f, 2.5f));

            var patient = Spawn(ent.Comp.PatientPrototype, coordinates);
            if (!IsCurrentHospitalComputer(ent) || TransportUnavailable(patient))
                return false;
            var patientComp = EnsureComp<HospitalPatientComponent>(patient);
            patientComp.SourceComputer = ent;
            // Track immediately: outfit/injury callbacks can throw or delete the
            // console, and must not leave an unowned partially prepared patient.
            ent.Comp.Patients.Add(patient);
            PrepareHospitalPatient(patient);
            if (!IsCurrentHospitalComputer(ent) || TransportUnavailable(patient) ||
                !TryComp<HospitalPatientComponent>(patient, out var prepared) || !ReferenceEquals(prepared, patientComp))
                return false;
            patientComp.IsVip = i == vipIndex;
            patientComp.DeathPenaltyApplied = false;
            patientComp.ArrivedWithFatalOutcome = false;
            patientComp.NextPainLineAt = _timing.CurTime + RandomPainLineDelay(initial: true);
            CaptureAdmissionAnatomy((patient, patientComp));

            if (patientComp.IsVip)
            {
                ent.Comp.VipPatient = patient;
                _meta.SetEntityName(patient, $"{Name(patient)} (VIP)");
            }

            OutfitPatient(ent, patient);
            if (!IsCurrentHospitalComputer(ent) || TransportUnavailable(patient))
                return false;
            ApplyPatientInjuries(patient, ent.Comp.Severity);
            if (!IsCurrentHospitalComputer(ent) || TransportUnavailable(patient) ||
                !TryComp<HospitalPatientComponent>(patient, out var injured) || !ReferenceEquals(injured, patientComp))
                return false;
            patientComp.ArrivedWithFatalOutcome = HasFatalOutcome(patient);
        }
        return true;
    }

    private void GetShuttlePatientSpawnCoordinates(EntityUid shuttle, List<EntityCoordinates> coordinates)
    {
        coordinates.Clear();
        var query = EntityQueryEnumerator<ThreatSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var marker, out var xform))
        {
            if (xform.GridUid != shuttle && xform.ParentUid != shuttle)
                continue;

            if (!marker.ThirdParty)
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

    private void TryReturnShuttle(Entity<HospitalEmergencyComputerComponent> ent)
    {
        var comp = ent.Comp;
        comp.NextTransportRetryAt = _timing.CurTime + UiRefreshInterval;
        if (comp.ActiveShuttle is not { } shuttle || TransportUnavailable(shuttle))
        {
            ReconcileTransport(ent);
            return;
        }
        if (HasComp<FTLComponent>(shuttle))
        {
            comp.TransportFailure = "Hospital shuttle waiting for flight cooldown.";
            return;
        }
        if (!TryGetTransportLease(shuttle, out var lease) || lease.Comp.Computer != ent.Owner ||
            !ReferenceEquals(lease.Comp.Controller, comp) || comp.ReturnDestination is not { } destination ||
            destination != lease.Comp.ReturnDestination || TransportUnavailable(destination) ||
            !TryComp<ThirdPartyDropshipReturnDestinationComponent>(destination, out var marker) || marker.Shuttle != shuttle ||
            Transform(destination).MapUid is not { } returnMap || !lease.Comp.Maps.TryGetValue(returnMap, out var originalMap) ||
            !TryComp<MapComponent>(returnMap, out var map) || !ReferenceEquals(originalMap, map))
        {
            comp.TransportFailure = "Hospital shuttle cannot depart. Restore its exact return marker and owned return map, or recover the transport.";
            return;
        }
        if (!TryStartHospitalFlight(lease, destination, null, comp.ShuttleDepartureStartupTime, comp.ShuttlePurpose))
        {
            comp.TransportFailure = lease.Comp.Failure;
            return;
        }
        comp.ExpectedDestination = destination;
        comp.TransportFailure = string.Empty;
        comp.Status = HospitalEmergencyStatus.ShuttleDeparting;
        comp.PhaseEndsAt = _timing.CurTime + TimeSpan.FromSeconds(comp.ShuttleDepartureStartupTime + comp.ShuttleTravelTime + 30);
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
            var fractureSeverity = severity switch
            {
                1 => FractureSeverity.Compound,
                2 => _random.Prob(0.7f) ? FractureSeverity.Compound : FractureSeverity.Shattered,
                _ => _random.Prob(0.85f) ? FractureSeverity.Shattered : FractureSeverity.Compound,
            };

            _bone.SeedFracture(part, fractureSeverity);
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

        if (AssessDischarge(patient).Cleared)
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
        ent.Comp.ShuttlePurpose = HospitalShuttlePurpose.PickupReturning;
        ent.Comp.Status = HospitalEmergencyStatus.WaitingForDeparture;
        TryReturnShuttle(ent);
        UpdateUi(ent);
    }

    private void SettlePickup(Entity<HospitalEmergencyComputerComponent> ent)
    {
        var missed = 0;
        var vipPenalty = 0;
        var boardedPatients = 0;
        foreach (var patient in ent.Comp.Patients)
        {
            if (Deleted(patient) || !TryComp<HospitalPatientComponent>(patient, out var patientComp) ||
                patientComp.SourceComputer != ent.Owner)
                continue;

            if (!IsPatientOnActiveShuttle(ent, patient))
            {
                RemComp<HospitalPatientComponent>(patient);
                continue;
            }

            var fatalOutcome = HasFatalOutcome(patient);
            var permanentOutcome = TryApplyPermanentDeathPenalty((patient, patientComp), ent, updateUi: false);
            var fatalOutcomeExempt = IsArrivalFatalOutcomeExempt(patientComp, fatalOutcome);
            var assessment = AssessDischarge(patient);
            var patientMissed = fatalOutcomeExempt
                ? 0
                : assessment.MissedInjuries;
            var isVip = ent.Comp.VipPatient == patient || patientComp.IsVip;

            if (isVip && !fatalOutcomeExempt && (patientMissed > 0 || assessment.TreatmentPending || permanentOutcome))
                vipPenalty += ent.Comp.VipMissedInjuryPenalty;

            if (!fatalOutcomeExempt)
            {
                if (assessment.EligibleForReward)
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

    /// <summary>Records the original occupied sites and minimum organ capabilities before injury generation.</summary>
    public void CaptureAdmissionAnatomy(Entity<HospitalPatientComponent> patient)
    {
        // An existing admission must never be overwritten after anatomy has been removed.
        if (patient.Comp.AdmissionAnatomy.Count != 0)
            return;

        foreach (var part in _medicalIndex.GetBodyParts(patient.Owner))
        {
            var organs = new List<HospitalAdmissionOrgan>();
            foreach (var slot in _medicalIndex.GetOrganSlots(part.Owner))
            {
                if (slot.Organ is { } organ && TryComp<OrganComponent>(organ, out var component))
                    organs.Add(new(slot.SlotId, component.Category, GetOrganCapabilities(organ)));
            }
            patient.Comp.AdmissionAnatomy.Add(new(part.Comp.PartType, part.Comp.Symmetry), organs);
            if (_body.GetParentPartOrNull(part.Owner) is { } parent && TryComp<BodyPartComponent>(parent, out var parentPart))
            {
                patient.Comp.AdmissionParents.Add(new(part.Comp.PartType, part.Comp.Symmetry),
                    new(parentPart.PartType, parentPart.Symmetry));
            }
        }
    }

    /// <summary>
    /// Evaluates treatment debt and recovery eligibility, including missing or incompatible admission anatomy.
    /// </summary>
    public HospitalDischargeAssessment AssessDischarge(EntityUid patient)
    {
        if (Deleted(patient))
            return new(0, true, true);

        var missed = 0;
        var missingAnatomy = false;
        var incompatibleOrgan = false;
        var organConditions = new HashSet<EntityUid>();
        if (TryComp<HospitalPatientComponent>(patient, out var hospitalPatient))
        {
            var missingParts = new HashSet<CMUMedicalBodyPartKey>();
            foreach (var key in hospitalPatient.AdmissionAnatomy.Keys)
            {
                if (!_medicalIndex.TryGetBodyPart(patient, key, out _))
                    missingParts.Add(key);
            }
            missingAnatomy = missingParts.Count > 0;
            foreach (var key in missingParts)
            {
                // Organs and descendants disappear with their containing part.
                // Legacy admissions without parents conservatively keep per-site debt.
                if (!hospitalPatient.AdmissionParents.TryGetValue(key, out var parent) || !missingParts.Contains(parent))
                    missed++;
            }
            foreach (var (key, organSlots) in hospitalPatient.AdmissionAnatomy)
            {
                if (!_medicalIndex.TryGetBodyPart(patient, key, out var part))
                    continue;

                foreach (var required in organSlots)
                {
                    if (!_medicalIndex.TryGetOrganInSlot(part, required.Slot, out var organ))
                    {
                        missingAnatomy = true;
                        missed++;
                    }
                    else if (!TryComp<OrganComponent>(organ, out var component) ||
                             component.Body != patient || component.Category != required.Category ||
                             !HasRequiredOrganCapabilities(organ, required.Capabilities))
                    {
                        // A same-category donor without the original physiology is
                        // occupied anatomy, but it cannot satisfy the recovery contract.
                        incompatibleOrgan = true;
                        organConditions.Add(organ);
                    }
                }
            }
        }

        missed += CountBillableConditions(patient, organConditions, out var treatmentPending);
        return new(missed, missingAnatomy, HasFatalOutcome(patient), incompatibleOrgan, treatmentPending);
    }

    private HospitalOrganCapabilities GetOrganCapabilities(EntityUid organ)
    {
        var capabilities = HospitalOrganCapabilities.None;
        if (HasComp<OrganHealthComponent>(organ))
            capabilities |= HospitalOrganCapabilities.Health;
        if (HasComp<HeartComponent>(organ))
            capabilities |= HospitalOrganCapabilities.Heart;
        if (HasComp<LungsComponent>(organ))
            capabilities |= HospitalOrganCapabilities.Lungs;
        if (HasComp<LiverComponent>(organ))
            capabilities |= HospitalOrganCapabilities.Liver;
        if (HasComp<KidneysComponent>(organ))
            capabilities |= HospitalOrganCapabilities.Kidneys;
        if (HasComp<CMUStomachComponent>(organ))
            capabilities |= HospitalOrganCapabilities.Stomach;
        if (HasComp<CMUBrainComponent>(organ))
            capabilities |= HospitalOrganCapabilities.Brain;
        if (HasComp<EyesComponent>(organ))
            capabilities |= HospitalOrganCapabilities.Eyes;
        if (HasComp<EarsComponent>(organ))
            capabilities |= HospitalOrganCapabilities.Ears;
        return capabilities;
    }

    private bool HasRequiredOrganCapabilities(EntityUid organ, HospitalOrganCapabilities required)
        => ((required & HospitalOrganCapabilities.Health) == 0 || HasComp<OrganHealthComponent>(organ)) &&
           ((required & HospitalOrganCapabilities.Heart) == 0 || HasComp<HeartComponent>(organ)) &&
           ((required & HospitalOrganCapabilities.Lungs) == 0 || HasComp<LungsComponent>(organ)) &&
           ((required & HospitalOrganCapabilities.Liver) == 0 || HasComp<LiverComponent>(organ)) &&
           ((required & HospitalOrganCapabilities.Kidneys) == 0 || HasComp<KidneysComponent>(organ)) &&
           ((required & HospitalOrganCapabilities.Stomach) == 0 || HasComp<CMUStomachComponent>(organ)) &&
           ((required & HospitalOrganCapabilities.Brain) == 0 || HasComp<CMUBrainComponent>(organ)) &&
           ((required & HospitalOrganCapabilities.Eyes) == 0 || HasComp<EyesComponent>(organ)) &&
           ((required & HospitalOrganCapabilities.Ears) == 0 || HasComp<EarsComponent>(organ));

    /// <summary>
    /// Counts unresolved conditions, not their redundant damage/marker projections.
    /// Clinical treatment state remains independent from this economy projection.
    /// </summary>
    private int CountBillableConditions(EntityUid patient, HashSet<EntityUid> organConditions, out bool treatmentPending)
    {
        var missed = 0;
        var remainingDamage = TryComp<DamageableComponent>(patient, out var damageable)
            ? _damage.GetAllDamage((patient, damageable)) : new DamageSpecifier();
        treatmentPending = remainingDamage.AnyPositive();
        var brute = _prototypes.Index<DamageGroupPrototype>("Brute");
        var burn = _prototypes.Index<DamageGroupPrototype>("Burn");
        TryComp<CMUSurgeryInProgressComponent>(patient, out var surgery);
        var surgerySiteSeen = false;
        var surgeryTargetMissing = surgery != null && TryComp<HospitalPatientComponent>(patient, out var admission) &&
                                   admission.AdmissionAnatomy.ContainsKey(new(surgery.TargetPartType, surgery.TargetSymmetry)) &&
                                   !_medicalIndex.TryGetBodyPart(patient, new(surgery.TargetPartType, surgery.TargetSymmetry), out _);

        foreach (var organ in _medicalIndex.GetOrgans(patient))
        {
            if (IsUnresolvedOrgan(organ.Owner))
                organConditions.Add(organ.Owner);
        }

        foreach (var (part, _) in _medicalIndex.GetBodyParts(patient))
        {
            var siteConditions = 0;
            var bruteTrauma = false;
            var burnTrauma = HasComp<CMUEscharComponent>(part);
            var structuralDeficit = false;
            var foreignBody = TryComp<CMUShrapnelComponent>(part, out var shrapnel) && shrapnel.Fragments > 0 ||
                              HasComp<CMUEmbeddedForeignBodyComponent>(part);
            var contaminated = HasComp<CMUContaminatedWoundComponent>(part);
            var openTreatment = HasComp<CMIncisionOpenComponent>(part) || HasComp<CMRibcageOpenComponent>(part);
            if (surgery?.Part == part)
            {
                openTreatment = true;
                surgerySiteSeen = true;
            }

            if (TryComp<BodyPartHealthComponent>(part, out var health))
            {
                structuralDeficit = health.Current < health.Max;
                bruteTrauma = HasPositiveGroupDamage(health.BodyDamage, brute);
                burnTrauma |= HasPositiveGroupDamage(health.BodyDamage, burn);
                // Only exact typed attribution shares units with aggregate damage.
                // Wound magnitude and HP are resistance/propagation projections.
                foreach (var (type, amount) in health.BodyDamage.DamageDict)
                {
                    if (amount > FixedPoint2.Zero)
                        remainingDamage.DamageDict[type] = FixedPoint2.Max(FixedPoint2.Zero,
                            remainingDamage.DamageDict.GetValueOrDefault(type) - amount);
                }
            }

            var externalBleeding = false;
            if (TryComp<BodyPartWoundComponent>(part, out var wounds))
            {
                externalBleeding = wounds.ExternalBleeding != ExternalBleedTier.None;
                bruteTrauma |= externalBleeding;
                foreach (var entry in _woundLedger.GetEntries(wounds))
                {
                    if (!entry.Wound.Treated || entry.Wound.Damage > FixedPoint2.Zero)
                    {
                        bruteTrauma |= entry.Wound.Type == WoundType.Brute;
                        burnTrauma |= entry.Wound.Type == WoundType.Burn;
                    }
                    foreignBody |= (entry.Cleanup & (WoundCleanupFlags.RetainedFragment | WoundCleanupFlags.CrushDebris)) != 0;
                    burnTrauma |= (entry.Cleanup & WoundCleanupFlags.CharredTissue) != 0;
                    // New wounds start with DirtyDressing: it is ordinary treatment
                    // work, not an independent contamination complication.
                    openTreatment |= (entry.Cleanup & (WoundCleanupFlags.PoorClosure | WoundCleanupFlags.DirtyDressing)) != 0;
                }
            }

            if (bruteTrauma)
                siteConditions++;
            if (burnTrauma)
                siteConditions++;
            if (structuralDeficit && !bruteTrauma && !burnTrauma)
                siteConditions++; // A scalar HP deficit cannot invent a second typed trauma.

            var boneCondition = TryComp<FractureComponent>(part, out var fracture) && fracture.Severity != FractureSeverity.None ||
                                TryComp<BoneComponent>(part, out var bone) && bone.Integrity < bone.IntegrityMax ||
                                HasComp<CMUBoneSplinteredComponent>(part);
            if (boneCondition)
                siteConditions++;
            if (foreignBody)
                siteConditions++;
            if (contaminated)
                siteConditions++;
            if (HasComp<CMUCompartmentPressureComponent>(part))
                siteConditions++;
            if (HasComp<CMUOrganAdhesionComponent>(part))
                siteConditions++;

            var organConditionOnSite = false;
            foreach (var organ in _medicalIndex.GetPartOrgans(part))
                organConditionOnSite |= organConditions.Contains(organ.Owner);

            var vascularCondition = HasComp<CMUVascularTearComponent>(part) || HasComp<CMUOrganHemorrhageComponent>(part) ||
                                    HasComp<CMUSurgicalInternalBleedingComponent>(part);
            if (TryComp<InternalBleedingComponent>(part, out var bleeding))
            {
                // Derived source tags are produced by the wounds owner. Unknown or
                // explicitly seeded vascular trauma is independent, never guessed away.
                var source = bleeding.Source;
                var represented = source.StartsWith("fracture:", StringComparison.Ordinal) && boneCondition ||
                                  source.StartsWith("organ:", StringComparison.Ordinal) && organConditionOnSite ||
                                  source == "blunt" && bruteTrauma;
                vascularCondition |= !represented;
            }
            if (vascularCondition)
                siteConditions++;

            treatmentPending |= siteConditions > 0 || openTreatment || organConditionOnSite;
            // An incision/lock describes the work already being billed on this site.
            // A clean but unclosed site is still one unresolved treatment condition.
            if (openTreatment && siteConditions == 0 && !organConditionOnSite &&
                !(surgery?.Part == part && surgeryTargetMissing))
                siteConditions++;
            missed += siteConditions;
        }

        if (surgery != null && !surgerySiteSeen)
        {
            treatmentPending = true;
            // Missing-limb workflows use an anchor; the admission deficit already
            // contributes a condition. A stale lock still blocks clinical clearance.
        }

        missed += organConditions.Count;
        if (TryComp<BloodstreamComponent>(patient, out var blood) &&
            (_bloodstream.GetBloodLevel((patient, blood)) < blood.BloodlossThreshold || blood.BleedAmount > 0))
        {
            treatmentPending = true;
            // Restoring volume is independent treatment. CMU wounds drain volume
            // directly; they do not add the separate legacy BleedAmount field.
            // Preserve one circulatory condition for either unresolved state.
            missed++;
        }

        // Unlocalized Brute/Burn still count by treatment group. Other systemic
        // damage types are independent outstanding debts: no causal organ ledger
        // exists from which to safely subtract their historical pressure.
        if (HasPositiveGroupDamage(remainingDamage, brute))
            missed++;
        if (HasPositiveGroupDamage(remainingDamage, burn))
            missed++;
        foreach (var (type, amount) in remainingDamage.DamageDict)
        {
            if (amount > FixedPoint2.Zero && !brute.DamageTypes.Contains(type) && !burn.DamageTypes.Contains(type))
                missed++;
        }
        return missed;
    }

    private bool IsUnresolvedOrgan(EntityUid organ)
        => TryComp<OrganHealthComponent>(organ, out var health) &&
           (health.Current < health.Max || health.Stage != OrganDamageStage.Healthy) ||
           TryComp<HeartComponent>(organ, out var heart) && heart.Stopped;

    private static bool HasPositiveGroupDamage(DamageSpecifier damage, DamageGroupPrototype group)
    {
        foreach (var type in group.DamageTypes)
        {
            if (damage.DamageDict.GetValueOrDefault(type) > FixedPoint2.Zero)
                return true;
        }
        return false;
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
            if (countHealed && AssessDischarge(patient).Cleared)
                healed++;
        }

        return (active, healed);
    }

    private EntityUid? FindLandingZone(Entity<HospitalEmergencyComputerComponent> ent)
    {
        EntityUid? nearest = null;
        var nearestDistance = float.MaxValue;
        var computerCoords = _transform.GetMapCoordinates(ent);
        var computerMap = computerCoords.MapId;

        var query = EntityQueryEnumerator<HospitalDropshipLandingZoneComponent, DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            var zoneCoords = _transform.GetMapCoordinates(uid, xform);
            if (zoneCoords.MapId != computerMap)
                continue;

            var distance = (zoneCoords.Position - computerCoords.Position).LengthSquared();
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = uid;
        }

        return nearest;
    }

    private bool EnsureLandingZone(Entity<HospitalEmergencyComputerComponent> ent, TimeSpan now, bool force = false)
    {
        if (!force && now < ent.Comp.NextLandingZoneRefreshAt)
            return ent.Comp.LandingZone is { } landingZone && !Deleted(landingZone) &&
                HasComp<DropshipDestinationComponent>(landingZone) &&
                Transform(landingZone).MapUid == Transform(ent).MapUid;

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
        if (ent.Comp.ActiveShuttle is { } shuttle && TryGetTransportLease(shuttle, out var lease) &&
            lease.Comp.Computer == ent.Owner && ReferenceEquals(lease.Comp.Controller, ent.Comp))
        {
            lease.Comp.Retiring = true;
            lease.Comp.Computer = null;
            lease.Comp.Controller = null;
            lease.Comp.NextAction = _timing.CurTime;
            if (!TryReclaimTransport(lease))
                lease.Comp.Failure = "Hospital transport retained for its remaining occupants and belongings.";
        }
        // Unknown ownership is never permission to delete an arbitrary grid/map.
        ClearComputerTransport(ent.Comp);
    }

    private void UpdateUi(Entity<HospitalEmergencyComputerComponent> ent)
    {
        // Opening the UI rebuilds state. Unobserved consoles do not need a full
        // anatomy/clinical assessment and BUI publication every two seconds.
        if (TransportUnavailable(ent.Owner) || !_ui.IsUiOpen(ent.Owner, HospitalEmergencyComputerUi.Key))
            return;
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
            ? (int) Math.Min(int.MaxValue, Math.Ceiling((target - now).TotalSeconds))
            : 0;
    }

    private static string GetStatusText(HospitalEmergencyComputerComponent comp, int secondsRemaining)
    {
        if (!string.IsNullOrEmpty(comp.TransportFailure))
            return comp.TransportFailure;

        return comp.Status switch
        {
            HospitalEmergencyStatus.Idle => comp.NextIncidentAt == TimeSpan.Zero
                ? "Standing by"
                : $"Standing by. Next orbital alert in {secondsRemaining} seconds.",
            HospitalEmergencyStatus.AwaitingApproval => "Hospital shuttle in orbit. Landing approval required.",
            HospitalEmergencyStatus.Arriving => "Hospital shuttle approved and inbound.",
            HospitalEmergencyStatus.ManualUnloading => "Hospital shuttle landed. Manually unload casualties.",
            HospitalEmergencyStatus.ShuttleDeparting => "Hospital shuttle departure sequence active.",
            HospitalEmergencyStatus.WaitingForDeparture => "Hospital shuttle waiting for departure clearance.",
            HospitalEmergencyStatus.WaitingForArrival => "Hospital shuttle awaiting arrival clearance; prepared patients remain aboard.",
            HospitalEmergencyStatus.Treating => "Casualties are in hospital care.",
            HospitalEmergencyStatus.PickupInbound => "Recovery shuttle inbound for patient release.",
            HospitalEmergencyStatus.PickupBoarding => "Recovered patients are boarding the pickup shuttle.",
            HospitalEmergencyStatus.RewardReady => "Incident complete. Any earned payment has been dispensed.",
            _ => "Standing by",
        };
    }
}
