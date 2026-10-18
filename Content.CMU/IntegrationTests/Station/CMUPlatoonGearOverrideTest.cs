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
///     A platoon's starting gear overrides swap the job's clothing for the platoon's own version.
/// </summary>
[TestFixture]
public sealed class CMUPlatoonGearOverrideTest : GameTest
{
    [TestCase("LACN", "AU14JobGOVFOREngineeringTech", "CMULACNEngineerJumpsuit")]
    [TestCase("LACN", "AU14JobGOVFORNurse", "CMULACNMedicalScrubs")]
    [TestCase("USCM", "AU14JobGOVFOREngineeringTech", "CMJumpsuitMaintTech")]
    public async Task JobSpawnsInPlatoonClothing(string platoon, string job, string jumpsuit)
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

                Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(mob, "jumpsuit", out var worn), Is.True);
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(worn!.Value).EntityPrototype!.ID, Is.EqualTo(jumpsuit));
                SEntMan.DeleteEntity(mob);
            }
            finally
            {
                platoons.SelectedGovforPlatoon = previous;
            }
        });
    }
}
