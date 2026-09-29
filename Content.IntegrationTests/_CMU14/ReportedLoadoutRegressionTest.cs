using System.Linq;
using Content.Client.Lobby;
using Content.Server.Station.Systems;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class ReportedLoadoutRegressionTest
{
    [TestCase("AU14JobCivilianHeadOfEngineering", "EngiJacketAU14", "HazardVestAU14", "outerClothing", "RMCHazardVest")]
    [TestCase("AU14JobColonyWorkingJoe", "EyewearRMC", "AviatorsRMC", "eyes", "RMCGlassesAviators")]
    public async Task CharacterPreviewShowsSelectedEquipment(string jobId, string group, string selection, string slot, string expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var (key, definition) = LoadoutSystem.GetJobLoadoutInfo(jobId, prototypes);
            var loadout = new RoleLoadout(definition!.ID);
            loadout.SelectedLoadouts[group] = [new() { Prototype = selection }];
            var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithLoadout(key, loadout);
            var controller = pair.Client.ResolveDependency<IUserInterfaceManager>().GetUIController<LobbyUIController>();
            var preview = controller.LoadProfileEntity(profile, prototypes.Index<JobPrototype>(jobId), true);
            var inventory = entities.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(preview, slot, out var equipped), Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(equipped!.Value).EntityPrototype?.ID, Is.EqualTo(expected));
            if (jobId == "AU14JobCivilianHeadOfEngineering")
                Assert.That(inventory.TryGetSlotEntity(preview, "jumpsuit", out _), Is.True, "Selecting a vest must not strip the uniform in character setup.");
            entities.DeleteEntity(preview);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("AU14JobCivilianHeadOfService")]
    [TestCase("AU14JobCivilianColonySecurityLeader")]
    [TestCase("AU14JobCivilianColonySecurityOfficer")]
    [TestCase("AU14JobCivilianNSPAConstable")]
    [TestCase("AU14JobCivilianEthicsAndWellnessAdvisor")]
    [TestCase("AU14JobMobBoss")]
    [TestCase("AU14JobMobGoon")]
    [TestCase("AU14JobCivilianPrisoner")]
    [TestCase("AU14JobColonyWorkingJoe")]
    [TestCase("AU14JobColonyWorkingJoeHazmat")]
    [TestCase("AU14JobCivilianOrbitalLawyer")]
    [TestCase("AU14JobCivilianShopkeep")]
    public async Task ReportedJobsResolveSelectableLoadouts(string job)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var (key, loadout) = LoadoutSystem.GetJobLoadoutInfo(job, prototypes);
            Assert.That(key, Is.EqualTo("Job" + job));
            Assert.That(loadout, Is.Not.Null, job);
            Assert.That(loadout.Groups, Does.Contain(new ProtoId<LoadoutGroupPrototype>("PaperworkRMC")));
            foreach (var group in loadout.Groups)
                Assert.That(prototypes.HasIndex(group), Is.True, group.ToString());
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WorkingJoeReceivesSelectedEquipment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            const string key = "JobAU14JobColonyWorkingJoe";
            var loadout = new RoleLoadout("JobAU14JobWorkingJoeBase");
            loadout.SelectedLoadouts["EyewearRMC"] = [new() { Prototype = "AviatorsRMC" }];
            var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithLoadout(key, loadout);
            var joe = entities.System<StationSpawningSystem>().SpawnPlayerMob(map.GridCoords,
                "AU14JobColonyWorkingJoe", profile, null);
            var inventory = entities.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(joe, "eyes", out var glasses), Is.True,
                "Custom job bodies must receive selected loadout equipment.");
            Assert.That(entities.GetComponent<MetaDataComponent>(glasses!.Value).EntityPrototype?.ID,
                Is.EqualTo("RMCGlassesAviators"));
            entities.DeleteEntity(joe);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HazardVestUsesOuterClothingAndPreservesUniform()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var spawning = entities.System<StationSpawningSystem>();
            var inventory = entities.System<InventorySystem>();
            var wearer = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var uniform = entities.SpawnEntity("CMJumpsuitColonist", map.GridCoords);
            Assert.That(inventory.TryEquip(wearer, uniform, "jumpsuit", silent: true, force: true), Is.True);
            var vest = prototypes.Index<LoadoutPrototype>("HazardVestAU14");
            spawning.EquipStartingGear(wearer, vest);
            Assert.That(inventory.TryGetSlotEntity(wearer, "jumpsuit", out var equippedUniform), Is.True);
            Assert.That(equippedUniform, Is.EqualTo(uniform));
            Assert.That(inventory.TryGetSlotEntity(wearer, "outerClothing", out var equippedVest), Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(equippedVest!.Value).EntityPrototype?.ID,
                Is.EqualTo("RMCHazardVest"));
            entities.DeleteEntity(wearer);
        });
        await pair.CleanReturnAsync();
    }
}
