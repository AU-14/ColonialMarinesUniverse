using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Storage;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Preferences;

[TestFixture]
public sealed class GlobalLoadoutTest
{
    [Test]
    public async Task PinsBelongOnlyToGlobalLoadoutAndAreFree()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var global = prototypes.Index<RoleLoadoutPrototype>(HumanoidCharacterProfile.GlobalLoadoutId);
            Assert.That(global.Groups, Is.EqualTo(new[] { HumanoidCharacterProfile.GlobalPinsGroup }));
            Assert.That(global.Points, Is.Null);
            foreach (var role in prototypes.EnumeratePrototypes<RoleLoadoutPrototype>())
            {
                if (role.ID != global.ID)
                    Assert.That(role.Groups, Does.Not.Contain(HumanoidCharacterProfile.GlobalPinsGroup), role.ID);
            }

            var pins = prototypes.Index(HumanoidCharacterProfile.GlobalPinsGroup);
            Assert.That(pins.MaxLimit, Is.EqualTo(13));
            foreach (var pin in pins.Loadouts)
                Assert.That(prototypes.Index(pin).Cost, Is.EqualTo(0), pin.ToString());
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GlobalValidationAllowsAllThirteenPinsAndRejectsInvalidSelections()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var session = pair.Client.ResolveDependency<IPlayerManager>().LocalSession!;
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();
            var global = new RoleLoadout(HumanoidCharacterProfile.GlobalLoadoutId);
            var pins = prototypes.Index(HumanoidCharacterProfile.GlobalPinsGroup);
            global.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup] =
                pins.Loadouts.Select(pin => new Loadout { Prototype = pin }).ToList();
            global.EnsureValid(profile, session, IoCManager.Instance!);
            Assert.That(global.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup], Has.Count.EqualTo(13));
            Assert.That(global.Points, Is.Null);

            global.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup] =
                [new() { Prototype = "RMCPinLGBT" }, new() { Prototype = "RMCPinLGBT" },
                    new() { Prototype = "AviatorsRMC" }];
            global.SelectedLoadouts["EyewearRMC"] = [new() { Prototype = "AviatorsRMC" }];
            global.EnsureValid(profile, session, IoCManager.Instance!);
            Assert.That(global.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup], Has.Count.EqualTo(1));
            Assert.That(global.SelectedLoadouts, Does.Not.ContainKey(new ProtoId<LoadoutGroupPrototype>("EyewearRMC")));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LegacyPinsMigrateOnceWithoutChangingOtherProfiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var entities = pair.Server.EntMan;
            var first = new RoleLoadout("JobAU14JobCivilianHeadOfService");
            first.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup] =
                [new() { Prototype = "RMCPinLGBT" }, new() { Prototype = "RMCPinTransgender" }];
            var second = new RoleLoadout("JobAU14JobCivilianShopkeep");
            second.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup] =
                [new() { Prototype = "RMCPinLGBT" }];
            var original = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithLoadout(first.Role.Id, first).WithLoadout(second.Role.Id, second);
            var profile = original.Clone();
            var global = profile.GetLoadoutOrDefault(HumanoidCharacterProfile.GlobalLoadoutId,
                null, profile.Species, entities, prototypes);
            Assert.That(global.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup].Select(pin => pin.Prototype.Id),
                Is.EquivalentTo(new[] { "RMCPinLGBT", "RMCPinTransgender" }));
            Assert.That(profile.Loadouts[first.Role.Id].SelectedLoadouts, Does.Not.ContainKey(HumanoidCharacterProfile.GlobalPinsGroup));
            Assert.That(original.Loadouts[first.Role.Id].SelectedLoadouts, Does.ContainKey(HumanoidCharacterProfile.GlobalPinsGroup));

            global.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup].Clear();
            profile = profile.WithLoadout(HumanoidCharacterProfile.GlobalLoadoutId, global);
            profile = profile.WithLoadout(second.Role.Id, second);
            var reloaded = profile.Clone().GetLoadoutOrDefault(HumanoidCharacterProfile.GlobalLoadoutId,
                null, profile.Species, entities, prototypes);
            Assert.That(reloaded.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup], Is.Empty,
                "Legacy job data must not restore pins after the global selection is cleared.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("AU14JobCivilianHeadOfService")]
    [TestCase("AU14JobCivilianShopkeep")]
    [TestCase("AU14JobColonyWorkingJoe")]
    public async Task GlobalPinsSpawnOnceInEveryJobsBackpack(string job)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var global = new RoleLoadout(HumanoidCharacterProfile.GlobalLoadoutId);
            global.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup] = [new() { Prototype = "RMCPinTransgender" }];
            var profile = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithLoadout(HumanoidCharacterProfile.GlobalLoadoutId, global);
            var mob = entities.System<StationSpawningSystem>().SpawnPlayerMob(map.GridCoords, job, profile, null);
            var inventory = entities.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(mob, "back", out var backpack), Is.True, job);
            var storage = entities.GetComponent<StorageComponent>(backpack!.Value);
            Assert.That(storage.Container.ContainedEntities.Count(item =>
                entities.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == "RMCClothingNeckTransPin"),
                Is.EqualTo(1), "Global pins must be added after job storage and not duplicated.");
            entities.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }
}
