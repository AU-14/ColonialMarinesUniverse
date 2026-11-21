using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;

namespace Content.IntegrationTests.CMU14.Roles;

[TestFixture]
public sealed class CMUFighterPilotLoadoutTest : GameTest
{
    [TestCase("JobAU14JobGOVFORFighterPilot")]
    [TestCase("JobAU14JobGOVFORDSPilot")]
    public async Task FighterReceivesSelectedEyewear(string savedKey)
    {
        const string sharedRole = "JobAU14JobGOVFORDSPilot";
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var loadout = new RoleLoadout(sharedRole);
            loadout.SelectedLoadouts["EyewearRMCPVE"] = [new Loadout { Prototype = "GogglesM1A1RMC" }];
            var profile = new HumanoidCharacterProfile().WithLoadout(savedKey, loadout);
            var pilot = Server.System<StationSpawningSystem>().SpawnPlayerMob(map.GridCoords,
                "AU14JobGOVFORFighterPilot", profile, station: null);
            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(pilot, "eyes", out var eyes), Is.True);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(eyes!.Value).EntityPrototype?.ID,
                Is.EqualTo("RMCGogglesM1A1Ballistic"));
        });
    }
}
