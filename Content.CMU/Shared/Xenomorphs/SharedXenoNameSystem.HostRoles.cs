using Content.Shared._RMC14.Roles;
using Content.Shared._RMC14.Synth;
using Content.Shared.Humanoid;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Xenonids.Name;

public abstract partial class SharedXenoNameSystem
{
    [Dependency] private IPrototypeManager _hostPrototypes = default!;

    private string? GetHostRoleName(EntityUid host)
    {
        if (!HasComp<HumanoidProfileComponent>(host)
            || HasComp<SynthComponent>(host)
            || !TryComp<OriginalRoleComponent>(host, out var original)
            || !_hostPrototypes.TryIndex(original.Job, out var job))
            return null;

        // Faction variants inherit RoundRole. Legacy RMC jobs use their prototype IDs.
        var name = (job.RoundRole ?? job.ID) switch
        {
            "PlatoonCommander" or "BrigadierGeneral" or "CMCommandingOfficer" => "empress",
            "ExecutiveOfficer" or "CMExecutiveOfficer" => "queen",
            // The RadioTelephoneOperator family is the FTL, not the RadioTrafficOperator (RTO).
            "SquadSergeant" or "SectionSergeant" or "RadioTelephoneOperator" or "VehicleCommander"
                or "CLFCellLeader" or "CMSquadLeader" or "CMFireteamLeader" => "leader",
            "RadioTrafficOperator" or "CLFRadioOperator" => "antennae",
            "SquadAutomaticRifleman" or "CLFMachineGunner" or "CMSmartGunOperator" => "fast-spitter",
            "WeaponsSpecialist" or "CMWeaponsSpecialist" => "heavy-spitter",
            "SquadRifleman" or "CLFGuerilla" or "ColonyPanicRoomCommando" or "CMRifleman"
                or "RMCRecruit" => "warrior",
            "PlatoonCorpsman" or "MilitaryDoctor" or "Nurse" or "CLFPhysician" or "CLFSurgeon"
                or "CivilianPhysician" or "CivilianNurse" or "CivilianEmergencyResponseOfficer"
                or "ColonyMinistryDoctor" or "ColonyPMCMedic" or "CMHospitalCorpsman"
                or "CMDoctor" or "CMNurse" or "RMCFieldDoctor" => "healer",
            "CMO" or "CivilianHeadPhysician" or "CMCMO" => "elder-healer",
            "SquadCombatTech" or "EngineeringTech" or "CivilianEngineer" or "CivilianAtmosian"
                or "ColonyReactorTechnician" or "CMCombatTech" or "CMMaintTech" => "builder",
            "EngineeringOfficer" or "CivilianHeadOfEngineering" or "CivilianForeman"
                or "CMChiefEngineer" => "elder-builder",
            "CMOrdnanceTech" => "fire-maker",
            "DropshipPilot" or "CivilianColonyPilot" or "CMPilotDropship" or "CMPilotGunship" => "sky",
            "DropshipCrewChief" or "AuxTech" or "CMDropshipCrewChief" => "sky-keeper",
            "VehicleCrewman" => "shell",
            "DroneOperator" => "puppeteer",
            "IntelOfficer" or "CMIntelOfficer" => "seeker",
            "JuniorOfficer" or "AdjutantDress" or "CMStaffOfficer" or "CMAuxiliarySupportOfficer" => "hive-mind",
            "Advisor" or "CMSeniorEnlistedAdvisor" => "elder",
            "LogisticsOfficer" or "CivilianFreightSystemsSpecialist" or "CivilianShopkeep"
                or "CMQuartermaster" or "CMCargoTech" => "provider",
            "MilitaryPolice" or "ChiefMP" or "VipEscort" or "WYGuard" or "CivilianProdigyGuard"
                or "CivilianColonySecurityOfficer" or "CivilianColonySecurityLeader"
                or "CivilianCMBDeputy" or "CivilianCMBMarshal" or "CivilianCWPSRanger"
                or "CivilianKellandWarden" or "CivilianNSPAConstable" or "CivilianNSPAInspector"
                or "CivilianOrbitalSecurity" or "CivilianPaPOfficer" or "ColonyPrisonGuard"
                or "ColonyRiotControlOfficer" or "ColonyRiotControlTeamLeader"
                or "CMChiefMP" or "CMMilitaryPolice" or "CMMilitaryWarden" => "guard",
            "CivilianScientist" or "ColonyXenobiologist" or "ColonyXenoarchaeologist"
                or "CMResearcher" => "experimenter",
            "CivilianColonyAdministrator" or "CivilianCorporateAdministrator"
                or "CivilianProdigySupervisor" or "CivilianOrbitalManager" => "hive-keeper",
            "CivilianMiner" => "burrower",
            "CivilianFoodServiceWorker" => "feeder",
            "CivilianWasteManagementSpecialist" => "cleaner",
            "CivilianJournalist" or "CombatCorrespondent" => "watcher",
            "MobBoss" or "MobGoon" => "rogue",
            "CivilianColonist" or "CivilianPrisoner" or "CivilianColonyAdminAssistant"
                or "CivilianCorporateAdminAssistant" or "CivilianCorporateAssistant"
                or "CivilianCorporateLiaison" or "CivilianProdigyLiaison" or "CivilianLaselleLiaison"
                or "CivilianEthicsAndWellnessAdvisor" or "CivilianHeadOfService"
                or "CivilianOrbitalArbiter" or "CivilianOrbitalLawyer" or "CivilianUSASFRecruiter"
                or "CivilianAmbassadorCCA" or "CivilianAmbassadorICSC" or "CivilianAmbassadorTWE"
                or "CivilianAmbassadorUA" or "CivilianAmbassadorUPP" or "ColonyChaplain" => "worker",
            _ => null,
        };

        return name == null ? null : Loc.GetString($"cmu-xeno-host-{name}");
    }
}
