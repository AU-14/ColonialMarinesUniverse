using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Network;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class ColonyJobOverrideRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task SelectedPlanetRemapsLawEnforcementAndKeepsAccessories()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var round = Server.System<AuRoundSystem>();
            var previous = round.GetSelectedPlanetId();
            try
            {
                Assert.That(round.SetPlanet("CMUPlanetHopesRetreat"), Is.True);
                var preset = Server.System<GameTicker>().CurrentPreset?.ID ?? Server.System<GameTicker>().Preset?.ID;
                var loadout = new RoleLoadout("JobAU14JobLEOBase");
                loadout.SelectedLoadouts["EyewearRMC"] = [new() { Prototype = "AviatorsRMC" }];
                var profile = HumanoidCharacterProfile.DefaultWithSpecies()
                    .WithGamemodeJobPriority(preset, "AU14JobLEOBase", JobPriority.High)
                    .WithLoadout("JobAU14JobLEOBase", loadout);
                var user = new NetUserId(Guid.NewGuid());
                var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile> { [user] = profile };
                SEntMan.EventBus.RaiseEvent(EventSource.Local, new RulePlayerSpawningEvent([], profiles, false));
                var assigned = profiles[user];
                Assert.That(assigned.GetJobPriorityForGamemode(preset, "AU14JobCivilianNSPAConstable"), Is.EqualTo(JobPriority.High));
                Assert.That(assigned.GetJobPriorityForGamemode(preset, "AU14JobCivilianCMBDeputy"), Is.EqualTo(JobPriority.Never));
                var officer = Server.System<StationSpawningSystem>().SpawnPlayerMob(map.GridCoords,
                    "AU14JobCivilianNSPAConstable", assigned, null);
                Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(officer, "eyes", out var glasses), Is.True);
                Assert.That(SComp<MetaDataComponent>(glasses!.Value).EntityPrototype?.ID, Is.EqualTo("RMCGlassesAviators"));
                SEntMan.DeleteEntity(officer);
            }
            finally
            {
                if (previous != null)
                    round.SetPlanet(previous);
            }
        });
    }
}
