using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.util;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Station;

/// <summary>
///     A platoon's starting gear overrides swap the job's clothing and loadout satchel for the platoon's own versions.
/// </summary>
[TestFixture]
public sealed class CMUPlatoonGearOverrideTest : GameTest
{
    [TestCase("LACN", "AU14JobGOVFOREngineeringTech", "jumpsuit", "CMULACNEngineerJumpsuit")]
    [TestCase("LACN", "AU14JobGOVFOREngineeringTech", "head", "CMULACNHelmetEngineer")]
    [TestCase("LACN", "AU14JobGOVFOREngineeringTech", "outerClothing", "CMULACNLogisticsVest")]
    [TestCase("LACN", "AU14JobGOVFOROfficerLogistics", "head", "CMULACNHelmetRequisitions")]
    [TestCase("LACN", "AU14JobGOVFOROfficerLogistics", "jumpsuit", "CMULACNRequisitionsFieldUniform")]
    [TestCase("LACN", "AU14JobGOVFORNurse", "jumpsuit", "CMULACNMedicalScrubs")]
    [TestCase("LACN", "AU14JobGOVFORMilitaryDoctor", "jumpsuit", "CMULACNSurgicalScrubs")]
    [TestCase("LACN", "AU14JobGOVFORSquadRifleman", "back", "CMULACNSatchel")]
    [TestCase("CMBCIU", "AU14JobGOVFORSquadRifleman", "back", "RMCSatchelTSEPA")]
    [TestCase("ProdigySF", "AU14JobGOVFORSquadRifleman", "back", "RMCSatchelLightpackBlack")]
    [TestCase("WEYU", "AU14JobGOVFORSquadRifleman", "back", "RMCSatchelLightpackWeYa")]
    [TestCase("UPP", "AU14JobGOVFORSquadRifleman", "back", "RuMCUPPNavalInfantrySatchel")]
    [TestCase("USCM", "AU14JobGOVFOREngineeringTech", "jumpsuit", "CMJumpsuitMaintTech")]
    [TestCase("USCM", "AU14JobGOVFORSquadRifleman", "back", "CMSatchelMarine")]
    public async Task JobSpawnsInPlatoonGear(string platoon, string job, string slot, string expected)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var platoons = Server.System<PlatoonSpawnRuleSystem>();
            var previous = platoons.SelectedGovforPlatoon;
            platoons.SelectedGovforPlatoon = SProtoMan.Index<PlatoonPrototype>(platoon);
            try
            {
                var mob = Server.System<StationSpawningSystem>()
                    .SpawnPlayerMob(map.GridCoords, new ProtoId<JobPrototype>(job), new HumanoidCharacterProfile(), station: null);

                Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(mob, slot, out var worn), Is.True, $"nothing in {slot}");
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(worn!.Value).EntityPrototype!.ID, Is.EqualTo(expected));
                SEntMan.DeleteEntity(mob);
            }
            finally
            {
                platoons.SelectedGovforPlatoon = previous;
            }
        });
    }
}
