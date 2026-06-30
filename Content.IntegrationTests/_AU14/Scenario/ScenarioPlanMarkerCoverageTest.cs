using System.Collections.Generic;
using System.Linq;
using Content.Server._CMU14.Threats;
using Content.Server.Administration;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.AU14.Scenario.Commands;
using Content.Server._CMU14.Ops.ThirdParty;
using Content.Server.GameTicking.Presets;
using Content.Server.Spawners.Components;
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.Rules;
using Content.Shared.Administration;
using Content.Shared.AU14;
using Content.Shared.AU14.Scenario;
using Content.Shared._CMU14.Threats;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using ParachuteMarkerComponent = Content.Shared._CMU14.Threats.ParachuteMarkerComponent;
using ThirdPartySystem = Content.Server._CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.IntegrationTests._AU14.Scenario;

[TestFixture]
public sealed class ScenarioPlanMarkerCoverageTest
{
    private const int MarkerValidationPlayerCount = 100;
    private const string SelectedThreatAssignmentPreset = "Prometheus";
    private const string ColonyCivilianJobId = "AU14JobCivilianColonist";
    private const string StandaloneCooldownMarkerId = "scenario-plan-cooldown";
    private const string InsurgencyPreset = "Insurgency";
    private const string DistressSignalPreset = "DistressSignal";
    private const string ColonyFallPreset = "ColonyFall";
    private const string DistressSignalSeededSlicePlanetId = "CMUPlanetHopesRetreat";
    private const string ColonyFallSeededSlicePlanetId = "AUPlanetShepherdsPride";
    private static readonly ProtoId<ThreatPrototype> SelectedThreatAssignmentThreat = "XenoThreat";
    private static readonly ProtoId<ThreatPrototype> DistressSignalAbominationThreat = "abominationsThreatDistress";
    private static readonly ProtoId<ThreatPrototype> ColonyFallCultistThreat = "cultistThreat";
    private static readonly ProtoId<ThreatPrototype> ColonyFallXenoThreat = "XenoThreatCF";
    private static readonly ProtoId<ThreatPrototype> ColonyFallAbominationThreat = "abominationsThreat";
    private static readonly ProtoId<ThreatPrototype> CustomMarkerThreat = "cultistThreatOnMarker";
    private static readonly ProtoId<ThreatPrototype> WendigoThreat = "wendigoThreat";
    private static readonly ProtoId<ThirdPartyPrototype> GroundThirdParty = "USArmyAlt";
    private const string StandaloneGroundThirdParty = "ScenarioPlanStandaloneThirdParty";
    private const string StandaloneCooldownThirdParty = "ScenarioPlanStandaloneCooldownThirdParty";
    private const string ParachuteThirdParty = "ScenarioPlanParachuteThirdParty";
    private static readonly ProtoId<ThirdPartyPrototype> DropshipThirdParty = "WYPMCParty";
    private static readonly EntProtoId ThreatEntityMarker = "threatentityspawnmarker";
    private static readonly EntProtoId ThreatLeaderMarker = "threatleaderspawnmarker";
    private static readonly EntProtoId ThreatMemberMarker = "threatmemberspawnmarker";
    private static readonly EntProtoId ScenarioThreatLeaderMarker = "scenariothreatleaderspawnmarker";
    private static readonly EntProtoId ScenarioThreatMemberMarker = "scenariothreatmemberspawnmarker";
    private static readonly EntProtoId XenoCfEntityMarker = "xenocfthreatentityspawnmarker";
    private static readonly EntProtoId XenoCfLeaderMarker = "xenocfthreatleaderspawnmarker";
    private static readonly EntProtoId XenoCfMemberMarker = "xenocfthreatmemberspawnmarker";
    private static readonly EntProtoId ThirdPartyEntityMarker = "thirdpartyentityspawnmarker";
    private static readonly EntProtoId ThirdPartyLeaderMarker = "thirdpartyleaderspawnmarker";
    private static readonly EntProtoId ThirdPartyMemberMarker = "thirdpartymemberspawnmarker";
    private const string StandaloneThirdPartyLeaderMarker = "ScenarioPlanStandaloneThirdPartyLeaderMarker";
    private const string StandaloneThirdPartyMemberMarker = "ScenarioPlanStandaloneThirdPartyMemberMarker";
    private const string StandaloneThirdPartyCooldownLeaderMarker = "ScenarioPlanStandaloneThirdPartyCooldownLeaderMarker";
    private const string StandaloneThirdPartyCooldownMemberMarker = "ScenarioPlanStandaloneThirdPartyCooldownMemberMarker";
    private const string StandaloneClfCivilianMarker = "ScenarioPlanStandaloneClfCivilianMarker";
    private static readonly EntProtoId ScenarioClfSafehouseMarker = "scenarioclfsafehousespawnmarker";
    private static readonly EntProtoId CultistCfLeaderMarker = "cultistcfthreatleaderspawnmarker";
    private static readonly EntProtoId CultistCfMemberMarker = "cultistcfthreatmemberspawnmarker";
    private static readonly EntProtoId ClfCellLeaderSpawnPoint = "AU14SpawnPointCLFCellLeader";
    private static readonly EntProtoId CivilianColonistSpawnPoint = "AU14SpawnPointCivilianColonist";
    private static readonly EntProtoId ScenarioClfCivilianColonistSpawnMarker = "ScenarioClfCivilianColonistSpawnMarker";
    private static readonly EntProtoId SafehouseMarker = "safehousemarker";
    private const string PrototypeShapeVotingChoices = "ScenarioPlanPrototypeShapeVotingChoices";
    private const string PrototypeShapeRoundGroup = "ScenarioPlanPrototypeShapeRoundGroup";
    private const string PrototypeShapeSpawnMarker = "ScenarioPlanPrototypeShapeSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThreatLeaderGenericSpawnMarker = "ThreatLeaderGenericSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThreatMemberGenericSpawnMarker = "ThreatMemberGenericSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThreatEntityGenericSpawnMarker = "ThreatEntityGenericSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThreatLeaderXenoCfSpawnMarker = "ThreatLeaderXenoCfSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThreatMemberXenoCfSpawnMarker = "ThreatMemberXenoCfSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThreatEntityXenoCfSpawnMarker = "ThreatEntityXenoCfSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThreatLeaderCultistCfSpawnMarker = "ThreatLeaderCultistCfSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThreatMemberCultistCfSpawnMarker = "ThreatMemberCultistCfSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThirdPartyLeaderGenericSpawnMarker = "ThirdPartyLeaderGenericSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThirdPartyMemberGenericSpawnMarker = "ThirdPartyMemberGenericSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ThirdPartyEntityGenericSpawnMarker = "ThirdPartyEntityGenericSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ClfSafehouseSpawnMarker = "ClfSafehouseSpawnMarker";
    private static readonly ProtoId<SpawnMarkerPrototype> ClfCivilianColonistSpawnMarker = "ClfCivilianColonistSpawnMarker";
    private static readonly ProtoId<VotingChoicesPrototype> InsurgencyVotingChoices = "InsurgencyVotingChoices";
    private static readonly ProtoId<VotingChoicesPrototype> DistressSignalVotingChoices = "DistressSignalVotingChoices";
    private static readonly ProtoId<VotingChoicesPrototype> ColonyFallVotingChoices = "ColonyFallVotingChoices";
    private static readonly ProtoId<RoundGroupPrototype> InsurgencyClfRoundGroup = "InsurgencyClfRoundGroup";
    private const string InsurgencyClfSpawnPlan = "InsurgencyClfSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> GovforUppPlatoonRoundGroup = "GovforUppPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> GovforUscmPlatoonRoundGroup = "GovforUscmPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> GovforRmcPlatoonRoundGroup = "GovforRmcPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> GovforLacnPlatoonRoundGroup = "GovforLacnPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> GovforCmbciuPlatoonRoundGroup = "GovforCmbciuPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> GovforWeyuPlatoonRoundGroup = "GovforWeyuPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> GovforHazopsPlatoonRoundGroup = "GovforHazopsPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> GovforVaipoPlatoonRoundGroup = "GovforVaipoPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> GovforProdigySfPlatoonRoundGroup = "GovforProdigySfPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> OpforUppPlatoonRoundGroup = "OpforUppPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> OpforWeyuPlatoonRoundGroup = "OpforWeyuPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> OpforVaipoPlatoonRoundGroup = "OpforVaipoPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> OpforProdigySfPlatoonRoundGroup = "OpforProdigySfPlatoonRoundGroup";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalXenoRoundGroup = "DistressSignalXenoRoundGroup";
    private const string DistressSignalXenoSpawnPlan = "DistressSignalXenoSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalAbominationRoundGroup = "DistressSignalAbominationRoundGroup";
    private const string DistressSignalAbominationSpawnPlan = "DistressSignalAbominationSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallCultistRoundGroup = "ColonyFallCultistRoundGroup";
    private const string ColonyFallCultistSpawnPlan = "ColonyFallCultistSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallCultistMarkerRoundGroup = "ColonyFallCultistMarkerRoundGroup";
    private const string ColonyFallCultistMarkerSpawnPlan = "ColonyFallCultistMarkerSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallXenoRoundGroup = "ColonyFallXenoRoundGroup";
    private const string ColonyFallXenoSpawnPlan = "ColonyFallXenoSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallWendigoRoundGroup = "ColonyFallWendigoRoundGroup";
    private const string ColonyFallWendigoSpawnPlan = "ColonyFallWendigoSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallAbominationRoundGroup = "ColonyFallAbominationRoundGroup";
    private const string ColonyFallAbominationSpawnPlan = "ColonyFallAbominationSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalWypmcPartyRoundGroup = "DistressSignalWypmcPartyRoundGroup";
    private const string DistressSignalWypmcPartySpawnPlan = "DistressSignalWypmcPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalWypmcPartyAltRoundGroup = "DistressSignalWypmcPartyAltRoundGroup";
    private const string DistressSignalWypmcPartyAltSpawnPlan = "DistressSignalWypmcPartyAltSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUsArmyPartyRoundGroup = "DistressSignalUsArmyPartyRoundGroup";
    private const string DistressSignalUsArmyPartySpawnPlan = "DistressSignalUsArmyPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUsArmyAltPartyRoundGroup = "DistressSignalUsArmyAltPartyRoundGroup";
    private const string DistressSignalUsArmyAltPartySpawnPlan = "DistressSignalUsArmyAltPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUsArmyAlt2PartyRoundGroup = "DistressSignalUsArmyAlt2PartyRoundGroup";
    private const string DistressSignalUsArmyAlt2PartySpawnPlan = "DistressSignalUsArmyAlt2PartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUppGromPartyRoundGroup = "DistressSignalUppGromPartyRoundGroup";
    private const string DistressSignalUppGromPartySpawnPlan = "DistressSignalUppGromPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUppGromAltPartyRoundGroup = "DistressSignalUppGromAltPartyRoundGroup";
    private const string DistressSignalUppGromAltPartySpawnPlan = "DistressSignalUppGromAltPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUppGromAlt2PartyRoundGroup = "DistressSignalUppGromAlt2PartyRoundGroup";
    private const string DistressSignalUppGromAlt2PartySpawnPlan = "DistressSignalUppGromAlt2PartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalForeconPartyRoundGroup = "DistressSignalForeconPartyRoundGroup";
    private const string DistressSignalForeconPartySpawnPlan = "DistressSignalForeconPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalForeconAltPartyRoundGroup = "DistressSignalForeconAltPartyRoundGroup";
    private const string DistressSignalForeconAltPartySpawnPlan = "DistressSignalForeconAltPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalForeconAlt2PartyRoundGroup = "DistressSignalForeconAlt2PartyRoundGroup";
    private const string DistressSignalForeconAlt2PartySpawnPlan = "DistressSignalForeconAlt2PartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalIasfSurvLargePartyRoundGroup = "DistressSignalIasfSurvLargePartyRoundGroup";
    private const string DistressSignalIasfSurvLargePartySpawnPlan = "DistressSignalIasfSurvLargePartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalIasfSurvMediumPartyRoundGroup = "DistressSignalIasfSurvMediumPartyRoundGroup";
    private const string DistressSignalIasfSurvMediumPartySpawnPlan = "DistressSignalIasfSurvMediumPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalIasfSurvSmallPartyRoundGroup = "DistressSignalIasfSurvSmallPartyRoundGroup";
    private const string DistressSignalIasfSurvSmallPartySpawnPlan = "DistressSignalIasfSurvSmallPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalWeyuSurvLargePartyRoundGroup = "DistressSignalWeyuSurvLargePartyRoundGroup";
    private const string DistressSignalWeyuSurvLargePartySpawnPlan = "DistressSignalWeyuSurvLargePartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalWeyuSurvMediumPartyRoundGroup = "DistressSignalWeyuSurvMediumPartyRoundGroup";
    private const string DistressSignalWeyuSurvMediumPartySpawnPlan = "DistressSignalWeyuSurvMediumPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalWeyuSurvSmallPartyRoundGroup = "DistressSignalWeyuSurvSmallPartyRoundGroup";
    private const string DistressSignalWeyuSurvSmallPartySpawnPlan = "DistressSignalWeyuSurvSmallPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalApePartyRoundGroup = "DistressSignalApePartyRoundGroup";
    private const string DistressSignalApePartySpawnPlan = "DistressSignalApePartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalTimeSharesSalesmanPartyRoundGroup = "DistressSignalTimeSharesSalesmanPartyRoundGroup";
    private const string DistressSignalTimeSharesSalesmanPartySpawnPlan = "DistressSignalTimeSharesSalesmanPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalProfessorVonBandolierPartyRoundGroup = "DistressSignalProfessorVonBandolierPartyRoundGroup";
    private const string DistressSignalProfessorVonBandolierPartySpawnPlan = "DistressSignalProfessorVonBandolierPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUppTdLostTeamPartyRoundGroup = "DistressSignalUppTdLostTeamPartyRoundGroup";
    private const string DistressSignalUppTdLostTeamPartySpawnPlan = "DistressSignalUppTdLostTeamPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalLacnSniperTeamPartyRoundGroup = "DistressSignalLacnSniperTeamPartyRoundGroup";
    private const string DistressSignalLacnSniperTeamPartySpawnPlan = "DistressSignalLacnSniperTeamPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalForeconFirstReconPartyRoundGroup = "DistressSignalForeconFirstReconPartyRoundGroup";
    private const string DistressSignalForeconFirstReconPartySpawnPlan = "DistressSignalForeconFirstReconPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalRmcRrtPartyRoundGroup = "DistressSignalRmcRrtPartyRoundGroup";
    private const string DistressSignalRmcRrtPartySpawnPlan = "DistressSignalRmcRrtPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalCmbInvestigationPartyRoundGroup = "DistressSignalCmbInvestigationPartyRoundGroup";
    private const string DistressSignalCmbInvestigationPartySpawnPlan = "DistressSignalCmbInvestigationPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalCmbInvestigationPartyAltRoundGroup = "DistressSignalCmbInvestigationPartyAltRoundGroup";
    private const string DistressSignalCmbInvestigationPartyAltSpawnPlan = "DistressSignalCmbInvestigationPartyAltSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallCmbParoleeTransportPartyRoundGroup = "ColonyFallCmbParoleeTransportPartyRoundGroup";
    private const string ColonyFallCmbParoleeTransportPartySpawnPlan = "ColonyFallCmbParoleeTransportPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallCmbParoleeTransportBigPartyRoundGroup = "ColonyFallCmbParoleeTransportBigPartyRoundGroup";
    private const string ColonyFallCmbParoleeTransportBigPartySpawnPlan = "ColonyFallCmbParoleeTransportBigPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallIpiePartyRoundGroup = "ColonyFallIpiePartyRoundGroup";
    private const string ColonyFallIpiePartySpawnPlan = "ColonyFallIpiePartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallIpieSynthPartyRoundGroup = "ColonyFallIpieSynthPartyRoundGroup";
    private const string ColonyFallIpieSynthPartySpawnPlan = "ColonyFallIpieSynthPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallWyhtPartyRoundGroup = "ColonyFallWyhtPartyRoundGroup";
    private const string ColonyFallWyhtPartySpawnPlan = "ColonyFallWyhtPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallMissionariesPartyRoundGroup = "ColonyFallMissionariesPartyRoundGroup";
    private const string ColonyFallMissionariesPartySpawnPlan = "ColonyFallMissionariesPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallMissionariesPartyMediumRoundGroup = "ColonyFallMissionariesPartyMediumRoundGroup";
    private const string ColonyFallMissionariesPartyMediumSpawnPlan = "ColonyFallMissionariesPartyMediumSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallMigratingCivsPartyRoundGroup = "ColonyFallMigratingCivsPartyRoundGroup";
    private const string ColonyFallMigratingCivsPartySpawnPlan = "ColonyFallMigratingCivsPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallMigratingCivsMediumPartyRoundGroup = "ColonyFallMigratingCivsMediumPartyRoundGroup";
    private const string ColonyFallMigratingCivsMediumPartySpawnPlan = "ColonyFallMigratingCivsMediumPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallMigratingCivsLargePartyRoundGroup = "ColonyFallMigratingCivsLargePartyRoundGroup";
    private const string ColonyFallMigratingCivsLargePartySpawnPlan = "ColonyFallMigratingCivsLargePartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallDrugSmugglerPartyRoundGroup = "ColonyFallDrugSmugglerPartyRoundGroup";
    private const string ColonyFallDrugSmugglerPartySpawnPlan = "ColonyFallDrugSmugglerPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallForeignCrimeGangPartyRoundGroup = "ColonyFallForeignCrimeGangPartyRoundGroup";
    private const string ColonyFallForeignCrimeGangPartySpawnPlan = "ColonyFallForeignCrimeGangPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallForeconKillTeamPartyRoundGroup = "ColonyFallForeconKillTeamPartyRoundGroup";
    private const string ColonyFallForeconKillTeamPartySpawnPlan = "ColonyFallForeconKillTeamPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallXenoParasiteRunnerPartyRoundGroup = "ColonyFallXenoParasiteRunnerPartyRoundGroup";
    private const string ColonyFallXenoParasiteRunnerPartySpawnPlan = "ColonyFallXenoParasiteRunnerPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> ColonyFallCarpInvasivePartyRoundGroup = "ColonyFallCarpInvasivePartyRoundGroup";
    private const string ColonyFallCarpInvasivePartySpawnPlan = "ColonyFallCarpInvasivePartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalPapInvestigationPartyRoundGroup = "DistressSignalPapInvestigationPartyRoundGroup";
    private const string DistressSignalPapInvestigationPartySpawnPlan = "DistressSignalPapInvestigationPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalPapInvestigationPartyAltRoundGroup = "DistressSignalPapInvestigationPartyAltRoundGroup";
    private const string DistressSignalPapInvestigationPartyAltSpawnPlan = "DistressSignalPapInvestigationPartyAltSpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalNspaInvestigationPartyRoundGroup = "DistressSignalNspaInvestigationPartyRoundGroup";
    private const string DistressSignalNspaInvestigationPartySpawnPlan = "DistressSignalNspaInvestigationPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUacgLostTeamPartyRoundGroup = "DistressSignalUacgLostTeamPartyRoundGroup";
    private const string DistressSignalUacgLostTeamPartySpawnPlan = "DistressSignalUacgLostTeamPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUsArmyArmoredPartyRoundGroup = "DistressSignalUsArmyArmoredPartyRoundGroup";
    private const string DistressSignalUsArmyArmoredPartySpawnPlan = "DistressSignalUsArmyArmoredPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalUsArmyTankPartyRoundGroup = "DistressSignalUsArmyTankPartyRoundGroup";
    private const string DistressSignalUsArmyTankPartySpawnPlan = "DistressSignalUsArmyTankPartySpawnPlan";
    private static readonly ProtoId<RoundGroupPrototype> DistressSignalIcrcReliefPartyRoundGroup = "DistressSignalIcrcReliefPartyRoundGroup";
    private const string DistressSignalIcrcReliefPartySpawnPlan = "DistressSignalIcrcReliefPartySpawnPlan";
    private static readonly EntProtoId AddClfRule = "AddClf";
    private const string ShadowFallbackPreset = "DistressSignal";
    private const string ShadowFallbackPlanet = "AUPlanetSorokyne";
    private const string ShadowFallbackMap = "ScenarioPlanShadowFallbackMap";

    [TestPrototypes]
    private const string ScenarioPlanTestPrototypes = @"
- type: partySpawn
  id: ScenarioPlanStandaloneThirdPartySpawn
  leadersToSpawn:
    MobHuman: 1
  gruntsToSpawn:
    MobHuman: 1
  spawnTogether: false

- type: thirdParty
  id: ScenarioPlanStandaloneThirdParty
  displayName: Scenario Plan Standalone Third Party
  partyspawn: ScenarioPlanStandaloneThirdPartySpawn
  whitelistedgamemodes:
    - DistressSignal
  whitelistedThreats:
    - XenoThreat
  entrymethod: ground
  weight: 1
  minplayers: 0
  maxplayers: 400
  GhostsNeeded: 0

- type: partySpawn
  id: ScenarioPlanStandaloneCooldownThirdPartySpawn
  leadersToSpawn:
    MobHuman: 1
  gruntsToSpawn:
    MobHuman: 1
  spawnTogether: false
  Markers:
    Leader: scenario-plan-cooldown
    Member: scenario-plan-cooldown

- type: thirdParty
  id: ScenarioPlanStandaloneCooldownThirdParty
  displayName: Scenario Plan Standalone Cooldown Third Party
  partyspawn: ScenarioPlanStandaloneCooldownThirdPartySpawn
  whitelistedgamemodes:
    - DistressSignal
  whitelistedThreats:
    - XenoThreat
  entrymethod: ground
  weight: 1
  minplayers: 0
  maxplayers: 400
  GhostsNeeded: 0

- type: partySpawn
  id: ScenarioPlanParachuteThirdPartySpawn
  leadersToSpawn:
    MobHuman: 1
  gruntsToSpawn:
    MobHuman: 1
  spawnTogether: false

- type: thirdParty
  id: ScenarioPlanParachuteThirdParty
  displayName: Scenario Plan Parachute Third Party
  partyspawn: ScenarioPlanParachuteThirdPartySpawn
  whitelistedgamemodes:
    - DistressSignal
  whitelistedThreats:
    - XenoThreat
  entrymethod: parachute
  weight: 1
  minplayers: 0
  maxplayers: 400
  GhostsNeeded: 0

- type: entity
  parent: MarkerBase
  id: ScenarioPlanStandaloneThirdPartyLeaderMarker
  name: scenario plan standalone third party leader marker
  components:
  - type: ScenarioSpawnMarker
    kind: ThirdPartyMarker
    tags:
    - force:third-party
    - bucket:Leader
    - marker-id:<generic>
  - type: ScenarioSpawnMarkerCooldown

- type: entity
  parent: MarkerBase
  id: ScenarioPlanStandaloneThirdPartyMemberMarker
  name: scenario plan standalone third party member marker
  components:
  - type: ScenarioSpawnMarker
    kind: ThirdPartyMarker
    tags:
    - force:third-party
    - bucket:Member
    - marker-id:<generic>
  - type: ScenarioSpawnMarkerCooldown

- type: entity
  parent: MarkerBase
  id: ScenarioPlanStandaloneThirdPartyCooldownLeaderMarker
  name: scenario plan standalone third party cooldown leader marker
  components:
  - type: ScenarioSpawnMarker
    kind: ThirdPartyMarker
    tags:
    - force:third-party
    - bucket:Leader
    - marker-id:scenario-plan-cooldown
  - type: ScenarioSpawnMarkerCooldown

- type: entity
  parent: MarkerBase
  id: ScenarioPlanStandaloneThirdPartyCooldownMemberMarker
  name: scenario plan standalone third party cooldown member marker
  components:
  - type: ScenarioSpawnMarker
    kind: ThirdPartyMarker
    tags:
    - force:third-party
    - bucket:Member
    - marker-id:scenario-plan-cooldown
  - type: ScenarioSpawnMarkerCooldown

- type: entity
  parent: MarkerBase
  id: ScenarioPlanStandaloneClfSafehouseMarker
  name: scenario plan standalone CLF safehouse marker
  components:
  - type: ScenarioSpawnMarker
    kind: ClfSafehouse
    tags:
    - force:clf:safehouse

- type: entity
  parent: MarkerBase
  id: ScenarioPlanStandaloneClfCivilianMarker
  name: scenario plan standalone CLF civilian marker
  components:
  - type: ScenarioSpawnMarker
    kind: ClfCivilianSpawn
    tags:
    - force:clf:civilian-spawn:AU14JobCivilianColonist
";

    [TestPrototypes]
    private const string ScenarioPlanFallbackTestPrototypes = @"
- type: roundGroup
  id: ScenarioPlanFallbackXenoRoundGroup
  kind: Hostile
  sourcePrototypeId: XenoThreat
  spawn:
    bodyBuckets:
      - bucket: Leader
        count: 1
        bodies:
          CMXenoQueen: 1
      - bucket: Member
        count: 1
        bodies:
          CMXenoLarva: 1
    allowsUnderfill: true
  winConditionRuleIds:
    - KillAllGovforRule
    - KillAllXenoRule

- type: gameMap
  id: ScenarioPlanShadowFallbackMap
  mapName: Scenario Plan Shadow Fallback Test Map
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations: {}

- type: votingChoices
  id: AScenarioPlanThreatVotingBackupChoices
  preset: DistressSignal
  planets:
    - AUPlanetSorokyne
  threatVote:
    candidates:
      - ScenarioPlanFallbackXenoRoundGroup

- type: votingBackup
  id: AScenarioPlanThreatFallback
  preset: DistressSignal
  setup: AScenarioPlanThreatVotingBackupChoices
  planets:
    - AUPlanetSorokyne

- type: votingChoices
  id: ScenarioPlanShadowVotingBackupChoices
  preset: DistressSignal
  planets:
    - AUPlanetSorokyne

- type: votingBackup
  id: ScenarioPlanShadowFallback
  preset: DistressSignal
  setup: ScenarioPlanShadowVotingBackupChoices
  planets:
    - AUPlanetSorokyne
";

    private const string ScenarioPrototypeShapeTestPrototypes = @"
- type: roundGroup
  id: ScenarioPlanPrototypeShapeRoundGroup
  kind: Hostile
  sourcePrototypeId: XenoThreat
  spawn:
    bodyBuckets:
      - bucket: Leader
        count: 1
        bodies:
          MobHuman: 1
    MarkerRequirements:
      - bucket: Leader
        requiredBodyCount: 1
        RequiredMarkerCount: 1
        requiredTags:
          - force:hostile
          - bucket:Leader
    allowsUnderfill: true
  winConditionRuleIds:
    - KillAllGovforRule
  timing:
    delayMinSeconds: 1200
    delayMaxSeconds: 2400

- type: votingChoices
  id: ScenarioPlanPrototypeShapeVotingChoices
  preset: DistressSignal
  planets:
    - LV624
  groups:
    - ScenarioPlanPrototypeShapeRoundGroup
  threatVote:
    candidates:
      - ScenarioPlanPrototypeShapeRoundGroup

- type: spawnMarker
  id: ScenarioPlanPrototypeShapeSpawnMarker
  kind: ThreatMarker
  tags:
    - force:hostile
    - bucket:Leader
  count: 1
";

    private sealed record LegacyThreatCandidate(string ThreatId, ThreatVoteBodyCount BodyCount);

    private static IEnumerable<TestCaseData> ExactThreatSliceCases()
    {
        yield return new TestCaseData(
                DistressSignalVotingChoices.Id,
                DistressSignalPreset,
                "AUPlanetCorsatStation",
                new[] { SelectedThreatAssignmentThreat.Id })
            .SetName("DistressSignal Corsat xeno-only package slice matches adapter");
        yield return new TestCaseData(
                DistressSignalVotingChoices.Id,
                DistressSignalPreset,
                "AUPlanetTrijent",
                new[] { SelectedThreatAssignmentThreat.Id })
            .SetName("DistressSignal Trijent xeno-only package slice matches adapter");
        yield return new TestCaseData(
                DistressSignalVotingChoices.Id,
                DistressSignalPreset,
                "AuPlanetKutjevo",
                new[] { SelectedThreatAssignmentThreat.Id })
            .SetName("DistressSignal Kutjevo xeno-only package slice matches adapter");
        yield return new TestCaseData(
                DistressSignalVotingChoices.Id,
                DistressSignalPreset,
                "AuPlanetShivasSnowball",
                new[] { SelectedThreatAssignmentThreat.Id })
            .SetName("DistressSignal Shivas xeno-only package slice matches adapter");
        yield return new TestCaseData(
                DistressSignalVotingChoices.Id,
                DistressSignalPreset,
                "AUPlanetSorokyne",
                new[] { SelectedThreatAssignmentThreat.Id, DistressSignalAbominationThreat.Id })
            .SetName("DistressSignal Sorokyne xeno-abomination package slice matches adapter");
        yield return new TestCaseData(
                DistressSignalVotingChoices.Id,
                DistressSignalPreset,
                "AUPlanetLV327",
                new[] { SelectedThreatAssignmentThreat.Id, DistressSignalAbominationThreat.Id })
            .SetName("DistressSignal LV327 xeno-abomination package slice matches adapter");
        yield return new TestCaseData(
                DistressSignalVotingChoices.Id,
                DistressSignalPreset,
                "AuPlanetChances",
                new[] { DistressSignalAbominationThreat.Id, SelectedThreatAssignmentThreat.Id })
            .SetName("DistressSignal Chances abomination-xeno package slice matches adapter");
        yield return new TestCaseData(
                ColonyFallVotingChoices.Id,
                ColonyFallPreset,
                "AUPlanetStableGarrison",
                new[] { ColonyFallCultistThreat.Id, ColonyFallXenoThreat.Id, WendigoThreat.Id, ColonyFallAbominationThreat.Id })
            .SetName("ColonyFall Stable Garrison package slice matches adapter");
        yield return new TestCaseData(
                ColonyFallVotingChoices.Id,
                ColonyFallPreset,
                "AUPlanetLV327",
                new[] { ColonyFallCultistThreat.Id, ColonyFallXenoThreat.Id, WendigoThreat.Id })
            .SetName("ColonyFall LV327 package slice matches adapter");
        yield return new TestCaseData(
                ColonyFallVotingChoices.Id,
                ColonyFallPreset,
                "AUPlanetSorokyne",
                new[] { ColonyFallCultistThreat.Id, WendigoThreat.Id })
            .SetName("ColonyFall Sorokyne package slice matches adapter");
        yield return new TestCaseData(
                ColonyFallVotingChoices.Id,
                ColonyFallPreset,
                "CMUPlanetLament",
                new[] { CustomMarkerThreat.Id, ColonyFallXenoThreat.Id, WendigoThreat.Id, ColonyFallAbominationThreat.Id })
            .SetName("ColonyFall Lament marker-cultist package slice matches adapter");
        yield return new TestCaseData(
                ColonyFallVotingChoices.Id,
                ColonyFallPreset,
                "CMUPlanetHopesRetreat",
                new[] { CustomMarkerThreat.Id, ColonyFallAbominationThreat.Id })
            .SetName("ColonyFall Hope's Retreat marker-cultist package slice matches adapter");
    }

    private static IEnumerable<TestCaseData> RemainingGovforPlatoonRoundGroupCases()
    {
        yield return new TestCaseData("AUPlanetCorsatStation", "LACN", GovforLacnPlatoonRoundGroup.Id)
            .SetName("GOVFOR LACN platoon Round Group matches adapter");
        yield return new TestCaseData("AUPlanetCorsatStation", "CMBCIU", GovforCmbciuPlatoonRoundGroup.Id)
            .SetName("GOVFOR CMBCIU platoon Round Group matches adapter");
        yield return new TestCaseData("AuPlanetKutjevo", "WEYU", GovforWeyuPlatoonRoundGroup.Id)
            .SetName("GOVFOR WEYU platoon Round Group matches adapter");
        yield return new TestCaseData("AUPlanetCorsatStation", "HAZOPS", GovforHazopsPlatoonRoundGroup.Id)
            .SetName("GOVFOR HAZOPS platoon Round Group matches adapter");
        yield return new TestCaseData("AuPlanetKutjevo", "VAIPO", GovforVaipoPlatoonRoundGroup.Id)
            .SetName("GOVFOR VAIPO platoon Round Group matches adapter");
        yield return new TestCaseData("AUPlanetSorokyne", "ProdigySF", GovforProdigySfPlatoonRoundGroup.Id)
            .SetName("GOVFOR ProdigySF platoon Round Group matches adapter");
    }

    private static IEnumerable<TestCaseData> FixedOpforPlatoonRoundGroupCases()
    {
        yield return new TestCaseData("AUPlanetSorokyne", "UPP", OpforUppPlatoonRoundGroup.Id)
            .SetName("OPFOR UPP platoon Round Group matches adapter");
        yield return new TestCaseData("AUPlanetCorsatStation", "WEYU", OpforWeyuPlatoonRoundGroup.Id)
            .SetName("OPFOR WEYU platoon Round Group matches adapter");
        yield return new TestCaseData("AUPlanetTrijent", "VAIPO", OpforVaipoPlatoonRoundGroup.Id)
            .SetName("OPFOR VAIPO platoon Round Group matches adapter");
        yield return new TestCaseData("CMUPlanetHopesRetreat", "ProdigySF", OpforProdigySfPlatoonRoundGroup.Id)
            .SetName("OPFOR ProdigySF platoon Round Group matches adapter");
    }

    private static IEnumerable<TestCaseData> UsArmyThirdPartyRoundGroupCases()
    {
        yield return new TestCaseData(
                "USArmy",
                DistressSignalUsArmyPartyRoundGroup.Id,
                DistressSignalUsArmyPartySpawnPlan,
                120)
            .SetName("Distress Signal full USArmy third-party Round Group matches adapter");
        yield return new TestCaseData(
                "USArmyAlt2",
                DistressSignalUsArmyAlt2PartyRoundGroup.Id,
                DistressSignalUsArmyAlt2PartySpawnPlan,
                75)
            .SetName("Distress Signal medium USArmy third-party Round Group matches adapter");
    }

    private static IEnumerable<TestCaseData> UppGromThirdPartyRoundGroupCases()
    {
        yield return new TestCaseData(
                "UPPGROM",
                DistressSignalUppGromPartyRoundGroup.Id,
                DistressSignalUppGromPartySpawnPlan,
                120)
            .SetName("Distress Signal full UPPGROM third-party Round Group matches adapter");
        yield return new TestCaseData(
                "UPPGROMAlt",
                DistressSignalUppGromAltPartyRoundGroup.Id,
                DistressSignalUppGromAltPartySpawnPlan,
                40)
            .SetName("Distress Signal small UPPGROM third-party Round Group matches adapter");
        yield return new TestCaseData(
                "UPPGROMAlt2",
                DistressSignalUppGromAlt2PartyRoundGroup.Id,
                DistressSignalUppGromAlt2PartySpawnPlan,
                75)
            .SetName("Distress Signal medium UPPGROM third-party Round Group matches adapter");
    }

    private static IEnumerable<TestCaseData> ForeconThirdPartyRoundGroupCases()
    {
        yield return new TestCaseData(
                "FORECON",
                DistressSignalForeconPartyRoundGroup.Id,
                DistressSignalForeconPartySpawnPlan,
                120)
            .SetName("Distress Signal full FORECON third-party Round Group matches adapter");
        yield return new TestCaseData(
                "FORECONAlt",
                DistressSignalForeconAltPartyRoundGroup.Id,
                DistressSignalForeconAltPartySpawnPlan,
                40)
            .SetName("Distress Signal small FORECON third-party Round Group matches adapter");
        yield return new TestCaseData(
                "FORECONAlt2",
                DistressSignalForeconAlt2PartyRoundGroup.Id,
                DistressSignalForeconAlt2PartySpawnPlan,
                75)
            .SetName("Distress Signal medium FORECON third-party Round Group matches adapter");
    }

    private static IEnumerable<TestCaseData> IasfSurvThirdPartyRoundGroupCases()
    {
        yield return new TestCaseData(
                "IASFSurvLarge",
                DistressSignalIasfSurvLargePartyRoundGroup.Id,
                DistressSignalIasfSurvLargePartySpawnPlan,
                120)
            .SetName("Distress Signal large IASF survivor third-party Round Group matches adapter");
        yield return new TestCaseData(
                "IASFSurvMedium",
                DistressSignalIasfSurvMediumPartyRoundGroup.Id,
                DistressSignalIasfSurvMediumPartySpawnPlan,
                75)
            .SetName("Distress Signal medium IASF survivor third-party Round Group matches adapter");
        yield return new TestCaseData(
                "IASFSurvSmall",
                DistressSignalIasfSurvSmallPartyRoundGroup.Id,
                DistressSignalIasfSurvSmallPartySpawnPlan,
                40)
            .SetName("Distress Signal small IASF survivor third-party Round Group matches adapter");
    }

    private static IEnumerable<TestCaseData> WeyuSurvThirdPartyRoundGroupCases()
    {
        yield return new TestCaseData(
                "WEYUSurvLarge",
                DistressSignalWeyuSurvLargePartyRoundGroup.Id,
                DistressSignalWeyuSurvLargePartySpawnPlan,
                120)
            .SetName("Distress Signal large WEYU survivor third-party Round Group matches adapter");
        yield return new TestCaseData(
                "WEYUSurvMedium",
                DistressSignalWeyuSurvMediumPartyRoundGroup.Id,
                DistressSignalWeyuSurvMediumPartySpawnPlan,
                75)
            .SetName("Distress Signal medium WEYU survivor third-party Round Group matches adapter");
        yield return new TestCaseData(
                "WEYUSurvSmall",
                DistressSignalWeyuSurvSmallPartyRoundGroup.Id,
                DistressSignalWeyuSurvSmallPartySpawnPlan,
                40)
            .SetName("Distress Signal small WEYU survivor third-party Round Group matches adapter");
    }

    private static IEnumerable<TestCaseData> SimpleThirdPartyRoundGroupCases()
    {
        yield return new TestCaseData(
                "ApeThirdParty",
                DistressSignalApePartyRoundGroup.Id,
                DistressSignalApePartySpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                string.Empty)
            .SetName("Distress Signal ape third-party Round Group matches adapter");
        yield return new TestCaseData(
                "TimeSharesSalesman",
                DistressSignalTimeSharesSalesmanPartyRoundGroup.Id,
                DistressSignalTimeSharesSalesmanPartySpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                "black_ert.yml")
            .SetName("Distress Signal timeshares shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "ProfessorVonBandolier",
                DistressSignalProfessorVonBandolierPartyRoundGroup.Id,
                DistressSignalProfessorVonBandolierPartySpawnPlan,
                "AuPlanetShivasSnowball",
                75,
                "black_ert.yml")
            .SetName("Distress Signal Professor Von Bandolier shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "UPPTDLostTeam",
                DistressSignalUppTdLostTeamPartyRoundGroup.Id,
                DistressSignalUppTdLostTeamPartySpawnPlan,
                "AUPlanetTrijent",
                40,
                "rmc_ert_spp_shuttle.yml")
            .SetName("Distress Signal UPPTD shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "WYPMCPartyAlt",
                DistressSignalWypmcPartyAltRoundGroup.Id,
                DistressSignalWypmcPartyAltSpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                "rmc_ert_pmc_shuttle.yml")
            .SetName("Distress Signal WYPMC alt shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "LACNSniperteam",
                DistressSignalLacnSniperTeamPartyRoundGroup.Id,
                DistressSignalLacnSniperTeamPartySpawnPlan,
                "AUPlanetTrijent",
                40,
                "rmc_ert_response_shuttle.yml")
            .SetName("Distress Signal LACN sniper shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "FORECONFirstRecon",
                DistressSignalForeconFirstReconPartyRoundGroup.Id,
                DistressSignalForeconFirstReconPartySpawnPlan,
                "AUPlanetTrijent",
                75,
                "rmc_ert_response_shuttle.yml")
            .SetName("Distress Signal FORECON First Recon shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "RMCRRT",
                DistressSignalRmcRrtPartyRoundGroup.Id,
                DistressSignalRmcRrtPartySpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                "rmc_ert_response_shuttle.yml")
            .SetName("Distress Signal RMC RRT shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "CMBInvestigationParty",
                DistressSignalCmbInvestigationPartyRoundGroup.Id,
                DistressSignalCmbInvestigationPartySpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                "rmc_ert_cmb_shuttle.yml")
            .SetName("Distress Signal CMB investigation shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "CMBInvestigationPartyAlt",
                DistressSignalCmbInvestigationPartyAltRoundGroup.Id,
                DistressSignalCmbInvestigationPartyAltSpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                "rmc_ert_cmb_shuttle.yml")
            .SetName("Distress Signal CMB investigation alt shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "PAPInvestigationParty",
                DistressSignalPapInvestigationPartyRoundGroup.Id,
                DistressSignalPapInvestigationPartySpawnPlan,
                "AUPlanetTrijent",
                40,
                "rmc_ert_tsepa_shuttle.yml")
            .SetName("Distress Signal PAP investigation shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "PAPInvestigationPartyAlt",
                DistressSignalPapInvestigationPartyAltRoundGroup.Id,
                DistressSignalPapInvestigationPartyAltSpawnPlan,
                "AUPlanetTrijent",
                40,
                "rmc_ert_tsepa_shuttle.yml")
            .SetName("Distress Signal PAP investigation alt shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "NSPAInvestigationParty",
                DistressSignalNspaInvestigationPartyRoundGroup.Id,
                DistressSignalNspaInvestigationPartySpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                "rmc_ert_tsepa_shuttle.yml")
            .SetName("Distress Signal NSPA investigation shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "UACGLostTeam",
                DistressSignalUacgLostTeamPartyRoundGroup.Id,
                DistressSignalUacgLostTeamPartySpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                "rmc_ert_response_shuttle.yml")
            .SetName("Distress Signal UACG Lost Team shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "USArmyArmored",
                DistressSignalUsArmyArmoredPartyRoundGroup.Id,
                DistressSignalUsArmyArmoredPartySpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                "rmc_ert_response_shuttle.yml")
            .SetName("Distress Signal USArmy armored shuttle third-party Round Group matches adapter");
        yield return new TestCaseData(
                "USArmyTank",
                DistressSignalUsArmyTankPartyRoundGroup.Id,
                DistressSignalUsArmyTankPartySpawnPlan,
                "AuPlanetShivasSnowball",
                40,
                string.Empty)
            .SetName("Distress Signal USArmy tank ground third-party Round Group matches adapter");
        yield return new TestCaseData(
                "ICRCReliefParty",
                DistressSignalIcrcReliefPartyRoundGroup.Id,
                DistressSignalIcrcReliefPartySpawnPlan,
                "AuPlanetShivasSnowball",
                75,
                "black_ert.yml")
            .SetName("Distress Signal ICRC relief shuttle third-party Round Group matches adapter");
    }

    private static IEnumerable<TestCaseData> ColonyFallThirdPartyResolverRoundGroupCases()
    {
        yield return new TestCaseData(
                "CMBParoleeTransport",
                ColonyFallCmbParoleeTransportPartyRoundGroup.Id,
                "AUPlanetLV747",
                40,
                2)
            .SetName("Colony Fall CMB parolee transport resolver uses Round Group");
        yield return new TestCaseData(
                "CMBParoleeTransportBig",
                ColonyFallCmbParoleeTransportBigPartyRoundGroup.Id,
                "AUPlanetLV747",
                100,
                3)
            .SetName("Colony Fall CMB parolee transport big resolver uses Round Group");
        yield return new TestCaseData(
                "IPIE",
                ColonyFallIpiePartyRoundGroup.Id,
                "AUPlanetLV747",
                40,
                1)
            .SetName("Colony Fall IPIE resolver uses Round Group");
        yield return new TestCaseData(
                "IPIESynth",
                ColonyFallIpieSynthPartyRoundGroup.Id,
                "AUPlanetLV747",
                40,
                1)
            .SetName("Colony Fall IPIE synth resolver uses Round Group");
        yield return new TestCaseData(
                "WYHT",
                ColonyFallWyhtPartyRoundGroup.Id,
                "AUPlanetLV747",
                40,
                5)
            .SetName("Colony Fall WYHT resolver uses Round Group");
        yield return new TestCaseData(
                "MissionariesParty",
                ColonyFallMissionariesPartyRoundGroup.Id,
                "AUPlanetLV747",
                40,
                1)
            .SetName("Colony Fall missionaries resolver uses Round Group");
        yield return new TestCaseData(
                "MissionariesPartyMedium",
                ColonyFallMissionariesPartyMediumRoundGroup.Id,
                "AUPlanetLV747",
                100,
                3)
            .SetName("Colony Fall missionaries medium resolver uses Round Group");
        yield return new TestCaseData(
                "MigratingCivs",
                ColonyFallMigratingCivsPartyRoundGroup.Id,
                "AUPlanetLV747",
                40,
                2)
            .SetName("Colony Fall migrating civilians resolver uses Round Group");
        yield return new TestCaseData(
                "MigratingCivsMedium",
                ColonyFallMigratingCivsMediumPartyRoundGroup.Id,
                "AUPlanetLV747",
                80,
                4)
            .SetName("Colony Fall migrating civilians medium resolver uses Round Group");
        yield return new TestCaseData(
                "MigratingCivsLarge",
                ColonyFallMigratingCivsLargePartyRoundGroup.Id,
                "AUPlanetLV747",
                120,
                5)
            .SetName("Colony Fall migrating civilians large resolver uses Round Group");
        yield return new TestCaseData(
                "DrugSmuggler",
                ColonyFallDrugSmugglerPartyRoundGroup.Id,
                "AUPlanetLV747",
                40,
                1)
            .SetName("Colony Fall drug smuggler resolver uses Round Group");
        yield return new TestCaseData(
                "ForeignCrimeGang",
                ColonyFallForeignCrimeGangPartyRoundGroup.Id,
                "AUPlanetLV747",
                40,
                3)
            .SetName("Colony Fall foreign crime gang resolver uses Round Group");
        yield return new TestCaseData(
                "FORECONKillTeam",
                ColonyFallForeconKillTeamPartyRoundGroup.Id,
                "AUPlanetLV747",
                80,
                1)
            .SetName("Colony Fall FORECON kill team resolver uses Round Group");
        yield return new TestCaseData(
                "XenoParasiteRunnerParty",
                ColonyFallXenoParasiteRunnerPartyRoundGroup.Id,
                "AUPlanetLV747",
                40,
                1)
            .SetName("Colony Fall xeno parasite runner resolver uses Round Group");
        yield return new TestCaseData(
                "CarpInvasiveParty",
                ColonyFallCarpInvasivePartyRoundGroup.Id,
                "AUPlanetShepherdsPride",
                40,
                0)
            .SetName("Colony Fall carp invasive resolver uses leader-only Round Group");
    }

    [Test]
    public void ScenarioPlanValidateCommandDocumentsAdminReports()
    {
        var command = new ScenarioPlanValidateCommand();
        var roundSetupCommand = new RoundSetupValidateCommand();
        var adminCommand = typeof(ScenarioPlanValidateCommand)
            .GetCustomAttributes(typeof(AdminCommandAttribute), false)
            .Cast<AdminCommandAttribute>()
            .Single();
        var roundSetupAdminCommand = typeof(RoundSetupValidateCommand)
            .GetCustomAttributes(typeof(AdminCommandAttribute), false)
            .Cast<AdminCommandAttribute>()
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(command.Command, Is.EqualTo("scenarioplanvalidate"));
            Assert.That(adminCommand.Flags, Is.EqualTo(AdminFlags.Debug));
            Assert.That(command.Help, Does.Contain("[presetId|all]"));
            Assert.That(command.Help, Does.Contain("[playerCount]"));
            Assert.That(command.Help, Does.Contain("[planetPrototypeId]"));
            Assert.That(command.Help, Does.Contain("[selectedThreatId]"));
            Assert.That(command.Help, Does.Contain("[--markers]"));
            Assert.That(command.Help, Does.Contain("[--choices]"));
            Assert.That(command.Help, Does.Contain("[--backups]"));
            Assert.That(roundSetupCommand.Command, Is.EqualTo("roundsetupvalidate"));
            Assert.That(roundSetupAdminCommand.Flags, Is.EqualTo(AdminFlags.Debug));
            Assert.That(roundSetupCommand.Help, Does.Contain("roundsetupvalidate"));
            Assert.That(roundSetupCommand.Help, Does.Contain("[--choices]"));
        });
    }

    [TestCase("DistressSignal")]
    [TestCase("Insurgency")]
    [TestCase("ColonyFall")]
    public async Task SupportedScenarioPlansHaveSpawnMarkerCoverage(string presetId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var generator = (IScenarioPlanGenerator) server.System<ScenarioPlanSystem>();
            var report = generator.ValidateMarkerCoverage(new ScenarioPlanValidationRequest(
                presetId,
                MarkerValidationPlayerCount));

            Assert.That(report.IsValid, Is.True, report.ToString());
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("DistressSignal")]
    [TestCase("ColonyFall")]
    public async Task DeferredThreatVoteReservationMatchesLegacySmallestCandidate(string presetId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var preset = prototypes.Index<GamePresetPrototype>(presetId);
            var playerCount = GetPresetValidPlayerCount(preset);
            var checkedCandidatePlanets = 0;

            foreach (var planetId in preset.SupportedPlanets)
            {
                if (!TryGetPlanet(prototypes, componentFactory, planetId, out var planet))
                    continue;

                var legacyCandidates = BuildLegacyThreatCandidates(
                    prototypes,
                    planet,
                    presetId,
                    playerCount);
                var request = new ScenarioPlanValidationRequest(
                    presetId,
                    playerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId);
                var resolved = generator.TryResolveDeferredThreatVote(
                    request,
                    out var resolvedChoice,
                    out var diagnostic);

                if (legacyCandidates.Count == 0)
                {
                    Assert.That(
                        resolved,
                        Is.False,
                        $"{planetId} ({planet.MapId}) resolved a deferred threat choice even though legacy selection has no candidates.");
                    continue;
                }

                Assert.That(
                    resolved,
                    Is.True,
                    diagnostic);
                Assert.That(resolvedChoice, Is.Not.Null);
                checkedCandidatePlanets++;

                var resolvedCandidates = resolvedChoice!.Candidates
                    .Select(candidate => new LegacyThreatCandidate(
                        candidate.ThreatId,
                        new ThreatVoteBodyCount(candidate.LeaderBodies, candidate.MemberBodies)))
                    .ToList();

                Assert.That(
                    resolvedCandidates,
                    Is.EqualTo(legacyCandidates),
                    $"{planetId} ({planet.MapId}) produced different Scenario Plan threat candidate body counts.");

                var legacySmallest = legacyCandidates
                    .OrderBy(candidate => candidate.BodyCount.Total)
                    .First()
                    .BodyCount;

                Assert.Multiple(() =>
                {
                    Assert.That(
                        resolvedChoice.ReservationPolicy.ReservedLeaderBodies,
                        Is.EqualTo(legacySmallest.Leaders),
                        $"{planetId} ({planet.MapId}) reserved leader body count changed.");
                    Assert.That(
                        resolvedChoice.ReservationPolicy.ReservedMemberBodies,
                        Is.EqualTo(legacySmallest.Members),
                        $"{planetId} ({planet.MapId}) reserved member body count changed.");
                    Assert.That(
                        resolvedChoice.ReservationPolicy.ReservedBodies,
                        Is.EqualTo(legacySmallest.Total),
                        $"{planetId} ({planet.MapId}) reserved total body count changed.");
                });
            }

            Assert.That(
                checkedCandidatePlanets,
                Is.GreaterThan(0),
                $"{presetId} did not have any supported planets with legacy threat vote candidates at {playerCount} players.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SelectedThreatForcePlanMatchesLegacyBodyCount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index<ThreatPrototype>(SelectedThreatAssignmentThreat);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(threat.RoundStartSpawn);
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, MarkerValidationPlayerCount);
            var request = new ScenarioPlanValidationRequest(
                SelectedThreatAssignmentPreset,
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveSelectedThreatForce(request, out var resolvedForce, out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(resolvedForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(resolvedForce!.ThreatId, Is.EqualTo(threat.ID));
                Assert.That(resolvedForce.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(resolvedForce.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
                Assert.That(resolvedForce.TotalBodies, Is.EqualTo(legacyBodyCount.Total));
                Assert.That(resolvedForce.WinConditionRuleIds, Is.EquivalentTo(threat.WinConditions));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SelectedThreatSpawnMarkersMatchLegacyRuntimeMarkerSelection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            entities.SpawnEntity(ThreatLeaderMarker, map.GridCoords);
            entities.SpawnEntity(XenoCfMemberMarker, map.GridCoords);
            entities.SpawnEntity(ThirdPartyMemberMarker, map.GridCoords);
            entities.SpawnEntity(CultistCfLeaderMarker, map.GridCoords);
            entities.SpawnEntity(CultistCfMemberMarker, map.GridCoords);
            entities.SpawnEntity(CultistCfMemberMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index<ThreatPrototype>(CustomMarkerThreat);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(threat.RoundStartSpawn);
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, MarkerValidationPlayerCount);
            var request = new ScenarioPlanValidationRequest(
                "ColonyFall",
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveSelectedThreatSpawnMarkers(
                    request,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(markerSet!.Force.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(markerSet.Force.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
            });

            AssertResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet!,
                ThreatMarkerType.Leader);
            AssertResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet!,
                ThreatMarkerType.Member);
            foreach (var markerType in new[] { ThreatMarkerType.Leader, ThreatMarkerType.Member })
            {
                Assert.That(
                    markerSet.TryGetMarkers(markerType.ToString(), out var resolvedMarkers),
                    Is.True);
                Assert.That(
                    resolvedMarkers!.All(uid => entities.HasComponent<ScenarioSpawnMarkerComponent>(uid)),
                    Is.True);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitScenarioSpawnMarkerRuntimeResolutionMatchesLegacyMarkerSelection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            entities.SpawnEntity(XenoCfEntityMarker, map.GridCoords);
            entities.SpawnEntity(XenoCfLeaderMarker, map.GridCoords);
            entities.SpawnEntity(XenoCfMemberMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index(DistressSignalAbominationThreat);
            var partySpawn = prototypes.Index(threat.RoundStartSpawn);
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveSelectedThreatSpawnMarkers(
                    request,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            foreach (var markerType in new[] { ThreatMarkerType.Leader, ThreatMarkerType.Member, ThreatMarkerType.Entity })
            {
                Assert.That(
                    markerSet!.TryGetMarkers(markerType.ToString(), out var resolvedMarkers),
                    Is.True);
                Assert.That(
                    resolvedMarkers!.All(uid => entities.HasComponent<ScenarioSpawnMarkerComponent>(uid)),
                    Is.True);
            }

            AssertResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet,
                ThreatMarkerType.Leader);
            AssertResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet,
                ThreatMarkerType.Member);
            AssertResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet,
                ThreatMarkerType.Entity);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitGenericThreatSpawnMarkerRuntimeResolutionMatchesLegacyMarkerSelection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            entities.SpawnEntity(ThreatLeaderMarker, map.GridCoords);
            entities.SpawnEntity(ThreatMemberMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index(SelectedThreatAssignmentThreat);
            var partySpawn = prototypes.Index(threat.RoundStartSpawn);
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveSelectedThreatSpawnMarkers(
                    request,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            foreach (var markerType in new[] { ThreatMarkerType.Leader, ThreatMarkerType.Member })
            {
                Assert.That(
                    markerSet!.TryGetMarkers(markerType.ToString(), out var resolvedMarkers),
                    Is.True);
                Assert.That(
                    resolvedMarkers!.All(uid => entities.HasComponent<ScenarioSpawnMarkerComponent>(uid)),
                    Is.True);
            }

            AssertResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet!,
                ThreatMarkerType.Leader);
            AssertResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet,
                ThreatMarkerType.Member);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HostileStandaloneScenarioSpawnMarkersResolveWithoutLegacyMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            entities.SpawnEntity(ScenarioThreatLeaderMarker, map.GridCoords);
            entities.SpawnEntity(ScenarioThreatMemberMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index(SelectedThreatAssignmentThreat);
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveSelectedThreatSpawnMarkers(
                    request,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            foreach (var markerType in new[] { ThreatMarkerType.Leader, ThreatMarkerType.Member })
            {
                Assert.That(
                    markerSet!.TryGetMarkers(markerType.ToString(), out var resolvedMarkers),
                    Is.True);
                Assert.That(
                    resolvedMarkers!.All(uid => entities.HasComponent<ScenarioSpawnMarkerComponent>(uid)),
                    Is.True);
                Assert.That(
                    resolvedMarkers!.All(uid => !entities.HasComponent<ThreatSpawnMarkerComponent>(uid)),
                    Is.True);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThirdPartyGroundSpawnMarkersMatchLegacyRuntimeMarkerSelection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            entities.SpawnEntity("threatleaderspawnmarker", map.GridCoords);
            entities.SpawnEntity(ThirdPartyEntityMarker, map.GridCoords);
            entities.SpawnEntity(ThirdPartyLeaderMarker, map.GridCoords);
            entities.SpawnEntity(ThirdPartyLeaderMarker, map.GridCoords);
            entities.SpawnEntity(ThirdPartyMemberMarker, map.GridCoords);
            entities.SpawnEntity(ThirdPartyMemberMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(GroundThirdParty);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(thirdParty.PartySpawn);
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount: 40);
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                40,
                SelectedThreatId: SelectedThreatAssignmentThreat.Id);

            Assert.That(
                generator.TryResolveThirdPartySpawnMarkers(
                    request,
                    thirdParty.ID,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(markerSet!.Force.ThirdPartyId, Is.EqualTo(thirdParty.ID));
                Assert.That(markerSet.Force.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(markerSet.Force.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
                Assert.That(markerSet.Force.EntityBodies, Is.Zero);
            });

            AssertThirdPartyResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet!,
                ThreatMarkerType.Leader);
            AssertThirdPartyResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet!,
                ThreatMarkerType.Member);
            foreach (var markerType in new[] { ThreatMarkerType.Leader, ThreatMarkerType.Member })
            {
                Assert.That(
                    markerSet.TryGetMarkers(markerType.ToString(), out var resolvedMarkers),
                    Is.True);
                Assert.That(
                    resolvedMarkers!.All(uid => entities.HasComponent<ScenarioSpawnMarkerComponent>(uid)),
                    Is.True);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(ColonyFallThirdPartyResolverRoundGroupCases))]
    public async Task ColonyFallThirdPartyResolverRoundGroupMatchesLegacyPartySpawn(
        string thirdPartyId,
        string RoundGroupId,
        string planetId,
        int playerCount,
        int expectedMembers)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            entities.SpawnEntity(ThirdPartyLeaderMarker, map.GridCoords);
            entities.SpawnEntity(ThirdPartyMemberMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(thirdPartyId);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(thirdParty.PartySpawn);
            var RoundGroup = prototypes.Index<RoundGroupPrototype>(RoundGroupId);
            var spawnPlan = RoundGroup.Spawn;
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out _),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");
            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    RoundGroupId,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            var request = new ScenarioPlanValidationRequest(
                ColonyFallPreset,
                playerCount,
                SelectedThreatId: ColonyFallXenoThreat.Id);

            Assert.That(
                generator.TryResolveThirdPartySpawnMarkers(
                    request,
                    thirdParty.ID,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(markerSet!.Force.ForceId, Is.EqualTo($"ThirdParty:{ColonyFallXenoThreat.Id}:{thirdPartyId}"));
                Assert.That(markerSet.Force.ThirdPartyId, Is.EqualTo(thirdParty.ID));
                Assert.That(markerSet.Force.LeaderBodies, Is.EqualTo(1));
                Assert.That(markerSet.Force.MemberBodies, Is.EqualTo(expectedMembers));
                Assert.That(markerSet.Force.EntityBodies, Is.Zero);
            });

            AssertResolvedSpawnPlansMatch(prototypeForce!.SpawnPlan, markerSet!.Force.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(
                spawnPlan,
                partySpawn,
                ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount),
                thirdParty: true);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);
            AssertThirdPartyResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet,
                ThreatMarkerType.Leader);
            if (expectedMembers > 0)
            {
                AssertThirdPartyResolvedMarkersMatchLegacy(
                    entities,
                    map.MapId,
                    partySpawn,
                    markerSet,
                    ThreatMarkerType.Member);
            }
            else
            {
                Assert.That(
                    markerSet.TryGetMarkers(ThreatMarkerType.Member.ToString(), out _),
                    Is.False,
                    "Leader-only third-party Round Groups should not resolve member markers.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThirdPartyStandaloneScenarioSpawnMarkersResolveWithoutLegacyMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var leaderMarker = EntityUid.Invalid;
        var memberMarker = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            leaderMarker = entities.SpawnEntity(StandaloneThirdPartyLeaderMarker, map.GridCoords);
            memberMarker = entities.SpawnEntity(StandaloneThirdPartyMemberMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(GroundThirdParty);
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                40,
                SelectedThreatId: SelectedThreatAssignmentThreat.Id);

            Assert.That(
                generator.TryResolveThirdPartySpawnMarkers(
                    request,
                    thirdParty.ID,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(markerSet!.TryGetMarkers(ThreatMarkerType.Leader.ToString(), out var leaderMarkers), Is.True);
                Assert.That(leaderMarkers, Is.EquivalentTo(new[] { leaderMarker }));
                Assert.That(markerSet.TryGetMarkers(ThreatMarkerType.Member.ToString(), out var memberMarkers), Is.True);
                Assert.That(memberMarkers, Is.EquivalentTo(new[] { memberMarker }));
                Assert.That(entities.HasComponent<ThreatSpawnMarkerComponent>(leaderMarker), Is.False);
                Assert.That(entities.HasComponent<ThreatSpawnMarkerComponent>(memberMarker), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThirdPartyStandaloneScenarioSpawnMarkersApplyCooldownDuringSpawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var leaderMarker = EntityUid.Invalid;
        var memberMarker = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            leaderMarker = entities.SpawnEntity(StandaloneThirdPartyCooldownLeaderMarker, map.GridCoords);
            memberMarker = entities.SpawnEntity(StandaloneThirdPartyCooldownMemberMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdPartySystem = server.System<ThirdPartySystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(StandaloneCooldownThirdParty);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(thirdParty.PartySpawn);
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                40,
                SelectedThreatId: SelectedThreatAssignmentThreat.Id);

            Assert.That(
                generator.TryResolveThirdPartySpawnMarkers(
                    request,
                    thirdParty.ID,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(markerSet!.TryGetMarkers(ThreatMarkerType.Leader.ToString(), out var leaderMarkers), Is.True);
                Assert.That(leaderMarkers, Is.EquivalentTo(new[] { leaderMarker }));
                Assert.That(markerSet.TryGetMarkers(ThreatMarkerType.Member.ToString(), out var memberMarkers), Is.True);
                Assert.That(memberMarkers, Is.EquivalentTo(new[] { memberMarker }));
            });

            Assert.That(thirdPartySystem.SpawnThirdParty(thirdParty, partySpawn, false), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(
                    entities.TryGetComponent<ScenarioSpawnMarkerCooldownComponent>(leaderMarker, out var leaderCooldown),
                    Is.True);
                Assert.That(
                    entities.TryGetComponent<ScenarioSpawnMarkerCooldownComponent>(memberMarker, out var memberCooldown),
                    Is.True);
                Assert.That(leaderCooldown!.NextAvailableAt, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(memberCooldown!.NextAvailableAt, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(entities.HasComponent<ThreatSpawnMarkerComponent>(leaderMarker), Is.False);
                Assert.That(entities.HasComponent<ThreatSpawnMarkerComponent>(memberMarker), Is.False);
            });

            Assert.That(
                thirdPartySystem.SpawnThirdParty(thirdParty, partySpawn, false),
                Is.False,
                "Scenario Plan standalone third-party markers should not be immediately reused while their cooldown is active.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThirdPartyParachuteSpawnMarkersMatchLegacyRuntimeMarkerSelection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            entities.SpawnEntity("thirdpartyleaderspawnmarker", map.GridCoords);
            entities.SpawnEntity("thirdpartymemberspawnmarker", map.GridCoords);

            var parachuteLeader = entities.SpawnEntity("thirdpartyleaderspawnmarker", map.GridCoords);
            entities.EnsureComponent<ParachuteMarkerComponent>(parachuteLeader);
            var parachuteMember = entities.SpawnEntity("thirdpartymemberspawnmarker", map.GridCoords);
            entities.EnsureComponent<ParachuteMarkerComponent>(parachuteMember);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(ParachuteThirdParty);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(thirdParty.PartySpawn);
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount: 40);
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                40,
                SelectedThreatId: SelectedThreatAssignmentThreat.Id);

            Assert.That(
                generator.TryResolveThirdPartySpawnMarkers(
                    request,
                    thirdParty.ID,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(markerSet!.Force.ThirdPartyId, Is.EqualTo(thirdParty.ID));
                Assert.That(markerSet.Force.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(markerSet.Force.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
                Assert.That(markerSet.Force.EntityBodies, Is.Zero);
            });

            AssertThirdPartyParachuteResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet!,
                ThreatMarkerType.Leader);
            AssertThirdPartyParachuteResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                partySpawn,
                markerSet!,
                ThreatMarkerType.Member);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThirdPartyDropshipSpawnMarkersMatchLegacyLoadedGridMarkerSelection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var mapLoader = server.System<MapLoaderSystem>();
            var mapSystem = server.System<SharedMapSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(DropshipThirdParty);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(thirdParty.PartySpawn);
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount: 40);

            mapSystem.CreateMap(out var mapId);
            Assert.That(
                mapLoader.TryLoadGrid(mapId, thirdParty.dropshippath, out var grid),
                Is.True,
                thirdParty.dropshippath.ToString());
            Assert.That(grid, Is.Not.Null);

            var gridUid = grid!.Value.Owner;
            var loadedMapId = entities.GetComponent<TransformComponent>(gridUid).MapID;
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                40,
                SelectedThreatId: SelectedThreatAssignmentThreat.Id);

            Assert.That(
                generator.TryResolveThirdPartySpawnMarkers(
                    request,
                    thirdParty.ID,
                    loadedMapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(markerSet!.Force.ThirdPartyId, Is.EqualTo(thirdParty.ID));
                Assert.That(markerSet.Force.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(markerSet.Force.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
                Assert.That(markerSet.Force.EntityBodies, Is.Zero);
            });

            AssertThirdPartyDropshipResolvedMarkersMatchLegacy(
                entities,
                gridUid,
                partySpawn,
                markerSet!,
                ThreatMarkerType.Leader);
            AssertThirdPartyDropshipResolvedMarkersMatchLegacy(
                entities,
                gridUid,
                partySpawn,
                markerSet!,
                ThreatMarkerType.Member);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClfSpawnMarkersMatchLegacyRuntimeSafehouseAndCivilianSelection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            entities.SpawnEntity(ClfCellLeaderSpawnPoint, map.GridCoords);
            entities.SpawnEntity(ScenarioClfCivilianColonistSpawnMarker, map.GridCoords);
            entities.SpawnEntity(ScenarioClfCivilianColonistSpawnMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var (commandBodies, guerillaBodies) = GetLegacyClfBodyCounts(prototypes, componentFactory);

            var request = new ScenarioPlanValidationRequest(
                "Insurgency",
                MarkerValidationPlayerCount);

            Assert.That(
                generator.TryResolveClfSpawnMarkers(
                    request,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(markerSet!.Force.CommandBodies, Is.EqualTo(commandBodies));
                Assert.That(markerSet.Force.GuerillaBodies, Is.EqualTo(guerillaBodies));
            });

            AssertClfResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                markerSet!,
                "CLFSafehouse",
                FindScenarioCompatibleClfSafehouseMarkers(entities, map.MapId));
            AssertClfResolvedMarkersMatchLegacy(
                entities,
                map.MapId,
                markerSet!,
                "CLFCivilianBackup",
                FindLegacyClfCivilianSpawnPoints(entities, map.MapId));
            foreach (var bucket in new[] { "CLFSafehouse", "CLFCivilianBackup" })
            {
                Assert.That(
                    markerSet.TryGetMarkers(bucket, out var resolvedMarkers),
                    Is.True);
                Assert.That(
                    resolvedMarkers!.All(uid => entities.HasComponent<ScenarioSpawnMarkerComponent>(uid)),
                    Is.True);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClfStandaloneScenarioSpawnMarkersResolveWithoutLegacyMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var safehouseMarker = EntityUid.Invalid;
        var civilianMarker = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;

            safehouseMarker = entities.SpawnEntity(ScenarioClfSafehouseMarker, map.GridCoords);
            civilianMarker = entities.SpawnEntity(StandaloneClfCivilianMarker, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var generator = server.System<ScenarioPlanSystem>();
            var request = new ScenarioPlanValidationRequest(
                "Insurgency",
                MarkerValidationPlayerCount);

            Assert.That(
                generator.TryResolveClfSpawnMarkers(
                    request,
                    map.MapId,
                    out var markerSet,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(markerSet, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(markerSet!.TryGetMarkers("CLFSafehouse", out var safehouseMarkers), Is.True);
                Assert.That(safehouseMarkers, Is.EquivalentTo(new[] { safehouseMarker }));
                Assert.That(markerSet.TryGetMarkers("CLFCivilianBackup", out var civilianMarkers), Is.True);
                Assert.That(civilianMarkers, Is.EquivalentTo(new[] { civilianMarker }));
                Assert.That(entities.HasComponent<SafehouseMarkerComponent>(safehouseMarker), Is.False);
                Assert.That(entities.HasComponent<SpawnPointComponent>(civilianMarker), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("DistressSignal")]
    [TestCase("Insurgency")]
    [TestCase("ColonyFall")]
    public async Task ShadowScenarioPlanCanBeGeneratedForSelectedTargetScenario(string presetId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var (planetId, planet) = GetFirstSupportedPlanet(prototypes, componentFactory, presetId);
            var selectedThreatId = GetFirstSelectedThreatId(generator, presetId, planetId, planet.MapId);

            var request = new ScenarioPlanValidationRequest(
                presetId,
                MarkerValidationPlayerCount,
                PlanetId: planetId,
                MapId: planet.MapId,
                SelectedThreatId: selectedThreatId);
            var snapshot = generator.GenerateShadowPlan(request, "IntegrationTest");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Report.IsValid, Is.True, snapshot.Report.ToString());
                Assert.That(snapshot.Report.Plans, Has.Count.EqualTo(1));
                Assert.That(snapshot.Report.Plans[0].PlanetId, Is.EqualTo(planetId));
                Assert.That(snapshot.Report.Plans[0].MapId, Is.EqualTo(planet.MapId));
                Assert.That(snapshot.Report.Plans[0].SelectedThreatId, Is.EqualTo(selectedThreatId));
                Assert.That(generator.LastShadowPlan, Is.SameAs(snapshot));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShadowScenarioPlanUsesVotingBackupChoicesWhenNormalMarkerValidationFails()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var generator = server.System<ScenarioPlanSystem>();
            var request = new ScenarioPlanValidationRequest(
                ShadowFallbackPreset,
                MarkerValidationPlayerCount,
                PlanetId: ShadowFallbackPlanet,
                MapId: ShadowFallbackMap);

            var normalReport = generator.ValidateMarkerCoverage(request);
            Assert.That(normalReport.IsValid, Is.False, normalReport.ToString());
            Assert.That(normalReport.Diagnostics.Any(diagnostic =>
                    diagnostic.Severity == ScenarioDiagnosticSeverity.Error),
                Is.True,
                normalReport.ToString());

            var snapshot = generator.GenerateShadowPlan(request, "IntegrationTestFallback");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Report.IsValid, Is.True, snapshot.Report.ToString());
                Assert.That(snapshot.Report.Plans, Has.Count.EqualTo(1));
                Assert.That(snapshot.Report.Plans[0].PresetId, Is.EqualTo(ShadowFallbackPreset));
                Assert.That(snapshot.Report.Plans[0].PlanetId, Is.EqualTo(ShadowFallbackPlanet));
                Assert.That(snapshot.Report.Plans[0].MapId, Is.EqualTo(ShadowFallbackMap));
                Assert.That(snapshot.Report.Plans[0].Forces, Has.Count.EqualTo(1));
                Assert.That(snapshot.Report.Plans[0].Forces[0].ForceKind, Is.EqualTo(ScenarioForceKind.Hostile));
                Assert.That(snapshot.Report.Plans[0].Forces[0].SourcePrototypeId, Is.EqualTo(SelectedThreatAssignmentThreat.Id));
                Assert.That(snapshot.Report.Plans[0].DeferredForceChoices, Has.Count.EqualTo(1));
                Assert.That(generator.LastShadowPlan, Is.SameAs(snapshot));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeferredThreatVoteUsesVotingBackupChoicesWhenNormalMarkerValidationFails()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var generator = server.System<ScenarioPlanSystem>();
            var request = new ScenarioPlanValidationRequest(
                ShadowFallbackPreset,
                MarkerValidationPlayerCount,
                PlanetId: ShadowFallbackPlanet,
                MapId: ShadowFallbackMap);

            var normalReport = generator.ValidateMarkerCoverage(request);
            Assert.That(normalReport.IsValid, Is.False, normalReport.ToString());

            Assert.That(
                generator.TryResolveDeferredThreatVote(request, out var deferredChoice, out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(deferredChoice, Is.Not.Null);

            var candidate = deferredChoice!.Candidates.Single();
            Assert.Multiple(() =>
            {
                Assert.That(candidate.ThreatId, Is.EqualTo(SelectedThreatAssignmentThreat.Id));
                Assert.That(candidate.LeaderBodies, Is.EqualTo(1));
                Assert.That(candidate.MemberBodies, Is.EqualTo(1));
                Assert.That(deferredChoice.ReservationPolicy.ReservedLeaderBodies, Is.EqualTo(1));
                Assert.That(deferredChoice.ReservationPolicy.ReservedMemberBodies, Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkerMigrationReportIncludesMapAuthorTagsAndLegacySources()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var (planetId, planet) = GetFirstSupportedPlanet(prototypes, componentFactory, "DistressSignal");
            var selectedThreatId = GetFirstSelectedThreatId(generator, "DistressSignal", planetId, planet.MapId);

            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                MarkerValidationPlayerCount,
                PlanetId: planetId,
                MapId: planet.MapId,
                SelectedThreatId: selectedThreatId);
            var report = generator.BuildMarkerMigrationReport(request);

            Assert.That(report.Hints, Is.Not.Empty);
            Assert.That(report.UnsatisfiedHints, Is.Empty, report.ToString());

            var leaderHint = report.Hints.FirstOrDefault(hint =>
                hint.ForceId.StartsWith("Hostile:", StringComparison.Ordinal) &&
                hint.Bucket.Equals(ThreatMarkerType.Leader.ToString(), StringComparison.OrdinalIgnoreCase));

            Assert.That(leaderHint, Is.Not.Null, report.ToString());
            Assert.Multiple(() =>
            {
                Assert.That(leaderHint!.RequiredTags, Does.Contain(ScenarioMarkerTags.ForceHostile));
                Assert.That(leaderHint.RequiredTags, Does.Contain(ScenarioMarkerTags.Bucket("Leader")));
                Assert.That(
                    leaderHint.RequiredTags.Any(tag => tag.StartsWith("marker-id:", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(leaderHint.AvailableMarkers, Is.GreaterThanOrEqualTo(leaderHint.RequiredMarkerCount));
                Assert.That(leaderHint.MatchingMarkerSources, Is.Not.Empty);
                Assert.That(
                    leaderHint.MatchingMarkerSources.Sum(source => source.Count),
                    Is.EqualTo(leaderHint.AvailableMarkers));
                Assert.That(report.ToString(), Does.Contain("Matching legacy sources:"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitScenarioSpawnMarkerComponentPreservesLegacyThreatMarker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var markers = new (EntProtoId Id, ThreatMarkerType MarkerType)[]
            {
                (XenoCfEntityMarker, ThreatMarkerType.Entity),
                (XenoCfLeaderMarker, ThreatMarkerType.Leader),
                (XenoCfMemberMarker, ThreatMarkerType.Member),
            };

            foreach (var (markerId, markerType) in markers)
            {
                var marker = prototypes.Index(markerId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        marker.TryGetComponent<ScenarioSpawnMarkerComponent>(out var scenarioMarker, componentFactory),
                        Is.True);
                    Assert.That(scenarioMarker!.Kind, Is.EqualTo(SpawnMarkerKind.ThreatMarker));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.ForceHostile));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.Bucket(markerType.ToString())));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.MarkerId("xenocf")));

                    Assert.That(
                        marker.TryGetComponent<ThreatSpawnMarkerComponent>(out var legacyMarker, componentFactory),
                        Is.True);
                    Assert.That(legacyMarker!.ThreatMarkerType, Is.EqualTo(markerType));
                    Assert.That(legacyMarker.ID, Is.EqualTo("xenocf"));
                    Assert.That(legacyMarker.ThirdParty, Is.False);
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitScenarioSpawnMarkerComponentPreservesLegacyGenericThreatMarker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var markers = new (EntProtoId Id, ThreatMarkerType MarkerType)[]
            {
                (ThreatEntityMarker, ThreatMarkerType.Entity),
                (ThreatLeaderMarker, ThreatMarkerType.Leader),
                (ThreatMemberMarker, ThreatMarkerType.Member),
            };

            foreach (var (markerId, markerType) in markers)
            {
                var marker = prototypes.Index(markerId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        marker.TryGetComponent<ScenarioSpawnMarkerComponent>(out var scenarioMarker, componentFactory),
                        Is.True);
                    Assert.That(scenarioMarker!.Kind, Is.EqualTo(SpawnMarkerKind.ThreatMarker));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.ForceHostile));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.Bucket(markerType.ToString())));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.MarkerId(string.Empty)));

                    Assert.That(
                        marker.TryGetComponent<ThreatSpawnMarkerComponent>(out var legacyMarker, componentFactory),
                        Is.True);
                    Assert.That(legacyMarker!.ThreatMarkerType, Is.EqualTo(markerType));
                    Assert.That(legacyMarker.ID, Is.Empty);
                    Assert.That(legacyMarker.ThirdParty, Is.False);
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitScenarioSpawnMarkerComponentPreservesLegacyCultistThreatMarker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var markers = new (EntProtoId Id, ThreatMarkerType MarkerType)[]
            {
                (CultistCfLeaderMarker, ThreatMarkerType.Leader),
                (CultistCfMemberMarker, ThreatMarkerType.Member),
            };

            foreach (var (markerId, markerType) in markers)
            {
                var marker = prototypes.Index(markerId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        marker.TryGetComponent<ScenarioSpawnMarkerComponent>(out var scenarioMarker, componentFactory),
                        Is.True);
                    Assert.That(scenarioMarker!.Kind, Is.EqualTo(SpawnMarkerKind.ThreatMarker));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.ForceHostile));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.Bucket(markerType.ToString())));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.MarkerId("cultcfmarker")));

                    Assert.That(
                        marker.TryGetComponent<ThreatSpawnMarkerComponent>(out var legacyMarker, componentFactory),
                        Is.True);
                    Assert.That(legacyMarker!.ThreatMarkerType, Is.EqualTo(markerType));
                    Assert.That(legacyMarker.ID, Is.EqualTo("cultcfmarker"));
                    Assert.That(legacyMarker.ThirdParty, Is.False);
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitScenarioSpawnMarkerComponentPreservesLegacyThirdPartyMarker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var markers = new (EntProtoId Id, ThreatMarkerType MarkerType)[]
            {
                (ThirdPartyEntityMarker, ThreatMarkerType.Entity),
                (ThirdPartyLeaderMarker, ThreatMarkerType.Leader),
                (ThirdPartyMemberMarker, ThreatMarkerType.Member),
            };

            foreach (var (markerId, markerType) in markers)
            {
                var marker = prototypes.Index(markerId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        marker.TryGetComponent<ScenarioSpawnMarkerComponent>(out var scenarioMarker, componentFactory),
                        Is.True);
                    Assert.That(scenarioMarker!.Kind, Is.EqualTo(SpawnMarkerKind.ThirdPartyMarker));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.ForceThirdParty));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.Bucket(markerType.ToString())));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.MarkerId(string.Empty)));

                    Assert.That(
                        marker.TryGetComponent<ThreatSpawnMarkerComponent>(out var legacyMarker, componentFactory),
                        Is.True);
                    Assert.That(legacyMarker!.ThreatMarkerType, Is.EqualTo(markerType));
                    Assert.That(legacyMarker.ID, Is.Empty);
                    Assert.That(legacyMarker.ThirdParty, Is.True);
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitScenarioSpawnMarkerComponentPreservesLegacyClfMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var safehouseMarkers = new[] { ClfCellLeaderSpawnPoint, SafehouseMarker };

            foreach (var markerId in safehouseMarkers)
            {
                var marker = prototypes.Index(markerId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        marker.TryGetComponent<ScenarioSpawnMarkerComponent>(out var scenarioMarker, componentFactory),
                        Is.True);
                    Assert.That(scenarioMarker!.Kind, Is.EqualTo(SpawnMarkerKind.ClfSafehouse));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.ForceClfSafehouse));
                    Assert.That(
                        marker.TryGetComponent<SafehouseMarkerComponent>(out _, componentFactory),
                        Is.True);
                });
            }

            foreach (var spawnPointId in new[] { CivilianColonistSpawnPoint, ScenarioClfCivilianColonistSpawnMarker })
            {
                var civilianSpawn = prototypes.Index(spawnPointId);
                Assert.Multiple(() =>
                {
                    Assert.That(
                        civilianSpawn.TryGetComponent<ScenarioSpawnMarkerComponent>(out var scenarioMarker, componentFactory),
                        Is.True);
                    Assert.That(scenarioMarker!.Kind, Is.EqualTo(SpawnMarkerKind.ClfCivilianSpawn));
                    Assert.That(scenarioMarker.Tags, Does.Contain(ScenarioMarkerTags.ClfCivilianSpawn("AU14JobCivilianColonist")));
                    Assert.That(
                        civilianSpawn.TryGetComponent<SpawnPointComponent>(out var spawnPoint, componentFactory),
                        Is.True);
                    Assert.That(spawnPoint!.Job?.Id, Is.EqualTo("AU14JobCivilianColonist"));
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LegacySpawnMarkerPrototypesHaveExplicitScenarioSpawnMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var missing = new List<string>();

            foreach (var prototype in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                var legacyThreatMarker = prototype.TryGetComponent<ThreatSpawnMarkerComponent>(out _, componentFactory);
                var safehouseMarker = prototype.TryGetComponent<SafehouseMarkerComponent>(out _, componentFactory);
                var civilianFallback = prototype.TryGetComponent<SpawnPointComponent>(out var spawnPoint, componentFactory) &&
                                       spawnPoint.Job?.Id == "AU14JobCivilianColonist";

                if (!legacyThreatMarker &&
                    !safehouseMarker &&
                    !civilianFallback)
                {
                    continue;
                }

                if (!prototype.TryGetComponent<ScenarioSpawnMarkerComponent>(out _, componentFactory))
                    missing.Add(prototype.ID);
            }

            Assert.That(
                missing,
                Is.Empty,
                $"Legacy Spawn Marker prototype(s) need ScenarioSpawnMarker tags: {string.Join(", ", missing)}");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkerMigrationReportIncludesExplicitScenarioSpawnMarkerSources()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var (planetId, planet) = GetFirstSupportedPlanet(prototypes, componentFactory, "DistressSignal");

            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                MarkerValidationPlayerCount,
                PlanetId: planetId,
                MapId: planet.MapId,
                SelectedThreatId: DistressSignalAbominationThreat.Id);
            var report = generator.BuildMarkerMigrationReport(request);
            var abominationLeaderHint = report.Hints.FirstOrDefault(hint =>
                hint.SourcePrototypeId.Equals(DistressSignalAbominationThreat.Id, StringComparison.OrdinalIgnoreCase) &&
                hint.Bucket.Equals(ThreatMarkerType.Leader.ToString(), StringComparison.OrdinalIgnoreCase));

            Assert.That(abominationLeaderHint, Is.Not.Null, report.ToString());
            Assert.That(
                abominationLeaderHint!.MatchingMarkerSources.Any(source =>
                    source.SourcePrototypeId.Equals("scenarioxenocfthreatleaderSpawnMarker", StringComparison.OrdinalIgnoreCase) &&
                    source.Tags.Contains(ScenarioMarkerTags.MarkerId("xenocf"), StringComparer.OrdinalIgnoreCase)),
                Is.True,
                report.ToString());
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VotingChoicesPrototypeShapesLoadAsReadOnlyMigrationTargets()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        var changed = new Dictionary<Type, HashSet<string>>();
        server.ProtoMan.LoadString(ScenarioPrototypeShapeTestPrototypes, changed: changed);
        await server.WaitPost(() => server.ProtoMan.ReloadPrototypes(changed));

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var package = prototypes.Index<VotingChoicesPrototype>(PrototypeShapeVotingChoices);
            var force = prototypes.Index<RoundGroupPrototype>(PrototypeShapeRoundGroup);
            var marker = prototypes.Index<SpawnMarkerPrototype>(PrototypeShapeSpawnMarker);
            var spawnPlan = force.Spawn;

            Assert.Multiple(() =>
            {
                Assert.That(package.Presets, Does.Contain("DistressSignal"));
                Assert.That(package.SupportedPlanets, Does.Contain("LV624"));
                Assert.That(package.Groups.Select(groupId => groupId.Id), Does.Contain(force.ID));
                Assert.That(package.DeferredForceChoices, Has.Count.EqualTo(1));
                Assert.That(package.DeferredForceChoices[0].Candidates.Select(forceId => forceId.Id), Does.Contain(force.ID));

                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.Hostile));
                Assert.That(force.Side, Is.EqualTo(RoundForceSide.None));
                Assert.That(force.Source, Is.EqualTo(RoundForceSource.None));
                Assert.That(force.SourcePrototypeId, Is.EqualTo("XenoThreat"));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(force.WinConditionRuleIds, Does.Contain("KillAllGovforRule"));
                Assert.That(force.Timing.DelayMinSeconds, Is.EqualTo(1200));
                Assert.That(force.Timing.DelayMaxSeconds, Is.EqualTo(2400));

                Assert.That(spawnPlan.AllowsUnderfill, Is.True);
                Assert.That(spawnPlan.BodyBuckets, Has.Count.EqualTo(1));
                Assert.That(spawnPlan.BodyBuckets[0].Bucket, Is.EqualTo("Leader"));
                Assert.That(spawnPlan.BodyBuckets[0].Count, Is.EqualTo(1));
                Assert.That(spawnPlan.BodyBuckets[0].Bodies["MobHuman"], Is.EqualTo(1));
                Assert.That(spawnPlan.MarkerRequirements, Has.Count.EqualTo(1));
                Assert.That(spawnPlan.MarkerRequirements[0].RequiredTags, Does.Contain(ScenarioMarkerTags.ForceHostile));
                Assert.That(spawnPlan.MarkerRequirements[0].RequiredTags, Does.Contain(ScenarioMarkerTags.Bucket("Leader")));

                Assert.That(marker.Kind, Is.EqualTo(SpawnMarkerKind.ThreatMarker));
                Assert.That(marker.Tags, Does.Contain(ScenarioMarkerTags.ForceHostile));
                Assert.That(marker.Count, Is.EqualTo(1));
            });

            Assert.That(
                generator.TryResolveVotingChoicesPrototype(
                    package.ID,
                    "DistressSignal",
                    "LV624",
                    "LV624",
                    MarkerValidationPlayerCount,
                    out var prototypePlan,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(prototypePlan, Is.Not.Null);

            var deferredChoice = prototypePlan!.DeferredForceChoices.Single();
            var resolvedBucket = prototypePlan.Forces[0].SpawnPlan.BodyBuckets.Single();
            Assert.Multiple(() =>
            {
                Assert.That(prototypePlan.Forces, Has.Count.EqualTo(1));
                Assert.That(prototypePlan.Forces[0].SourcePrototypeId, Is.EqualTo(force.SourcePrototypeId));
                Assert.That(prototypePlan.Forces[0].Timing, Is.EqualTo(new ScenarioForceTiming(1200, 2400)));
                Assert.That(resolvedBucket.Bucket, Is.EqualTo("Leader"));
                Assert.That(resolvedBucket.Count, Is.EqualTo(1));
                Assert.That(resolvedBucket.Bodies["MobHuman"], Is.EqualTo(1));
                Assert.That(deferredChoice.ChoiceId, Is.EqualTo("DeferredThreat:DistressSignal:LV624"));
                Assert.That(deferredChoice.Candidates, Has.Count.EqualTo(1));
                Assert.That(deferredChoice.ReservationPolicy.PolicyId, Is.EqualTo("SmallestCandidateBodyCountAllowsUnderfill"));
                Assert.That(deferredChoice.ReservationPolicy.ReservedLeaderBodies, Is.EqualTo(1));
                Assert.That(deferredChoice.ReservationPolicy.ReservedMemberBodies, Is.Zero);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScenarioRoundGroupsUseKindDefaultsForRoundSideAndSource()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var explicitOverrides = prototypes
                .EnumeratePrototypes<RoundGroupPrototype>()
                .Where(force => force.Side != RoundForceSide.None || force.Source != RoundForceSource.None)
                .Select(force => $"{force.ID}: side={force.Side}, source={force.Source}")
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.That(explicitOverrides, Is.Empty, string.Join("\n", explicitOverrides));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnMarkerPrototypeSeedsCoverMigratedLegacyMarkerFamilies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            AssertSpawnMarkerPrototype(
                prototypes,
                ThreatLeaderGenericSpawnMarker,
                SpawnMarkerKind.ThreatMarker,
                ScenarioMarkerTags.ForceHostile,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Leader.ToString()),
                ScenarioMarkerTags.MarkerId(string.Empty));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThreatMemberGenericSpawnMarker,
                SpawnMarkerKind.ThreatMarker,
                ScenarioMarkerTags.ForceHostile,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Member.ToString()),
                ScenarioMarkerTags.MarkerId(string.Empty));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThreatEntityGenericSpawnMarker,
                SpawnMarkerKind.ThreatMarker,
                ScenarioMarkerTags.ForceHostile,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Entity.ToString()),
                ScenarioMarkerTags.MarkerId(string.Empty));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThreatLeaderXenoCfSpawnMarker,
                SpawnMarkerKind.ThreatMarker,
                ScenarioMarkerTags.ForceHostile,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Leader.ToString()),
                ScenarioMarkerTags.MarkerId("xenocf"));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThreatMemberXenoCfSpawnMarker,
                SpawnMarkerKind.ThreatMarker,
                ScenarioMarkerTags.ForceHostile,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Member.ToString()),
                ScenarioMarkerTags.MarkerId("xenocf"));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThreatEntityXenoCfSpawnMarker,
                SpawnMarkerKind.ThreatMarker,
                ScenarioMarkerTags.ForceHostile,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Entity.ToString()),
                ScenarioMarkerTags.MarkerId("xenocf"));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThreatLeaderCultistCfSpawnMarker,
                SpawnMarkerKind.ThreatMarker,
                ScenarioMarkerTags.ForceHostile,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Leader.ToString()),
                ScenarioMarkerTags.MarkerId("cultistcf"));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThreatMemberCultistCfSpawnMarker,
                SpawnMarkerKind.ThreatMarker,
                ScenarioMarkerTags.ForceHostile,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Member.ToString()),
                ScenarioMarkerTags.MarkerId("cultistcf"));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThirdPartyLeaderGenericSpawnMarker,
                SpawnMarkerKind.ThirdPartyMarker,
                ScenarioMarkerTags.ForceThirdParty,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Leader.ToString()),
                ScenarioMarkerTags.MarkerId(string.Empty));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThirdPartyMemberGenericSpawnMarker,
                SpawnMarkerKind.ThirdPartyMarker,
                ScenarioMarkerTags.ForceThirdParty,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Member.ToString()),
                ScenarioMarkerTags.MarkerId(string.Empty));
            AssertSpawnMarkerPrototype(
                prototypes,
                ThirdPartyEntityGenericSpawnMarker,
                SpawnMarkerKind.ThirdPartyMarker,
                ScenarioMarkerTags.ForceThirdParty,
                ScenarioMarkerTags.Bucket(ThreatMarkerType.Entity.ToString()),
                ScenarioMarkerTags.MarkerId(string.Empty));
            AssertSpawnMarkerPrototype(
                prototypes,
                ClfSafehouseSpawnMarker,
                SpawnMarkerKind.ClfSafehouse,
                ScenarioMarkerTags.ForceClfSafehouse);
            AssertSpawnMarkerPrototype(
                prototypes,
                ClfCivilianColonistSpawnMarker,
                SpawnMarkerKind.ClfCivilianSpawn,
                ScenarioMarkerTags.ClfCivilianSpawn(ColonyCivilianJobId));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InsurgencyClfRoundGroupPrototypeMatchesLegacyAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var package = prototypes.Index(InsurgencyVotingChoices);
            var force = prototypes.Index(InsurgencyClfRoundGroup);
            var spawnPlan = force.Spawn;
            var preset = prototypes.Index<GamePresetPrototype>(InsurgencyPreset);
            var (planetId, planet) = GetFirstSupportedPlanet(prototypes, componentFactory, InsurgencyPreset);
            var plan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    InsurgencyPreset,
                    MarkerValidationPlayerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var adaptedClf = plan.Forces.Single(plannedForce => plannedForce.ForceKind == ScenarioForceKind.Clf);
            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    InsurgencyClfRoundGroup.Id,
                    MarkerValidationPlayerCount,
                    out var prototypeClf,
                    out var forceDiagnostic),
                Is.True,
                forceDiagnostic);
            Assert.That(prototypeClf, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(package.Presets, Does.Contain(InsurgencyPreset));
                Assert.That(package.SupportedPlanets, Is.EquivalentTo(preset.SupportedPlanets));
                Assert.That(package.Groups.Select(groupId => groupId.Id), Does.Contain(force.ID));
                Assert.That(plan.DeferredForceChoices.Select(choice => choice.ChoiceId), Does.Contain("GovforPlatoon"));

                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.Clf));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(adaptedClf.SourcePrototypeId));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(force.SourcePrototypeId, Is.EqualTo(AddClfRule.Id));
            });

            AssertPlannedForcesMatch(adaptedClf, prototypeClf!);
            AssertScenarioSpawnDefinitionMatchesAdapter(spawnPlan, adaptedClf.SpawnPlan);
            Assert.Multiple(() =>
            {
                var commandBucket = spawnPlan.BodyBuckets.Single(bucket => bucket.Bucket == "CLFCommand");
                var guerillaBucket = spawnPlan.BodyBuckets.Single(bucket => bucket.Bucket == "CLFGuerilla");

                Assert.That(commandBucket.Bodies["AU14JobCLFCellLeader"], Is.EqualTo(1));
                Assert.That(commandBucket.Bodies["AU14JobCLFPhysician"], Is.EqualTo(1));
                Assert.That(commandBucket.Bodies["AU14JobCLFSurgeon"], Is.EqualTo(1));
                Assert.That(guerillaBucket.Bodies["AU14JobCLFGuerilla"], Is.EqualTo(3));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InsurgencyVotingChoicesPrototypeResolverMatchesLegacyPlan()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var (planetId, planet) = GetFirstSupportedPlanet(prototypes, componentFactory, InsurgencyPreset);
            var adaptedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    InsurgencyPreset,
                    MarkerValidationPlayerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();

            Assert.That(
                generator.TryResolveVotingChoicesPrototype(
                    InsurgencyVotingChoices.Id,
                    InsurgencyPreset,
                    planetId,
                    planet.MapId,
                    MarkerValidationPlayerCount,
                    out var prototypePlan,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(prototypePlan, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(prototypePlan!.PresetId, Is.EqualTo(adaptedPlan.PresetId));
                Assert.That(prototypePlan.PlanetId, Is.EqualTo(adaptedPlan.PlanetId));
                Assert.That(prototypePlan.MapId, Is.EqualTo(adaptedPlan.MapId));
                Assert.That(prototypePlan.PlayerCount, Is.EqualTo(adaptedPlan.PlayerCount));
                Assert.That(prototypePlan.DeferredForceChoices, Is.Empty);
                Assert.That(prototypePlan.Diagnostics, Is.Empty);
                Assert.That(prototypePlan.SourceVotingChoicesIds, Is.EqualTo(new[] { InsurgencyVotingChoices.Id }));
                Assert.That(adaptedPlan.SourceVotingChoicesIds, Does.Contain(InsurgencyVotingChoices.Id));
            });

            AssertPlannedForcesMatch(
                prototypePlan!.Forces.Single(plannedForce => plannedForce.ForceKind == ScenarioForceKind.Clf),
                adaptedPlan.Forces.Single(plannedForce => plannedForce.ForceKind == ScenarioForceKind.Clf));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistressSignalVotingChoicesPrototypeResolverMatchesSeededLegacyChoices()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, DistressSignalSeededSlicePlanetId, out var planet),
                Is.True,
                $"{DistressSignalSeededSlicePlanetId} did not resolve to a planet map prototype.");

            var adaptedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    MarkerValidationPlayerCount,
                    PlanetId: DistressSignalSeededSlicePlanetId,
                    MapId: planet.MapId))
                .Single();

            Assert.That(
                generator.TryResolveVotingChoicesPrototype(
                    DistressSignalVotingChoices.Id,
                    DistressSignalPreset,
                    DistressSignalSeededSlicePlanetId,
                    planet.MapId,
                    MarkerValidationPlayerCount,
                    out var prototypePlan,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(prototypePlan, Is.Not.Null);

            var packageReport = generator.ValidateVotingChoicesPrototypeCoverage(
                DistressSignalVotingChoices.Id,
                DistressSignalPreset,
                DistressSignalSeededSlicePlanetId,
                planet.MapId,
                MarkerValidationPlayerCount);
            Assert.That(packageReport.IsValid, Is.True, packageReport.ToString());
            Assert.That(packageReport.Diagnostics, Is.Empty, packageReport.ToString());
            var validatedPlan = packageReport.Plans.Single();

            var package = prototypes.Index(DistressSignalVotingChoices);
            var planetChoice = package.PlanetChoices.Single(choice => choice.SupportsPlanet(DistressSignalSeededSlicePlanetId));
            var prototypeThreatChoice = prototypePlan!.DeferredForceChoices.Single(choice =>
                choice.ChoiceId.StartsWith("DeferredThreat:", StringComparison.Ordinal));
            var adaptedThreatChoice = adaptedPlan.DeferredForceChoices.Single(choice =>
                choice.ChoiceId.Equals(prototypeThreatChoice.ChoiceId, StringComparison.OrdinalIgnoreCase));
            var adaptedGovforChoice = adaptedPlan.DeferredForceChoices.Single(choice =>
                choice.ChoiceId.Equals("GovforPlatoon", StringComparison.OrdinalIgnoreCase));

            Assert.Multiple(() =>
            {
                Assert.That(package.Presets, Does.Contain(DistressSignalPreset));
                Assert.That(package.SupportedPlanets, Does.Contain(DistressSignalSeededSlicePlanetId));
                Assert.That(package.Groups, Is.Empty);
                Assert.That(package.DeferredForceChoices, Is.Empty);
                Assert.That(planetChoice.DeferredForceChoices, Has.Count.EqualTo(1));
                Assert.That(planetChoice.BackupDeferredForceChoices, Has.Count.EqualTo(1));
                Assert.That(prototypePlan.PresetId, Is.EqualTo(adaptedPlan.PresetId));
                Assert.That(prototypePlan.PlanetId, Is.EqualTo(adaptedPlan.PlanetId));
                Assert.That(prototypePlan.MapId, Is.EqualTo(adaptedPlan.MapId));
                Assert.That(prototypePlan.PlayerCount, Is.EqualTo(adaptedPlan.PlayerCount));
                Assert.That(prototypePlan.Diagnostics, Is.Empty);
                Assert.That(prototypePlan.SourceVotingChoicesIds, Is.EqualTo(new[] { DistressSignalVotingChoices.Id }));
                Assert.That(adaptedPlan.SourceVotingChoicesIds, Does.Contain(DistressSignalVotingChoices.Id));
                Assert.That(validatedPlan.Forces.Count, Is.EqualTo(prototypePlan.Forces.Count));
                Assert.That(validatedPlan.DeferredForceChoices.Count, Is.EqualTo(prototypePlan.DeferredForceChoices.Count));
                Assert.That(validatedPlan.SpawnMarkers, Is.Not.Empty);
                Assert.That(
                    prototypeThreatChoice.Candidates.Select(candidate => candidate.SourcePrototypeId),
                    Is.EqualTo(new[] { SelectedThreatAssignmentThreat.Id, DistressSignalAbominationThreat.Id }));
                Assert.That(adaptedGovforChoice.Candidates.Count, Is.GreaterThan(1));
            });

            foreach (var prototypeCandidate in prototypeThreatChoice.Candidates)
            {
                var adaptedCandidate = adaptedThreatChoice.Candidates.Single(candidate =>
                    candidate.SourcePrototypeId.Equals(prototypeCandidate.SourcePrototypeId, StringComparison.OrdinalIgnoreCase));
                var RoundGroupId = prototypeCandidate.SourcePrototypeId switch
                {
                    var source when source.Equals(SelectedThreatAssignmentThreat.Id, StringComparison.OrdinalIgnoreCase) =>
                        DistressSignalXenoRoundGroup.Id,
                    var source when source.Equals(DistressSignalAbominationThreat.Id, StringComparison.OrdinalIgnoreCase) =>
                        DistressSignalAbominationRoundGroup.Id,
                    _ => throw new InvalidOperationException($"Unexpected Distress Signal seeded threat '{prototypeCandidate.SourcePrototypeId}'."),
                };
                Assert.That(
                    generator.TryResolveRoundGroupPrototype(
                        RoundGroupId,
                        MarkerValidationPlayerCount,
                        out var RoundGroupCandidate,
                        out var RoundGroupDiagnostic),
                    Is.True,
                    RoundGroupDiagnostic);
                Assert.That(RoundGroupCandidate, Is.Not.Null);

                AssertPlannedForcesMatch(prototypeCandidate, RoundGroupCandidate!);
                AssertPlannedForcesMatch(prototypeCandidate, adaptedCandidate);
            }

            AssertReservationPolicyMatchesSmallestThreatCandidate(prototypeThreatChoice);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColonyFallVotingChoicesPrototypeResolverMatchesSeededLegacyThreatChoice()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var preset = prototypes.Index<GamePresetPrototype>(ColonyFallPreset);
            var playerCount = GetPresetValidPlayerCount(preset);
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, ColonyFallSeededSlicePlanetId, out var planet),
                Is.True,
                $"{ColonyFallSeededSlicePlanetId} did not resolve to a planet map prototype.");

            var adaptedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    ColonyFallPreset,
                    playerCount,
                    PlanetId: ColonyFallSeededSlicePlanetId,
                    MapId: planet.MapId))
                .Single();

            Assert.That(
                generator.TryResolveVotingChoicesPrototype(
                    ColonyFallVotingChoices.Id,
                    ColonyFallPreset,
                    ColonyFallSeededSlicePlanetId,
                    planet.MapId,
                    playerCount,
                    out var prototypePlan,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(prototypePlan, Is.Not.Null);

            var packageReport = generator.ValidateVotingChoicesPrototypeCoverage(
                ColonyFallVotingChoices.Id,
                ColonyFallPreset,
                ColonyFallSeededSlicePlanetId,
                planet.MapId,
                playerCount);
            Assert.That(packageReport.IsValid, Is.True, packageReport.ToString());
            Assert.That(packageReport.Diagnostics, Is.Empty, packageReport.ToString());
            var validatedPlan = packageReport.Plans.Single();

            var package = prototypes.Index(ColonyFallVotingChoices);
            var planetChoice = package.PlanetChoices.Single(choice => choice.SupportsPlanet(ColonyFallSeededSlicePlanetId));
            var prototypeThreatChoice = prototypePlan!.DeferredForceChoices.Single();
            var adaptedThreatChoice = adaptedPlan.DeferredForceChoices.Single(choice =>
                choice.ChoiceId.Equals(prototypeThreatChoice.ChoiceId, StringComparison.OrdinalIgnoreCase));
            var expectedThreatCandidates = new[]
            {
                ColonyFallCultistThreat.Id,
                ColonyFallXenoThreat.Id,
                WendigoThreat.Id,
                ColonyFallAbominationThreat.Id,
            };

            Assert.Multiple(() =>
            {
                Assert.That(package.Presets, Does.Contain(ColonyFallPreset));
                Assert.That(package.SupportedPlanets, Does.Contain(ColonyFallSeededSlicePlanetId));
                Assert.That(package.Groups, Is.Empty);
                Assert.That(package.DeferredForceChoices, Is.Empty);
                Assert.That(planetChoice.DeferredForceChoices, Has.Count.EqualTo(1));
                Assert.That(planetChoice.BackupDeferredForceChoices, Has.Count.EqualTo(1));
                Assert.That(prototypePlan.PresetId, Is.EqualTo(adaptedPlan.PresetId));
                Assert.That(prototypePlan.PlanetId, Is.EqualTo(adaptedPlan.PlanetId));
                Assert.That(prototypePlan.MapId, Is.EqualTo(adaptedPlan.MapId));
                Assert.That(prototypePlan.PlayerCount, Is.EqualTo(adaptedPlan.PlayerCount));
                Assert.That(prototypePlan.Diagnostics, Is.Empty);
                Assert.That(prototypePlan.SourceVotingChoicesIds, Is.EqualTo(new[] { ColonyFallVotingChoices.Id }));
                Assert.That(adaptedPlan.SourceVotingChoicesIds, Does.Contain(ColonyFallVotingChoices.Id));
                Assert.That(validatedPlan.Forces.Count, Is.EqualTo(prototypePlan.Forces.Count));
                Assert.That(validatedPlan.DeferredForceChoices.Count, Is.EqualTo(prototypePlan.DeferredForceChoices.Count));
                Assert.That(validatedPlan.SpawnMarkers, Is.Not.Empty);
                Assert.That(prototypeThreatChoice.ChoiceId, Is.EqualTo($"DeferredThreat:{ColonyFallPreset}:{ColonyFallSeededSlicePlanetId}"));
                Assert.That(
                    prototypeThreatChoice.Candidates.Select(candidate => candidate.SourcePrototypeId),
                    Is.EqualTo(expectedThreatCandidates));
                Assert.That(
                    adaptedThreatChoice.Candidates.Select(candidate => candidate.SourcePrototypeId),
                    Is.EqualTo(expectedThreatCandidates));
            });

            foreach (var prototypeCandidate in prototypeThreatChoice.Candidates)
            {
                var adaptedCandidate = adaptedThreatChoice.Candidates.Single(candidate =>
                    candidate.SourcePrototypeId.Equals(prototypeCandidate.SourcePrototypeId, StringComparison.OrdinalIgnoreCase));
                var RoundGroupId = prototypeCandidate.SourcePrototypeId switch
                {
                    var source when source.Equals(ColonyFallCultistThreat.Id, StringComparison.OrdinalIgnoreCase) =>
                        ColonyFallCultistRoundGroup.Id,
                    var source when source.Equals(ColonyFallXenoThreat.Id, StringComparison.OrdinalIgnoreCase) =>
                        ColonyFallXenoRoundGroup.Id,
                    var source when source.Equals(WendigoThreat.Id, StringComparison.OrdinalIgnoreCase) =>
                        ColonyFallWendigoRoundGroup.Id,
                    var source when source.Equals(ColonyFallAbominationThreat.Id, StringComparison.OrdinalIgnoreCase) =>
                        ColonyFallAbominationRoundGroup.Id,
                    _ => throw new InvalidOperationException($"Unexpected Colony Fall seeded threat '{prototypeCandidate.SourcePrototypeId}'."),
                };
                Assert.That(
                    generator.TryResolveRoundGroupPrototype(
                        RoundGroupId,
                        playerCount,
                        out var RoundGroupCandidate,
                        out var RoundGroupDiagnostic),
                    Is.True,
                    RoundGroupDiagnostic);
                Assert.That(RoundGroupCandidate, Is.Not.Null);

                AssertPlannedForcesMatch(prototypeCandidate, RoundGroupCandidate!);
                AssertPlannedForcesMatch(prototypeCandidate, adaptedCandidate);
            }

            AssertReservationPolicyMatchesSmallestThreatCandidate(prototypeThreatChoice);
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(ExactThreatSliceCases))]
    public async Task VotingChoicesThreatSlicesApplyToExactAdapterCandidates(
        string VotingChoicesId,
        string presetId,
        string planetId,
        string[] expectedThreatCandidates)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var preset = prototypes.Index<GamePresetPrototype>(presetId);
            var playerCount = GetPresetValidPlayerCount(preset);
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var adaptedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    presetId,
                    playerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();

            Assert.That(
                generator.TryResolveVotingChoicesPrototype(
                    VotingChoicesId,
                    presetId,
                    planetId,
                    planet.MapId,
                    playerCount,
                    out var prototypePlan,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(prototypePlan, Is.Not.Null);

            var packageReport = generator.ValidateVotingChoicesPrototypeCoverage(
                VotingChoicesId,
                presetId,
                planetId,
                planet.MapId,
                playerCount);
            Assert.That(packageReport.IsValid, Is.True, packageReport.ToString());
            Assert.That(packageReport.Diagnostics, Is.Empty, packageReport.ToString());

            var package = prototypes.Index<VotingChoicesPrototype>(VotingChoicesId);
            var packageDeferredForceChoices = package.PlanetChoices.Count == 0
                ? package.DeferredForceChoices
                : package.PlanetChoices.Single(choice => choice.SupportsPlanet(planetId)).DeferredForceChoices;
            var prototypeThreatChoice = prototypePlan!.DeferredForceChoices.Single();
            var adaptedThreatChoice = adaptedPlan.DeferredForceChoices.Single(choice =>
                choice.ChoiceId.Equals(prototypeThreatChoice.ChoiceId, StringComparison.OrdinalIgnoreCase));

            Assert.Multiple(() =>
            {
                Assert.That(package.Presets, Does.Contain(presetId));
                Assert.That(package.SupportedPlanets, Does.Contain(planetId));
                Assert.That(package.Groups, Is.Empty);
                Assert.That(packageDeferredForceChoices, Has.Count.EqualTo(1));
                Assert.That(prototypePlan.PresetId, Is.EqualTo(adaptedPlan.PresetId));
                Assert.That(prototypePlan.PlanetId, Is.EqualTo(adaptedPlan.PlanetId));
                Assert.That(prototypePlan.MapId, Is.EqualTo(adaptedPlan.MapId));
                Assert.That(prototypePlan.PlayerCount, Is.EqualTo(adaptedPlan.PlayerCount));
                Assert.That(prototypePlan.Diagnostics, Is.Empty);
                Assert.That(prototypePlan.SourceVotingChoicesIds, Is.EqualTo(new[] { VotingChoicesId }));
                Assert.That(adaptedPlan.SourceVotingChoicesIds, Does.Contain(VotingChoicesId));
                Assert.That(packageReport.Plans.Single().DeferredForceChoices.Count, Is.EqualTo(prototypePlan.DeferredForceChoices.Count));
                Assert.That(prototypeThreatChoice.ChoiceId, Is.EqualTo($"DeferredThreat:{presetId}:{planetId}"));
                Assert.That(
                    prototypeThreatChoice.Candidates.Select(candidate => candidate.SourcePrototypeId),
                    Is.EqualTo(expectedThreatCandidates));
                Assert.That(
                    adaptedThreatChoice.Candidates.Select(candidate => candidate.SourcePrototypeId),
                    Is.EqualTo(expectedThreatCandidates));
            });

            foreach (var prototypeCandidate in prototypeThreatChoice.Candidates)
            {
                var adaptedCandidate = adaptedThreatChoice.Candidates.Single(candidate =>
                    candidate.SourcePrototypeId.Equals(prototypeCandidate.SourcePrototypeId, StringComparison.OrdinalIgnoreCase));
                AssertPlannedForcesMatch(prototypeCandidate, adaptedCandidate);
            }

            AssertReservationPolicyMatchesSmallestThreatCandidate(prototypeThreatChoice);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColonyFallInlineQuietBackupRecordsRuntimeOwnershipForNoThreatPlan()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var preset = prototypes.Index<GamePresetPrototype>(ColonyFallPreset);
            var playerCount = GetPresetValidPlayerCount(preset);
            const string planetId = "AuPlanetChances";
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var package = prototypes.Index(ColonyFallVotingChoices);
            var planetChoice = package.PlanetChoices.Single(choice => choice.SupportsPlanet(planetId));

            Assert.That(
                generator.TryResolveVotingBackup(
                    ColonyFallPreset,
                    planetId,
                    planet.MapId,
                    playerCount,
                    out var backupPlan,
                    out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(backupPlan, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(planetChoice.BackupQuiet, Is.True);
                Assert.That(backupPlan!.Forces, Is.Empty);
                Assert.That(backupPlan.DeferredForceChoices, Is.Empty);
                Assert.That(backupPlan.Diagnostics, Is.Empty);
                Assert.That(backupPlan.SourceVotingChoicesIds, Is.EqualTo(new[] { ColonyFallVotingChoices.Id }));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VotingBackupPackagePrototypesCoverTargetPresetPlanets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();

            foreach (var presetId in new[] { DistressSignalPreset, InsurgencyPreset, ColonyFallPreset })
            {
                var preset = prototypes.Index<GamePresetPrototype>(presetId);
                var playerCount = GetPresetValidPlayerCount(preset);
                foreach (var planetId in preset.SupportedPlanets)
                {
                    if (!TryGetPlanet(prototypes, componentFactory, planetId, out var planet))
                        continue;

                    Assert.That(
                        generator.TryResolveVotingBackup(
                            presetId,
                            planetId,
                            planet.MapId,
                            playerCount,
                            out var selectedFallbackPlan,
                            out var fallbackDiagnostic),
                        Is.True,
                        $"{presetId}/{planetId} has no marker-valid Voting Choices fallback. {fallbackDiagnostic}");
                    Assert.That(selectedFallbackPlan, Is.Not.Null);

                    Assert.That(selectedFallbackPlan!.PresetId, Is.EqualTo(presetId));
                    Assert.That(selectedFallbackPlan.PlanetId, Is.EqualTo(planetId));
                    Assert.That(selectedFallbackPlan.SourceVotingChoicesIds, Is.Not.Empty);
                    foreach (var choice in selectedFallbackPlan.DeferredForceChoices)
                    {
                        Assert.That(choice.ChoiceId, Does.Not.Contain("{"));
                        Assert.That(choice.ChoiceId, Does.Contain(planetId));
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistressSignalXenoRoundGroupPrototypePreservesLegacyScaling()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index(SelectedThreatAssignmentThreat);
            var partySpawn = prototypes.Index(threat.RoundStartSpawn);
            var force = prototypes.Index(DistressSignalXenoRoundGroup);
            var spawnPlan = force.Spawn;
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, MarkerValidationPlayerCount);
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveSelectedThreatForce(request, out var adaptedForce, out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(adaptedForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.Hostile));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(threat.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(force.WinConditionRuleIds, Is.EquivalentTo(threat.WinConditions));
                Assert.That(force.Timing.DelayMinSeconds, Is.Null);
                Assert.That(force.Timing.DelayMaxSeconds, Is.Null);

                Assert.That(adaptedForce!.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(adaptedForce.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
            });

            AssertScenarioSpawnDefinitionMatchesAdapter(spawnPlan, adaptedForce!.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: false);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 30);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 40);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 100);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 200);

            var memberBucket = spawnPlan.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Member.ToString());
            Assert.Multiple(() =>
            {
                Assert.That(memberBucket.Bodies["CMXenoLarva"], Is.EqualTo(5));
                Assert.That(memberBucket.Scaling.Keys, Is.EquivalentTo(partySpawn.Scaling.Keys));
                Assert.That(memberBucket.Scaling["CMXenoLarva"], Is.EqualTo(partySpawn.Scaling["CMXenoLarva"]));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistressSignalAbominationRoundGroupPrototypePreservesEntityBucketAndCustomMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index(DistressSignalAbominationThreat);
            var partySpawn = prototypes.Index(threat.RoundStartSpawn);
            var force = prototypes.Index(DistressSignalAbominationRoundGroup);
            var spawnPlan = force.Spawn;
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, MarkerValidationPlayerCount);
            var request = new ScenarioPlanValidationRequest(
                "DistressSignal",
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveSelectedThreatForce(request, out var adaptedForce, out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(adaptedForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.Hostile));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(threat.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(force.WinConditionRuleIds, Is.EquivalentTo(threat.WinConditions));

                Assert.That(adaptedForce!.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(adaptedForce.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
            });

            AssertScenarioSpawnDefinitionMatchesAdapter(spawnPlan, adaptedForce!.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: false);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Entity, partySpawn.EntitiesToSpawn, partySpawn);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 25);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 50);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 100);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 180);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(40)]
    [TestCase(100)]
    [TestCase(200)]
    public async Task RoundGroupPrototypeResolverMatchesScaledXenoAdapter(int playerCount)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var generator = server.System<ScenarioPlanSystem>();
            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    DistressSignalXenoRoundGroup.Id,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.That(
                generator.TryResolveSelectedThreatForce(
                    new ScenarioPlanValidationRequest(
                        "DistressSignal",
                        playerCount,
                        SelectedThreatId: SelectedThreatAssignmentThreat.Id),
                    out var adaptedForce,
                    out var adaptedDiagnostic),
                Is.True,
                adaptedDiagnostic);
            Assert.That(adaptedForce, Is.Not.Null);

            AssertPrototypeForceMatchesThreatAdapter(prototypeForce!, adaptedForce!);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(50)]
    [TestCase(100)]
    public async Task RoundGroupPrototypeResolverMatchesAbominationAdapter(int playerCount)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var generator = server.System<ScenarioPlanSystem>();
            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    DistressSignalAbominationRoundGroup.Id,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.That(
                generator.TryResolveSelectedThreatForce(
                    new ScenarioPlanValidationRequest(
                        "DistressSignal",
                        playerCount,
                        SelectedThreatId: DistressSignalAbominationThreat.Id),
                    out var adaptedForce,
                    out var adaptedDiagnostic),
                Is.True,
                adaptedDiagnostic);
            Assert.That(adaptedForce, Is.Not.Null);

            AssertPrototypeForceMatchesThreatAdapter(prototypeForce!, adaptedForce!);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundGroupPrototypeResolverMatchesClfAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var (planetId, planet) = GetFirstSupportedPlanet(prototypes, componentFactory, InsurgencyPreset);

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    InsurgencyClfRoundGroup.Id,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            var adaptedForce = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    InsurgencyPreset,
                    MarkerValidationPlayerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single()
                .Forces
                .Single(plannedForce => plannedForce.ForceKind == ScenarioForceKind.Clf);

            AssertPlannedForcesMatch(prototypeForce!, adaptedForce);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundGroupPrototypeResolverMatchesGovforPlatoonAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var (planetId, planet) = GetFirstSupportedPlanet(prototypes, componentFactory, "DistressSignal");
            var adaptedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    "DistressSignal",
                    MarkerValidationPlayerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var adaptedForce = adaptedPlan.DeferredForceChoices
                .Single(choice => choice.ChoiceId == "GovforPlatoon")
                .Candidates
                .Single(candidate => candidate.SourcePrototypeId == "UPP");

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    GovforUppPlatoonRoundGroup.Id,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            AssertPlannedForcesMatch(prototypeForce!, adaptedForce);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundGroupPrototypeResolverMatchesUscmGovforPlatoonAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            const string planetId = "AUPlanetCorsatStation";
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var adaptedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    MarkerValidationPlayerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var adaptedForce = adaptedPlan.DeferredForceChoices
                .Single(choice => choice.ChoiceId == "GovforPlatoon")
                .Candidates
                .Single(candidate => candidate.SourcePrototypeId == "USCM");

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    GovforUscmPlatoonRoundGroup.Id,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            AssertPlannedForcesMatch(prototypeForce!, adaptedForce);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundGroupPrototypeResolverMatchesRmcGovforPlatoonAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            const string planetId = "AUPlanetCorsatStation";
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var adaptedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    MarkerValidationPlayerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var adaptedForce = adaptedPlan.DeferredForceChoices
                .Single(choice => choice.ChoiceId == "GovforPlatoon")
                .Candidates
                .Single(candidate => candidate.SourcePrototypeId == "RMC");

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    GovforRmcPlatoonRoundGroup.Id,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            AssertPlannedForcesMatch(prototypeForce!, adaptedForce);
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(RemainingGovforPlatoonRoundGroupCases))]
    public async Task RoundGroupPrototypeResolverMatchesRemainingGovforPlatoonAdapter(
        string planetId,
        string platoonId,
        string RoundGroupId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var adaptedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    MarkerValidationPlayerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var adaptedForce = adaptedPlan.DeferredForceChoices
                .Single(choice => choice.ChoiceId == "GovforPlatoon")
                .Candidates
                .Single(candidate => candidate.SourcePrototypeId == platoonId);

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    RoundGroupId,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            AssertPlannedForcesMatch(prototypeForce!, adaptedForce);
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(FixedOpforPlatoonRoundGroupCases))]
    public async Task RoundGroupPrototypeResolverMatchesFixedOpforPlatoonAdapter(
        string planetId,
        string platoonId,
        string RoundGroupId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var adaptedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    MarkerValidationPlayerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var adaptedForce = adaptedPlan.Forces
                .Single(candidate =>
                    candidate.ForceKind == ScenarioForceKind.Platoon &&
                    candidate.ForceId.Equals($"OpforPlatoon:{platoonId}", StringComparison.OrdinalIgnoreCase));

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    RoundGroupId,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            AssertPlannedForcesMatch(prototypeForce!, adaptedForce);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColonyFallCultistRoundGroupPrototypeMatchesLegacyAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index(ColonyFallCultistThreat);
            var partySpawn = prototypes.Index(threat.RoundStartSpawn);
            var force = prototypes.Index(ColonyFallCultistRoundGroup);
            var spawnPlan = force.Spawn;
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, MarkerValidationPlayerCount);
            var request = new ScenarioPlanValidationRequest(
                ColonyFallPreset,
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    ColonyFallCultistRoundGroup.Id,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);
            Assert.That(
                generator.TryResolveSelectedThreatForce(request, out var adaptedForce, out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(adaptedForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.Hostile));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(threat.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(force.WinConditionRuleIds, Is.EquivalentTo(threat.WinConditions));
                Assert.That(force.Timing.DelayMinSeconds, Is.EqualTo(threat.SpawnDelayMin));
                Assert.That(force.Timing.DelayMaxSeconds, Is.EqualTo(threat.SpawnDelayMax));

                Assert.That(adaptedForce!.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(adaptedForce.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
            });

            AssertPrototypeForceMatchesThreatAdapter(prototypeForce!, adaptedForce!);
            AssertScenarioSpawnDefinitionMatchesAdapter(spawnPlan, adaptedForce!.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: false);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColonyFallCultistMarkerRoundGroupPrototypeMatchesLegacyAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index(CustomMarkerThreat);
            var partySpawn = prototypes.Index(threat.RoundStartSpawn);
            var force = prototypes.Index(ColonyFallCultistMarkerRoundGroup);
            var spawnPlan = force.Spawn;
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, MarkerValidationPlayerCount);
            var request = new ScenarioPlanValidationRequest(
                ColonyFallPreset,
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    ColonyFallCultistMarkerRoundGroup.Id,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);
            Assert.That(
                generator.TryResolveSelectedThreatForce(request, out var adaptedForce, out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(adaptedForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.Hostile));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(threat.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(force.WinConditionRuleIds, Is.EquivalentTo(threat.WinConditions));
                Assert.That(force.Timing.DelayMinSeconds, Is.EqualTo(threat.SpawnDelayMin));
                Assert.That(force.Timing.DelayMaxSeconds, Is.EqualTo(threat.SpawnDelayMax));

                Assert.That(adaptedForce!.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(adaptedForce.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
            });

            AssertPrototypeForceMatchesThreatAdapter(prototypeForce!, adaptedForce!);
            AssertScenarioSpawnDefinitionMatchesAdapter(spawnPlan, adaptedForce!.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: false);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 60);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 100);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColonyFallXenoRoundGroupPrototypeMatchesLegacyAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index(ColonyFallXenoThreat);
            var partySpawn = prototypes.Index(threat.RoundStartSpawn);
            var force = prototypes.Index(ColonyFallXenoRoundGroup);
            var spawnPlan = force.Spawn;
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, MarkerValidationPlayerCount);
            var request = new ScenarioPlanValidationRequest(
                ColonyFallPreset,
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    ColonyFallXenoRoundGroup.Id,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);
            Assert.That(
                generator.TryResolveSelectedThreatForce(request, out var adaptedForce, out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(adaptedForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.Hostile));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(threat.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(force.WinConditionRuleIds, Is.EquivalentTo(threat.WinConditions));
                Assert.That(force.Timing.DelayMinSeconds, Is.EqualTo(threat.SpawnDelayMin));
                Assert.That(force.Timing.DelayMaxSeconds, Is.EqualTo(threat.SpawnDelayMax));

                Assert.That(adaptedForce!.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(adaptedForce.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
            });

            AssertPrototypeForceMatchesThreatAdapter(prototypeForce!, adaptedForce!);
            AssertScenarioSpawnDefinitionMatchesAdapter(spawnPlan, adaptedForce!.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: false);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 40);
            AssertPrototypeBodyCountMatchesLegacyPartySpawn(spawnPlan, partySpawn, playerCount: 100);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColonyFallWendigoRoundGroupPrototypeMatchesLegacyAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var threat = prototypes.Index(WendigoThreat);
            var partySpawn = prototypes.Index(threat.RoundStartSpawn);
            var force = prototypes.Index(ColonyFallWendigoRoundGroup);
            var spawnPlan = force.Spawn;
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, MarkerValidationPlayerCount);
            var request = new ScenarioPlanValidationRequest(
                "ColonyFall",
                MarkerValidationPlayerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveSelectedThreatForce(request, out var adaptedForce, out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(adaptedForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.Hostile));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(threat.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(force.WinConditionRuleIds, Is.EquivalentTo(threat.WinConditions));

                Assert.That(adaptedForce!.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(adaptedForce.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
                Assert.That(spawnPlan.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Leader.ToString()).Bodies["AU14Wendigo"], Is.EqualTo(1));
            });

            AssertScenarioSpawnDefinitionMatchesAdapter(spawnPlan, adaptedForce!.SpawnPlan);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColonyFallAbominationRoundGroupPrototypeMatchesLegacyAdapter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<ScenarioPlanSystem>();
            var preset = prototypes.Index<GamePresetPrototype>(ColonyFallPreset);
            var playerCount = GetPresetValidPlayerCount(preset);
            var threat = prototypes.Index(ColonyFallAbominationThreat);
            var partySpawn = prototypes.Index(threat.RoundStartSpawn);
            var force = prototypes.Index(ColonyFallAbominationRoundGroup);
            var spawnPlan = force.Spawn;
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
            var request = new ScenarioPlanValidationRequest(
                ColonyFallPreset,
                playerCount,
                SelectedThreatId: threat.ID);

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    ColonyFallAbominationRoundGroup.Id,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);
            Assert.That(
                generator.TryResolveSelectedThreatForce(request, out var adaptedForce, out var diagnostic),
                Is.True,
                diagnostic);
            Assert.That(adaptedForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.Hostile));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(threat.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(force.WinConditionRuleIds, Is.EquivalentTo(threat.WinConditions));
                Assert.That(force.Timing.DelayMinSeconds, Is.EqualTo(threat.SpawnDelayMin));
                Assert.That(force.Timing.DelayMaxSeconds, Is.EqualTo(threat.SpawnDelayMax));

                Assert.That(adaptedForce!.LeaderBodies, Is.EqualTo(legacyBodyCount.Leaders));
                Assert.That(adaptedForce.MemberBodies, Is.EqualTo(legacyBodyCount.Members));
                Assert.That(spawnPlan.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Entity.ToString()).Bodies["AU14AbominationFleshNest"], Is.EqualTo(1));
            });

            AssertPrototypeForceMatchesThreatAdapter(prototypeForce!, adaptedForce!);
            AssertScenarioSpawnDefinitionMatchesAdapter(spawnPlan, adaptedForce!.SpawnPlan);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistressSignalWypmcThirdPartyRoundGroupPrototypeMatchesLegacyPartySpawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index(DropshipThirdParty);
            var partySpawn = prototypes.Index(thirdParty.PartySpawn);
            var force = prototypes.Index(DistressSignalWypmcPartyRoundGroup);
            var spawnPlan = force.Spawn;
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount: 40);
            var (planetId, planet) = GetFirstSupportedPlanet(prototypes, componentFactory, DistressSignalPreset);
            var generatedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    MarkerValidationPlayerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var generatedThirdPartyForces = generatedPlan.Forces
                .Where(plannedForce =>
                    plannedForce.ForceKind == ScenarioForceKind.ThirdParty &&
                    plannedForce.SourcePrototypeId.Equals(DropshipThirdParty.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    DistressSignalWypmcPartyRoundGroup.Id,
                    MarkerValidationPlayerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.ThirdParty));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(thirdParty.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(spawnPlan.AllowsUnderfill, Is.False);
                Assert.That(generatedThirdPartyForces, Is.Not.Empty);
            });

            foreach (var generatedForce in generatedThirdPartyForces)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(generatedForce.ForceId, Does.StartWith("ThirdParty:"));
                    Assert.That(generatedForce.ForceId, Does.Not.Contain("Prototype"));
                    Assert.That(generatedForce.ForceKind, Is.EqualTo(prototypeForce!.ForceKind));
                    Assert.That(generatedForce.SourcePrototypeId, Is.EqualTo(prototypeForce.SourcePrototypeId));
                    Assert.That(generatedForce.WinConditionRuleIds, Is.EquivalentTo(prototypeForce.WinConditionRuleIds));
                    Assert.That(generatedForce.Timing, Is.EqualTo(prototypeForce.Timing));
                });
                AssertResolvedSpawnPlansMatch(prototypeForce!.SpawnPlan, generatedForce.SpawnPlan);
            }

            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: true);
            Assert.Multiple(() =>
            {
                var leaderBucket = spawnPlan.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Leader.ToString());
                var memberBucket = spawnPlan.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Member.ToString());

                Assert.That(leaderBucket.Bodies["AU14JobWYPMCPartyLeader"], Is.EqualTo(1));
                Assert.That(memberBucket.Bodies["AU14JobWYPMCPartyEngineer"], Is.EqualTo(1));
                Assert.That(memberBucket.Bodies["AU14JobWYPMCPartyMedic"], Is.EqualTo(1));
                Assert.That(memberBucket.Bodies["AU14JobWYPMCPartySmartgunner"], Is.EqualTo(1));
                Assert.That(memberBucket.Bodies["AU14JobWYPMCPartyContractorM42A"], Is.EqualTo(1));
                Assert.That(memberBucket.Bodies["AU14JobWYPMCPartyContractorNSG23"], Is.EqualTo(1));
                Assert.That(memberBucket.Bodies["AU14JobWYPMCPartyContractorM39B2"], Is.EqualTo(1));
                Assert.That(memberBucket.Bodies["AU14JobWYPMCPartyContractor"], Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistressSignalUsArmyAltThirdPartyRoundGroupPrototypeMatchesLegacyPartySpawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index(GroundThirdParty);
            var partySpawn = prototypes.Index(thirdParty.PartySpawn);
            var force = prototypes.Index(DistressSignalUsArmyAltPartyRoundGroup);
            var spawnPlan = force.Spawn;
            const int playerCount = 40;
            const string planetId = "AuPlanetShivasSnowball";
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var generatedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    playerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var generatedThirdPartyForce = generatedPlan.Forces
                .Single(plannedForce =>
                    plannedForce.ForceKind == ScenarioForceKind.ThirdParty &&
                    plannedForce.SourcePrototypeId.Equals(GroundThirdParty.Id, StringComparison.OrdinalIgnoreCase));

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    DistressSignalUsArmyAltPartyRoundGroup.Id,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.ThirdParty));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(thirdParty.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(spawnPlan.AllowsUnderfill, Is.False);
                Assert.That(generatedThirdPartyForce.ForceId, Is.EqualTo($"ThirdParty:{SelectedThreatAssignmentThreat.Id}:{GroundThirdParty.Id}"));
                Assert.That(generatedThirdPartyForce.ForceId, Does.Not.Contain("Prototype"));
                Assert.That(generatedThirdPartyForce.ForceKind, Is.EqualTo(prototypeForce!.ForceKind));
                Assert.That(generatedThirdPartyForce.SourcePrototypeId, Is.EqualTo(prototypeForce.SourcePrototypeId));
                Assert.That(generatedThirdPartyForce.WinConditionRuleIds, Is.EquivalentTo(prototypeForce.WinConditionRuleIds));
                Assert.That(generatedThirdPartyForce.Timing, Is.EqualTo(prototypeForce.Timing));
            });

            AssertResolvedSpawnPlansMatch(prototypeForce!.SpawnPlan, generatedThirdPartyForce.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: true);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(UsArmyThirdPartyRoundGroupCases))]
    public async Task DistressSignalUsArmyThirdPartyRoundGroupPrototypesMatchLegacyPartySpawn(
        string thirdPartyId,
        string RoundGroupId,
        string spawnPlanId,
        int playerCount)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(thirdPartyId);
            var partySpawn = prototypes.Index(thirdParty.PartySpawn);
            var force = prototypes.Index<RoundGroupPrototype>(RoundGroupId);
            var spawnPlan = force.Spawn;
            const string planetId = "AuPlanetShivasSnowball";
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var generatedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    playerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var generatedThirdPartyForce = generatedPlan.Forces
                .Single(plannedForce =>
                    plannedForce.ForceKind == ScenarioForceKind.ThirdParty &&
                    plannedForce.SourcePrototypeId.Equals(thirdPartyId, StringComparison.OrdinalIgnoreCase));

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    RoundGroupId,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.ThirdParty));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(thirdParty.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(spawnPlan.AllowsUnderfill, Is.False);
                Assert.That(generatedThirdPartyForce.ForceId, Is.EqualTo($"ThirdParty:{SelectedThreatAssignmentThreat.Id}:{thirdPartyId}"));
                Assert.That(generatedThirdPartyForce.ForceId, Does.Not.Contain("Prototype"));
                Assert.That(generatedThirdPartyForce.ForceKind, Is.EqualTo(prototypeForce!.ForceKind));
                Assert.That(generatedThirdPartyForce.SourcePrototypeId, Is.EqualTo(prototypeForce.SourcePrototypeId));
                Assert.That(generatedThirdPartyForce.WinConditionRuleIds, Is.EquivalentTo(prototypeForce.WinConditionRuleIds));
                Assert.That(generatedThirdPartyForce.Timing, Is.EqualTo(prototypeForce.Timing));
            });

            AssertResolvedSpawnPlansMatch(prototypeForce!.SpawnPlan, generatedThirdPartyForce.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: true);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(UppGromThirdPartyRoundGroupCases))]
    public async Task DistressSignalUppGromThirdPartyRoundGroupPrototypesMatchLegacyPartySpawn(
        string thirdPartyId,
        string RoundGroupId,
        string spawnPlanId,
        int playerCount)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(thirdPartyId);
            var partySpawn = prototypes.Index(thirdParty.PartySpawn);
            var force = prototypes.Index<RoundGroupPrototype>(RoundGroupId);
            var spawnPlan = force.Spawn;
            const string planetId = "AUPlanetTrijent";
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var generatedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    playerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var generatedThirdPartyForce = generatedPlan.Forces
                .Single(plannedForce =>
                    plannedForce.ForceKind == ScenarioForceKind.ThirdParty &&
                    plannedForce.SourcePrototypeId.Equals(thirdPartyId, StringComparison.OrdinalIgnoreCase));

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    RoundGroupId,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.ThirdParty));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(thirdParty.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(spawnPlan.AllowsUnderfill, Is.False);
                Assert.That(generatedThirdPartyForce.ForceId, Is.EqualTo($"ThirdParty:{SelectedThreatAssignmentThreat.Id}:{thirdPartyId}"));
                Assert.That(generatedThirdPartyForce.ForceId, Does.Not.Contain("Prototype"));
                Assert.That(generatedThirdPartyForce.ForceKind, Is.EqualTo(prototypeForce!.ForceKind));
                Assert.That(generatedThirdPartyForce.SourcePrototypeId, Is.EqualTo(prototypeForce.SourcePrototypeId));
                Assert.That(generatedThirdPartyForce.WinConditionRuleIds, Is.EquivalentTo(prototypeForce.WinConditionRuleIds));
                Assert.That(generatedThirdPartyForce.Timing, Is.EqualTo(prototypeForce.Timing));
            });

            AssertResolvedSpawnPlansMatch(prototypeForce!.SpawnPlan, generatedThirdPartyForce.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: true);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(ForeconThirdPartyRoundGroupCases))]
    public async Task DistressSignalForeconThirdPartyRoundGroupPrototypesMatchLegacyPartySpawn(
        string thirdPartyId,
        string RoundGroupId,
        string spawnPlanId,
        int playerCount)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(thirdPartyId);
            var partySpawn = prototypes.Index(thirdParty.PartySpawn);
            var force = prototypes.Index<RoundGroupPrototype>(RoundGroupId);
            var spawnPlan = force.Spawn;
            const string planetId = "AUPlanetTrijent";
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var generatedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    playerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var generatedThirdPartyForce = generatedPlan.Forces
                .Single(plannedForce =>
                    plannedForce.ForceKind == ScenarioForceKind.ThirdParty &&
                    plannedForce.SourcePrototypeId.Equals(thirdPartyId, StringComparison.OrdinalIgnoreCase));

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    RoundGroupId,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.ThirdParty));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(thirdParty.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(spawnPlan.AllowsUnderfill, Is.False);
                Assert.That(generatedThirdPartyForce.ForceId, Is.EqualTo($"ThirdParty:{SelectedThreatAssignmentThreat.Id}:{thirdPartyId}"));
                Assert.That(generatedThirdPartyForce.ForceId, Does.Not.Contain("Prototype"));
                Assert.That(generatedThirdPartyForce.ForceKind, Is.EqualTo(prototypeForce!.ForceKind));
                Assert.That(generatedThirdPartyForce.SourcePrototypeId, Is.EqualTo(prototypeForce.SourcePrototypeId));
                Assert.That(generatedThirdPartyForce.WinConditionRuleIds, Is.EquivalentTo(prototypeForce.WinConditionRuleIds));
                Assert.That(generatedThirdPartyForce.Timing, Is.EqualTo(prototypeForce.Timing));
            });

            AssertResolvedSpawnPlansMatch(prototypeForce!.SpawnPlan, generatedThirdPartyForce.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: true);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(IasfSurvThirdPartyRoundGroupCases))]
    public async Task DistressSignalIasfSurvThirdPartyRoundGroupPrototypesMatchLegacyPartySpawn(
        string thirdPartyId,
        string RoundGroupId,
        string spawnPlanId,
        int playerCount)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(thirdPartyId);
            var partySpawn = prototypes.Index(thirdParty.PartySpawn);
            var force = prototypes.Index<RoundGroupPrototype>(RoundGroupId);
            var spawnPlan = force.Spawn;
            const string planetId = "AUPlanetTrijent";
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var generatedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    playerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var generatedThirdPartyForce = generatedPlan.Forces
                .Single(plannedForce =>
                    plannedForce.ForceKind == ScenarioForceKind.ThirdParty &&
                    plannedForce.SourcePrototypeId.Equals(thirdPartyId, StringComparison.OrdinalIgnoreCase));

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    RoundGroupId,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.ThirdParty));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(thirdParty.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(spawnPlan.AllowsUnderfill, Is.False);
                Assert.That(generatedThirdPartyForce.ForceId, Is.EqualTo($"ThirdParty:{SelectedThreatAssignmentThreat.Id}:{thirdPartyId}"));
                Assert.That(generatedThirdPartyForce.ForceId, Does.Not.Contain("Prototype"));
                Assert.That(generatedThirdPartyForce.ForceKind, Is.EqualTo(prototypeForce!.ForceKind));
                Assert.That(generatedThirdPartyForce.SourcePrototypeId, Is.EqualTo(prototypeForce.SourcePrototypeId));
                Assert.That(generatedThirdPartyForce.WinConditionRuleIds, Is.EquivalentTo(prototypeForce.WinConditionRuleIds));
                Assert.That(generatedThirdPartyForce.Timing, Is.EqualTo(prototypeForce.Timing));
            });

            AssertResolvedSpawnPlansMatch(prototypeForce!.SpawnPlan, generatedThirdPartyForce.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: true);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(WeyuSurvThirdPartyRoundGroupCases))]
    public async Task DistressSignalWeyuSurvThirdPartyRoundGroupPrototypesMatchLegacyPartySpawn(
        string thirdPartyId,
        string RoundGroupId,
        string spawnPlanId,
        int playerCount)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(thirdPartyId);
            var partySpawn = prototypes.Index(thirdParty.PartySpawn);
            var force = prototypes.Index<RoundGroupPrototype>(RoundGroupId);
            var spawnPlan = force.Spawn;
            const string planetId = "AUPlanetTrijent";
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var generatedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    DistressSignalPreset,
                    playerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var generatedThirdPartyForce = generatedPlan.Forces
                .Single(plannedForce =>
                    plannedForce.ForceKind == ScenarioForceKind.ThirdParty &&
                    plannedForce.SourcePrototypeId.Equals(thirdPartyId, StringComparison.OrdinalIgnoreCase));

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    RoundGroupId,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.ThirdParty));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(thirdParty.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(spawnPlan.AllowsUnderfill, Is.False);
                Assert.That(generatedThirdPartyForce.ForceId, Is.EqualTo($"ThirdParty:{SelectedThreatAssignmentThreat.Id}:{thirdPartyId}"));
                Assert.That(generatedThirdPartyForce.ForceId, Does.Not.Contain("Prototype"));
                Assert.That(generatedThirdPartyForce.ForceKind, Is.EqualTo(prototypeForce!.ForceKind));
                Assert.That(generatedThirdPartyForce.SourcePrototypeId, Is.EqualTo(prototypeForce.SourcePrototypeId));
                Assert.That(generatedThirdPartyForce.WinConditionRuleIds, Is.EquivalentTo(prototypeForce.WinConditionRuleIds));
                Assert.That(generatedThirdPartyForce.Timing, Is.EqualTo(prototypeForce.Timing));
            });

            AssertResolvedSpawnPlansMatch(prototypeForce!.SpawnPlan, generatedThirdPartyForce.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: true);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(SimpleThirdPartyRoundGroupCases))]
    public async Task DistressSignalSimpleThirdPartyRoundGroupPrototypesMatchLegacyPartySpawn(
        string thirdPartyId,
        string RoundGroupId,
        string spawnPlanId,
        string planetId,
        int playerCount,
        string expectedMarkerSource)
    {
        await AssertSimpleThirdPartyRoundGroupPrototypesMatchLegacyPartySpawn(
            DistressSignalPreset,
            SelectedThreatAssignmentThreat.Id,
            thirdPartyId,
            RoundGroupId,
            spawnPlanId,
            planetId,
            playerCount,
            expectedMarkerSource);
    }

    private static async Task AssertSimpleThirdPartyRoundGroupPrototypesMatchLegacyPartySpawn(
        string presetId,
        string expectedThreatId,
        string thirdPartyId,
        string RoundGroupId,
        string spawnPlanId,
        string planetId,
        int playerCount,
        string expectedMarkerSource)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var generator = server.System<ScenarioPlanSystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(thirdPartyId);
            var partySpawn = prototypes.Index(thirdParty.PartySpawn);
            var force = prototypes.Index<RoundGroupPrototype>(RoundGroupId);
            var spawnPlan = force.Spawn;
            var legacyBodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
            Assert.That(
                TryGetPlanet(prototypes, componentFactory, planetId, out var planet),
                Is.True,
                $"{planetId} did not resolve to a planet map prototype.");

            var generatedPlan = generator.GeneratePlans(new ScenarioPlanValidationRequest(
                    presetId,
                    playerCount,
                    PlanetId: planetId,
                    MapId: planet.MapId))
                .Single();
            var expectedForceId = $"ThirdParty:{expectedThreatId}:{thirdPartyId}";
            var generatedThirdPartyForce = generatedPlan.Forces
                .Single(plannedForce =>
                    plannedForce.ForceKind == ScenarioForceKind.ThirdParty &&
                    plannedForce.SourcePrototypeId.Equals(thirdPartyId, StringComparison.OrdinalIgnoreCase) &&
                    plannedForce.ForceId.Equals(expectedForceId, StringComparison.OrdinalIgnoreCase));

            Assert.That(
                generator.TryResolveRoundGroupPrototype(
                    RoundGroupId,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic),
                Is.True,
                prototypeDiagnostic);
            Assert.That(prototypeForce, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(force.Kind, Is.EqualTo(RoundGroupKind.ThirdParty));
                Assert.That(force.SourcePrototypeId, Is.EqualTo(thirdParty.ID));
                Assert.That(force.Spawn.HasData, Is.True);
                Assert.That(spawnPlan.AllowsUnderfill, Is.False);
                Assert.That(generatedThirdPartyForce.ForceId, Is.EqualTo(expectedForceId));
                Assert.That(generatedThirdPartyForce.ForceId, Does.Not.Contain("Prototype"));
                Assert.That(generatedThirdPartyForce.ForceKind, Is.EqualTo(prototypeForce!.ForceKind));
                Assert.That(generatedThirdPartyForce.SourcePrototypeId, Is.EqualTo(prototypeForce.SourcePrototypeId));
                Assert.That(generatedThirdPartyForce.WinConditionRuleIds, Is.EquivalentTo(prototypeForce.WinConditionRuleIds));
                Assert.That(generatedThirdPartyForce.Timing, Is.EqualTo(prototypeForce.Timing));
            });

            AssertResolvedSpawnPlansMatch(prototypeForce!.SpawnPlan, generatedThirdPartyForce.SpawnPlan);
            AssertPartySpawnPlanPrototypeMatchesLegacy(spawnPlan, partySpawn, legacyBodyCount, thirdParty: true);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Leader, partySpawn.LeadersToSpawn, partySpawn);
            AssertPrototypeBucketBodiesMatchLegacy(spawnPlan, ThreatMarkerType.Member, partySpawn.GruntsToSpawn, partySpawn);

            if (!string.IsNullOrEmpty(expectedMarkerSource))
            {
                Assert.That(
                    generatedPlan.SpawnMarkers.Any(marker =>
                        marker.Source.ToString().EndsWith(expectedMarkerSource, StringComparison.OrdinalIgnoreCase) &&
                        marker.Tags.Contains(ScenarioMarkerTags.ForceThirdParty)),
                    Is.True,
                    $"Generated Scenario Plan did not include third-party markers from shuttle map '{expectedMarkerSource}'.");
            }
        });

        await pair.CleanReturnAsync();
    }

    private static (string Id, RMCPlanetMapPrototypeComponent Planet) GetFirstSupportedPlanet(
        IPrototypeManager prototypes,
        IComponentFactory componentFactory,
        string presetId)
    {
        var preset = prototypes.Index<GamePresetPrototype>(presetId);
        foreach (var planetId in preset.SupportedPlanets)
        {
            if (!prototypes.TryIndex<EntityPrototype>(planetId, out var entity) ||
                !entity.TryGetComponent<RMCPlanetMapPrototypeComponent>(out var planet, componentFactory))
            {
                continue;
            }

            return (planetId, planet);
        }

        Assert.Fail($"Preset {presetId} has no supported planet with an RMCPlanetMapPrototypeComponent.");
        return default;
    }

    private static bool TryGetPlanet(
        IPrototypeManager prototypes,
        IComponentFactory componentFactory,
        string planetId,
        out RMCPlanetMapPrototypeComponent planet)
    {
        planet = default!;
        if (!prototypes.TryIndex<EntityPrototype>(planetId, out var entity) ||
            !entity.TryGetComponent<RMCPlanetMapPrototypeComponent>(out var planetComp, componentFactory))
        {
            return false;
        }

        planet = planetComp;
        return true;
    }

    private static int GetPresetValidPlayerCount(GamePresetPrototype preset)
    {
        var playerCount = MarkerValidationPlayerCount;
        if (preset.MaxPlayers is { } maxPlayers && maxPlayers > 0)
            playerCount = Math.Min(playerCount, maxPlayers);

        if (preset.MinPlayers is { } minPlayers)
            playerCount = Math.Max(playerCount, minPlayers);

        return playerCount;
    }

    private static string GetFirstSelectedThreatId(
        ScenarioPlanSystem generator,
        string presetId,
        string planetId,
        string mapId)
    {
        var report = generator.ValidateMarkerCoverage(new ScenarioPlanValidationRequest(
            presetId,
            MarkerValidationPlayerCount,
            PlanetId: planetId,
            MapId: mapId));

        return report.Plans
            .SelectMany(plan => plan.DeferredForceChoices)
            .FirstOrDefault(choice => choice.ChoiceId.StartsWith("DeferredThreat:", StringComparison.Ordinal))
            ?.Candidates
            .FirstOrDefault()
            ?.SourcePrototypeId;
    }

    private static List<LegacyThreatCandidate> BuildLegacyThreatCandidates(
        IPrototypeManager prototypes,
        RMCPlanetMapPrototypeComponent planet,
        string presetId,
        int playerCount)
    {
        var candidates = new List<LegacyThreatCandidate>();
        foreach (var threatId in planet.AllowedThreats)
        {
            if (!prototypes.TryIndex(threatId, out ThreatPrototype threat) ||
                !ThreatVoteSelection.IsThreatAllowed(threat, presetId, null, null, playerCount) ||
                !prototypes.TryIndex(threat.RoundStartSpawn, out PartySpawnPrototype spawn))
            {
                continue;
            }

            var bodyCount = ThreatVoteSelection.CalculateBodyCount(spawn, playerCount);
            if (bodyCount.Total <= 0)
                continue;

            candidates.Add(new LegacyThreatCandidate(threat.ID, bodyCount));
        }

        return candidates;
    }

    private static void AssertResolvedMarkersMatchLegacy(
        IEntityManager entities,
        MapId mapId,
        PartySpawnPrototype partySpawn,
        ResolvedThreatSpawnMarkerSet markerSet,
        ThreatMarkerType markerType)
    {
        Assert.That(
            markerSet.TryGetMarkers(markerType.ToString(), out var markers),
            Is.True,
            $"Scenario Plan did not resolve runtime markers for {markerType}.");

        var legacyMarkers = FindScenarioCompatibleThreatMarkers(entities, mapId, partySpawn, markerType);
        Assert.That(
            markers,
            Is.EquivalentTo(legacyMarkers),
            $"Scenario Plan runtime markers for {markerType} did not match Scenario-compatible marker selection.");
    }

    private static void AssertThirdPartyResolvedMarkersMatchLegacy(
        IEntityManager entities,
        MapId mapId,
        PartySpawnPrototype partySpawn,
        ResolvedThirdPartySpawnMarkerSet markerSet,
        ThreatMarkerType markerType)
    {
        Assert.That(
            markerSet.TryGetMarkers(markerType.ToString(), out var markers),
            Is.True,
            $"Scenario Plan did not resolve third-party runtime markers for {markerType}.");

        var legacyMarkers = FindLegacyThirdPartyMarkers(entities, mapId, partySpawn, markerType);
        Assert.That(
            markers,
            Is.EquivalentTo(legacyMarkers),
            $"Scenario Plan third-party runtime markers for {markerType} did not match Scenario-compatible marker selection.");
    }

    private static void AssertThirdPartyParachuteResolvedMarkersMatchLegacy(
        IEntityManager entities,
        MapId mapId,
        PartySpawnPrototype partySpawn,
        ResolvedThirdPartySpawnMarkerSet markerSet,
        ThreatMarkerType markerType)
    {
        Assert.That(
            markerSet.TryGetMarkers(markerType.ToString(), out var markers),
            Is.True,
            $"Scenario Plan did not resolve parachute third-party runtime markers for {markerType}.");

        var legacyMarkers = FindLegacyThirdPartyParachuteMarkers(entities, mapId, partySpawn, markerType);
        Assert.That(
            markers,
            Is.EquivalentTo(legacyMarkers),
            $"Scenario Plan parachute third-party runtime markers for {markerType} did not match legacy marker selection.");
    }

    private static void AssertThirdPartyDropshipResolvedMarkersMatchLegacy(
        IEntityManager entities,
        EntityUid gridUid,
        PartySpawnPrototype partySpawn,
        ResolvedThirdPartySpawnMarkerSet markerSet,
        ThreatMarkerType markerType)
    {
        Assert.That(
            markerSet.TryGetMarkers(markerType.ToString(), out var markers),
            Is.True,
            $"Scenario Plan did not resolve dropship third-party runtime markers for {markerType}.");

        var filteredMarkers = markers!
            .Where(uid =>
                entities.TryGetComponent<TransformComponent>(uid, out var transform) &&
                transform.GridUid.HasValue &&
                transform.GridUid.Value == gridUid)
            .ToList();
        var legacyMarkers = FindLegacyThirdPartyDropshipMarkers(entities, gridUid, partySpawn, markerType);
        Assert.That(
            filteredMarkers,
            Is.EquivalentTo(legacyMarkers),
            $"Scenario Plan dropship third-party runtime markers for {markerType} did not match loaded-grid Scenario-compatible marker selection.");
    }

    private static void AssertClfResolvedMarkersMatchLegacy(
        IEntityManager entities,
        MapId mapId,
        ResolvedClfSpawnMarkerSet markerSet,
        string bucket,
        List<EntityUid> legacyMarkers)
    {
        Assert.That(
            markerSet.TryGetMarkers(bucket, out var markers),
            Is.True,
            $"Scenario Plan did not resolve CLF runtime markers for {bucket}.");

        Assert.That(
            markers,
            Is.EquivalentTo(legacyMarkers),
            $"Scenario Plan CLF runtime markers for {bucket} did not match Scenario-compatible marker selection on map {mapId}.");
    }

    private static (int CommandBodies, int GuerillaBodies) GetLegacyClfBodyCounts(
        IPrototypeManager prototypes,
        IComponentFactory componentFactory)
    {
        var addClfRule = prototypes.Index(AddClfRule);
        Assert.That(
            addClfRule.TryGetComponent<AddJobsRuleComponent>(out var addJobs, componentFactory),
            Is.True);
        Assert.That(addJobs.Jobs, Is.Not.Null);

        var commandBodies = 0;
        var guerillaBodies = 0;
        foreach (var (jobId, count) in addJobs.Jobs!)
        {
            if (IsClfCommandJob(jobId.Id))
                commandBodies += Math.Max(0, count);
            else if (jobId.Id.Contains("CLF", StringComparison.OrdinalIgnoreCase))
                guerillaBodies += Math.Max(0, count);
        }

        return (commandBodies, guerillaBodies);
    }

    private static bool IsClfCommandJob(string jobId)
    {
        return jobId.Equals("AU14JobCLFCellLeader", StringComparison.OrdinalIgnoreCase) ||
               jobId.Equals("AU14JobCLFPhysician", StringComparison.OrdinalIgnoreCase) ||
               jobId.Equals("AU14JobCLFSurgeon", StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertPrototypeForceMatchesThreatAdapter(
        PlannedForce prototypeForce,
        ResolvedThreatForcePlan adaptedForce)
    {
        Assert.Multiple(() =>
        {
            Assert.That(prototypeForce.ForceId, Is.EqualTo(adaptedForce.ForceId));
            Assert.That(prototypeForce.ForceKind, Is.EqualTo(ScenarioForceKind.Hostile));
            Assert.That(prototypeForce.SourcePrototypeId, Is.EqualTo(adaptedForce.ThreatId));
            Assert.That(prototypeForce.WinConditionRuleIds, Is.EquivalentTo(adaptedForce.WinConditionRuleIds));
            Assert.That(prototypeForce.Timing, Is.EqualTo(adaptedForce.Timing));
        });

        AssertResolvedSpawnPlansMatch(prototypeForce.SpawnPlan, adaptedForce.SpawnPlan);
    }

    private static void AssertPlannedForcesMatch(PlannedForce prototypeForce, PlannedForce adaptedForce)
    {
        Assert.Multiple(() =>
        {
            Assert.That(prototypeForce.ForceId, Is.EqualTo(adaptedForce.ForceId));
            Assert.That(prototypeForce.ForceKind, Is.EqualTo(adaptedForce.ForceKind));
            Assert.That(prototypeForce.SourcePrototypeId, Is.EqualTo(adaptedForce.SourcePrototypeId));
            Assert.That(prototypeForce.WinConditionRuleIds, Is.EquivalentTo(adaptedForce.WinConditionRuleIds));
            Assert.That(prototypeForce.Timing, Is.EqualTo(adaptedForce.Timing));
        });

        AssertResolvedSpawnPlansMatch(prototypeForce.SpawnPlan, adaptedForce.SpawnPlan);
    }

    private static void AssertReservationPolicyMatchesSmallestThreatCandidate(DeferredForceChoice choice)
    {
        var smallest = choice.Candidates
            .Select(candidate => new ThreatVoteBodyCount(
                GetBodyBucketCount(candidate.SpawnPlan, ThreatMarkerType.Leader.ToString()),
                GetBodyBucketCount(candidate.SpawnPlan, ThreatMarkerType.Member.ToString())))
            .OrderBy(bodyCount => bodyCount.Total)
            .First();

        Assert.Multiple(() =>
        {
            Assert.That(choice.ReservationPolicy.PolicyId, Is.EqualTo("SmallestCandidateBodyCountAllowsUnderfill"));
            Assert.That(choice.ReservationPolicy.ReservedLeaderBodies, Is.EqualTo(smallest.Leaders));
            Assert.That(choice.ReservationPolicy.ReservedMemberBodies, Is.EqualTo(smallest.Members));
            Assert.That(choice.ReservationPolicy.ReservedBodies, Is.EqualTo(smallest.Total));
        });
    }

    private static int GetBodyBucketCount(ResolvedSpawnPlan spawnPlan, string bucket)
    {
        return spawnPlan.BodyBuckets
            .FirstOrDefault(candidate => candidate.Bucket.Equals(bucket, StringComparison.OrdinalIgnoreCase))
            ?.Count ?? 0;
    }

    private static void AssertSpawnMarkerPrototype(
        IPrototypeManager prototypes,
        ProtoId<SpawnMarkerPrototype> prototypeId,
        SpawnMarkerKind kind,
        params string[] expectedTags)
    {
        var prototype = prototypes.Index(prototypeId);
        Assert.Multiple(() =>
        {
            Assert.That(prototype.Kind, Is.EqualTo(kind));
            Assert.That(prototype.Tags, Is.EquivalentTo(expectedTags));
            Assert.That(prototype.Count, Is.EqualTo(1));
        });
    }

    private static void AssertResolvedSpawnPlansMatch(
        ResolvedSpawnPlan prototypeSpawnPlan,
        ResolvedSpawnPlan adaptedSpawnPlan)
    {
        Assert.Multiple(() =>
        {
            Assert.That(prototypeSpawnPlan.AllowsUnderfill, Is.EqualTo(adaptedSpawnPlan.AllowsUnderfill));
            Assert.That(
                prototypeSpawnPlan.BodyBuckets.Select(bucket => bucket.Bucket),
                Is.EquivalentTo(adaptedSpawnPlan.BodyBuckets.Select(bucket => bucket.Bucket)));
            Assert.That(
                prototypeSpawnPlan.MarkerRequirements.Select(requirement => requirement.Bucket),
                Is.EquivalentTo(adaptedSpawnPlan.MarkerRequirements.Select(requirement => requirement.Bucket)));
        });

        foreach (var adaptedBucket in adaptedSpawnPlan.BodyBuckets)
        {
            var prototypeBucket = prototypeSpawnPlan.BodyBuckets.Single(bucket =>
                bucket.Bucket.Equals(adaptedBucket.Bucket, StringComparison.OrdinalIgnoreCase));
            Assert.That(
                prototypeBucket.Count,
                Is.EqualTo(adaptedBucket.Count),
                $"Resolved Spawn Plan body bucket {adaptedBucket.Bucket} changed.");
            Assert.That(
                prototypeBucket.Bodies,
                Is.EquivalentTo(adaptedBucket.Bodies),
                $"Resolved Spawn Plan body bucket {adaptedBucket.Bucket} bodies changed.");
        }

        foreach (var adaptedRequirement in adaptedSpawnPlan.MarkerRequirements)
        {
            var prototypeRequirement = prototypeSpawnPlan.MarkerRequirements.Single(requirement =>
                requirement.Bucket.Equals(adaptedRequirement.Bucket, StringComparison.OrdinalIgnoreCase));

            Assert.Multiple(() =>
            {
                Assert.That(prototypeRequirement.RequiredBodyCount, Is.EqualTo(adaptedRequirement.RequiredBodyCount));
                Assert.That(prototypeRequirement.RequiredMarkerCount, Is.EqualTo(adaptedRequirement.RequiredMarkerCount));
                Assert.That(prototypeRequirement.RequiredTags, Is.EquivalentTo(adaptedRequirement.RequiredTags));
                Assert.That(prototypeRequirement.WarningOnly, Is.EqualTo(adaptedRequirement.WarningOnly));
            });
        }
    }

    private static void AssertScenarioSpawnDefinitionMatchesAdapter(
        ScenarioSpawnDefinition prototype,
        ResolvedSpawnPlan adapted)
    {
        Assert.Multiple(() =>
        {
            Assert.That(prototype.AllowsUnderfill, Is.EqualTo(adapted.AllowsUnderfill));
            Assert.That(prototype.BodyBuckets.Select(bucket => bucket.Bucket), Is.EquivalentTo(adapted.BodyBuckets.Select(bucket => bucket.Bucket)));
            Assert.That(
                prototype.MarkerRequirements.Select(requirement => requirement.Bucket),
                Is.EquivalentTo(adapted.MarkerRequirements.Select(requirement => requirement.Bucket)));
        });

        foreach (var adaptedBucket in adapted.BodyBuckets)
        {
            var prototypeBucket = prototype.BodyBuckets.Single(bucket =>
                bucket.Bucket.Equals(adaptedBucket.Bucket, StringComparison.OrdinalIgnoreCase));
            Assert.That(
                prototypeBucket.Count,
                Is.EqualTo(adaptedBucket.Count),
                $"Spawn Plan body bucket {adaptedBucket.Bucket} changed.");
        }

        foreach (var adaptedRequirement in adapted.MarkerRequirements)
        {
            var prototypeRequirement = prototype.MarkerRequirements.Single(requirement =>
                requirement.Bucket.Equals(adaptedRequirement.Bucket, StringComparison.OrdinalIgnoreCase));

            Assert.Multiple(() =>
            {
                Assert.That(prototypeRequirement.RequiredBodyCount, Is.EqualTo(adaptedRequirement.RequiredBodyCount));
                Assert.That(prototypeRequirement.RequiredMarkerCount, Is.EqualTo(adaptedRequirement.RequiredMarkerCount));
                Assert.That(prototypeRequirement.RequiredTags, Is.EquivalentTo(adaptedRequirement.RequiredTags));
                Assert.That(prototypeRequirement.WarningOnly, Is.EqualTo(adaptedRequirement.WarningOnly));
            });
        }
    }

    private static void AssertPartySpawnPlanPrototypeMatchesLegacy(
        ScenarioSpawnDefinition prototype,
        PartySpawnPrototype partySpawn,
        ThreatVoteBodyCount bodyCount,
        bool thirdParty)
    {
        var entityBodies = partySpawn.EntitiesToSpawn.Values.Sum();
        Assert.Multiple(() =>
        {
            Assert.That(
                prototype.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Leader.ToString()).Count,
                Is.EqualTo(bodyCount.Leaders));
            Assert.That(
                prototype.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Member.ToString()).Count,
                Is.EqualTo(bodyCount.Members));
            Assert.That(
                prototype.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Entity.ToString()).Count,
                Is.EqualTo(entityBodies));
        });

        AssertPartySpawnMarkerRequirementMatchesLegacy(prototype, partySpawn, ThreatMarkerType.Leader, bodyCount.Leaders, thirdParty);
        AssertPartySpawnMarkerRequirementMatchesLegacy(prototype, partySpawn, ThreatMarkerType.Member, bodyCount.Members, thirdParty);

        if (entityBodies > 0)
            AssertPartySpawnMarkerRequirementMatchesLegacy(prototype, partySpawn, ThreatMarkerType.Entity, entityBodies, thirdParty);
    }

    private static void AssertPrototypeBodyCountMatchesLegacyPartySpawn(
        ScenarioSpawnDefinition prototype,
        PartySpawnPrototype partySpawn,
        int playerCount)
    {
        var bodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
        var entityBodies = partySpawn.EntitiesToSpawn.Values.Sum();

        Assert.Multiple(() =>
        {
            Assert.That(
                prototype.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Leader.ToString()).CalculateBodyCount(playerCount),
                Is.EqualTo(bodyCount.Leaders));
            Assert.That(
                prototype.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Member.ToString()).CalculateBodyCount(playerCount),
                Is.EqualTo(bodyCount.Members));
            Assert.That(
                prototype.BodyBuckets.Single(bucket => bucket.Bucket == ThreatMarkerType.Entity.ToString()).CalculateBodyCount(playerCount),
                Is.EqualTo(entityBodies));
        });
    }

    private static void AssertPrototypeBucketBodiesMatchLegacy(
        ScenarioSpawnDefinition prototype,
        ThreatMarkerType markerType,
        IReadOnlyDictionary<string, int> legacyBodies,
        PartySpawnPrototype partySpawn)
    {
        var bucket = prototype.BodyBuckets.Single(bodyBucket =>
            bodyBucket.Bucket.Equals(markerType.ToString(), StringComparison.OrdinalIgnoreCase));

        Assert.That(
            bucket.Bodies.Count,
            Is.EqualTo(legacyBodies.Count),
            $"Spawn Plan body bucket {markerType} body count changed.");
        foreach (var (bodyId, count) in legacyBodies)
        {
            Assert.That(
                bucket.Bodies.TryGetValue(bodyId, out var prototypeCount),
                Is.True,
                $"Spawn Plan body bucket {markerType} no longer contains {bodyId}.");
            Assert.That(
                prototypeCount,
                Is.EqualTo(count),
                $"Spawn Plan body bucket {markerType} count for {bodyId} changed.");
        }

        var expectedScaling = legacyBodies.Keys
            .Where(partySpawn.Scaling.ContainsKey)
            .ToList();

        Assert.That(
            bucket.Scaling.Keys,
            Is.EquivalentTo(expectedScaling),
            $"Spawn Plan body bucket {markerType} scaling keys changed.");
        foreach (var bodyId in expectedScaling)
        {
            Assert.That(
                bucket.Scaling[bodyId],
                Is.EqualTo(partySpawn.Scaling[bodyId]),
                $"Spawn Plan body bucket {markerType} scaling for {bodyId} changed.");
        }
    }

    private static void AssertPartySpawnMarkerRequirementMatchesLegacy(
        ScenarioSpawnDefinition prototype,
        PartySpawnPrototype partySpawn,
        ThreatMarkerType markerType,
        int requiredBodies,
        bool thirdParty)
    {
        if (requiredBodies <= 0)
            return;

        var requirement = prototype.MarkerRequirements.Single(markerRequirement =>
            markerRequirement.Bucket.Equals(markerType.ToString(), StringComparison.OrdinalIgnoreCase));
        var markerId = partySpawn.Markers.TryGetValue(markerType, out var configuredMarkerId)
            ? configuredMarkerId
            : string.Empty;
        var forceTag = thirdParty
            ? ScenarioMarkerTags.ForceThirdParty
            : ScenarioMarkerTags.ForceHostile;

        Assert.Multiple(() =>
        {
            Assert.That(requirement.RequiredBodyCount, Is.EqualTo(requiredBodies));
            Assert.That(requirement.RequiredMarkerCount, Is.EqualTo(1));
            Assert.That(requirement.RequiredTags, Does.Contain(forceTag));
            Assert.That(requirement.RequiredTags, Does.Contain(ScenarioMarkerTags.Bucket(markerType.ToString())));
            Assert.That(requirement.RequiredTags, Does.Contain(ScenarioMarkerTags.MarkerId(markerId)));
        });
    }

    private static List<EntityUid> FindScenarioCompatibleThreatMarkers(
        IEntityManager entities,
        MapId mapId,
        PartySpawnPrototype partySpawn,
        ThreatMarkerType markerType)
    {
        var markerId = partySpawn.Markers.TryGetValue(markerType, out var id)
            ? id
            : string.Empty;
        var markers = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var transform))
        {
            if (transform.MapID != mapId ||
                marker.Kind != SpawnMarkerKind.ThreatMarker ||
                !marker.Tags.Contains(ScenarioMarkerTags.ForceHostile) ||
                !marker.Tags.Contains(ScenarioMarkerTags.Bucket(markerType.ToString())) ||
                !marker.Tags.Contains(ScenarioMarkerTags.MarkerId(markerId)))
            {
                continue;
            }

            markers.Add(uid);
        }

        return markers;
    }

    private static List<EntityUid> FindLegacyThirdPartyMarkers(
        IEntityManager entities,
        MapId mapId,
        PartySpawnPrototype partySpawn,
        ThreatMarkerType markerType)
    {
        var markerId = partySpawn.Markers.TryGetValue(markerType, out var id)
            ? id
            : string.Empty;
        var markers = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var transform))
        {
            if (transform.MapID != mapId ||
                marker.Kind != SpawnMarkerKind.ThirdPartyMarker ||
                !marker.Tags.Contains(ScenarioMarkerTags.ForceThirdParty) ||
                !marker.Tags.Contains(ScenarioMarkerTags.Bucket(markerType.ToString())) ||
                !marker.Tags.Contains(ScenarioMarkerTags.MarkerId(markerId)))
            {
                continue;
            }

            markers.Add(uid);
        }

        return markers;
    }

    private static List<EntityUid> FindLegacyThirdPartyParachuteMarkers(
        IEntityManager entities,
        MapId mapId,
        PartySpawnPrototype partySpawn,
        ThreatMarkerType markerType)
    {
        var markerId = partySpawn.Markers.TryGetValue(markerType, out var id)
            ? id
            : string.Empty;
        var markers = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<ThreatSpawnMarkerComponent, ParachuteMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out _, out var transform))
        {
            if (transform.MapID != mapId ||
                marker.ThreatMarkerType != markerType ||
                !marker.ThirdParty ||
                !(marker.ID == markerId || marker.ID == string.Empty && markerId == string.Empty))
            {
                continue;
            }

            markers.Add(uid);
        }

        return markers;
    }

    private static List<EntityUid> FindLegacyThirdPartyDropshipMarkers(
        IEntityManager entities,
        EntityUid gridUid,
        PartySpawnPrototype partySpawn,
        ThreatMarkerType markerType)
    {
        var markerId = partySpawn.Markers.TryGetValue(markerType, out var id)
            ? id
            : string.Empty;
        var markers = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var transform))
        {
            if (!transform.GridUid.HasValue ||
                transform.GridUid.Value != gridUid ||
                marker.Kind != SpawnMarkerKind.ThirdPartyMarker ||
                !marker.Tags.Contains(ScenarioMarkerTags.ForceThirdParty) ||
                !marker.Tags.Contains(ScenarioMarkerTags.Bucket(markerType.ToString())) ||
                !marker.Tags.Contains(ScenarioMarkerTags.MarkerId(markerId)))
            {
                continue;
            }

            markers.Add(uid);
        }

        return markers;
    }

    private static List<EntityUid> FindScenarioCompatibleClfSafehouseMarkers(IEntityManager entities, MapId mapId)
    {
        var markers = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var transform))
        {
            if (transform.MapID != mapId ||
                marker.Kind != SpawnMarkerKind.ClfSafehouse ||
                !marker.Tags.Contains(ScenarioMarkerTags.ForceClfSafehouse))
            {
                continue;
            }

            markers.Add(uid);
        }

        return markers;
    }

    private static List<EntityUid> FindLegacyClfCivilianSpawnPoints(IEntityManager entities, MapId mapId)
    {
        var markers = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawnPoint, out var transform))
        {
            if (transform.MapID != mapId ||
                spawnPoint.Job == null ||
                !spawnPoint.Job.Value.Id.Equals("AU14JobCivilianColonist", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            markers.Add(uid);
        }

        return markers;
    }
}
