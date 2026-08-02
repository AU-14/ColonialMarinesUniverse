using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._CMU14.Yautja;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaMilitaryCasteRoleTest
{
    [Test]
    public async Task MilitaryCasteWornGearUsesOriginalCmss13OnMobRsi()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Client.WaitAssertion(() =>
        {
            var cache = pair.Client.ResolveDependency<IResourceCache>();
            var path = new ResPath("/Textures/_CMU14/Yautja/mcaste_gear_worn.rsi");

            Assert.That(cache.TryGetResource<RSIResource>(path, out var resource), Is.True);
            Assert.That(resource!.RSI.Size, Is.EqualTo(new Vector2i(32, 32)));

            var oneDirectionStates = new[] { "ARMOR", "SHOES", "HELMET", "BACK", "SHOULDER" };
            var fourDirectionStates = new[]
            {
                "fullarmor_soldier",
                "fullarmor_soldier_lead",
                "y-boots_powered",
                "helmet_powered",
                "cannonpack",
                "plasma_cannons",
            };

            foreach (var stateName in oneDirectionStates)
            {
                Assert.That(resource.RSI.TryGetState(stateName, out var state), Is.True, stateName);
                Assert.That(state!.RsiDirections, Is.EqualTo(RsiDirectionType.Dir1), stateName);
            }

            foreach (var stateName in fourDirectionStates)
            {
                Assert.That(resource.RSI.TryGetState(stateName, out var state), Is.True, stateName);
                Assert.That(state!.RsiDirections, Is.EqualTo(RsiDirectionType.Dir4), stateName);
            }
        });

        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var factory = pair.Server.EntMan.ComponentFactory;
            var wornPath = "_CMU14/Yautja/mcaste_gear_worn.rsi";

            var expectedStates = new Dictionary<string, string>
            {
                ["CMUYautjaPoweredArmor"] = "fullarmor_soldier",
                ["CMUYautjaPoweredGreaves"] = "y-boots_powered",
                ["CMUYautjaPoweredHelmet"] = "helmet_powered",
                ["CMUYautjaCannonPack"] = "cannonpack",
            };

            foreach (var (id, expectedState) in expectedStates)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<ClothingComponent>(out var clothing, factory), Is.True, id);
                Assert.That(clothing!.RsiPath, Is.EqualTo(wornPath), id);
                Assert.That(clothing.EquippedState, Is.EqualTo(expectedState), id);
            }

            var enforcer = prototypes.Index<EntityPrototype>("CMUYautjaPoweredArmorEnforcer");
            Assert.That(enforcer.TryGetComponent<ClothingComponent>(out var enforcerClothing, factory), Is.True);
            Assert.That(enforcerClothing!.EquippedState, Is.EqualTo("fullarmor_soldier_lead"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MilitaryCasteHudIconsAreVisibleInGame()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        EntityUid soldier = default;
        EntityUid enforcer = default;
        NetEntity soldierNet = default;
        NetEntity enforcerNet = default;

        await pair.Server.WaitPost(() =>
        {
            var entMan = pair.Server.EntMan;
            var session = pair.Server.PlayerMan.Sessions.Single();
            soldier = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            enforcer = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            entMan.EnsureComponent<YautjaComponent>(soldier);
            entMan.EnsureComponent<YautjaComponent>(enforcer);
            entMan.EnsureComponent<YautjaMilitaryCasteComponent>(soldier);
            var enforcerCaste = entMan.EnsureComponent<YautjaMilitaryCasteComponent>(enforcer);
            enforcerCaste.Caste = YautjaMilitaryCaste.Enforcer;
            enforcerCaste.WhitelistIcon = true;
            entMan.Dirty(enforcer, enforcerCaste);
            soldierNet = entMan.GetNetEntity(soldier);
            enforcerNet = entMan.GetNetEntity(enforcer);
            pair.Server.PlayerMan.SetAttachedEntity(session, soldier);
        });

        await pair.RunTicksSync(5);

        try
        {
            await pair.Client.WaitAssertion(() =>
            {
                var entMan = pair.Client.EntMan;
                var player = pair.Client.ResolveDependency<IPlayerManager>();
                Assert.That(entMan.TryGetEntity(soldierNet, out var clientSoldier), Is.True);
                Assert.That(entMan.TryGetEntity(enforcerNet, out var clientEnforcer), Is.True);
                Assert.That(player.LocalEntity, Is.EqualTo(clientSoldier));

                var soldierIcons = new List<StatusIconData>();
                var soldierEvent = new GetStatusIconsEvent(soldierIcons);
                entMan.EventBus.RaiseLocalEvent(clientSoldier!.Value, ref soldierEvent);
                var soldierStates = soldierIcons
                    .Select(icon => icon.Icon)
                    .OfType<SpriteSpecifier.Rsi>()
                    .Select(icon => icon.RsiState)
                    .ToList();

                Assert.That(soldierStates, Does.Contain("soldierhud"));
                Assert.That(soldierStates, Does.Not.Contain("predhud"),
                    "Military Yautja use the caste HUD icon instead of the ordinary rank icon.");

                entMan.EnsureComponent<YautjaHudViewerComponent>(clientSoldier.Value);
                var enforcerIcons = new List<StatusIconData>();
                var enforcerEvent = new GetStatusIconsEvent(enforcerIcons);
                entMan.EventBus.RaiseLocalEvent(clientEnforcer!.Value, ref enforcerEvent);
                var enforcerStates = enforcerIcons
                    .Select(icon => icon.Icon)
                    .OfType<SpriteSpecifier.Rsi>()
                    .Select(icon => icon.RsiState)
                    .ToList();

                Assert.That(enforcerStates, Does.Contain("enforcerhud_wl"));
                Assert.That(enforcerStates, Does.Not.Contain("enforcerhud"));
                Assert.That(enforcerStates, Does.Not.Contain("predhud"));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                var entMan = pair.Server.EntMan;
                foreach (var uid in new[] { soldier, enforcer })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MilitaryCasteHudPrototypesUseCmss13States()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var soldier = prototypes.Index<HealthIconPrototype>("CMUYautjaMilitarySoldierIcon");
            var enforcer = prototypes.Index<HealthIconPrototype>("CMUYautjaMilitaryEnforcerIcon");
            var soldierMob = prototypes.Index<EntityPrototype>("CMUMobYautjaMilitaryCasteSoldier");
            var enforcerMob = prototypes.Index<EntityPrototype>("CMUMobYautjaMilitaryCasteEnforcer");

            Assert.Multiple(() =>
            {
                Assert.That(soldier.Icon, Is.TypeOf<SpriteSpecifier.Rsi>());
                Assert.That(((SpriteSpecifier.Rsi) soldier.Icon).RsiState, Is.EqualTo("soldierhud"));
                Assert.That(enforcer.Icon, Is.TypeOf<SpriteSpecifier.Rsi>());
                Assert.That(((SpriteSpecifier.Rsi) enforcer.Icon).RsiState, Is.EqualTo("enforcerhud"));
                Assert.That(soldierMob.Components.ContainsKey("YautjaMilitaryCaste"), Is.True);
                Assert.That(enforcerMob.Components.ContainsKey("YautjaMilitaryCaste"), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MilitaryCasteJobsAreHiddenEventOnlyRoles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var soldier = prototypes.Index<JobPrototype>("CMUYautjaMilitaryCasteSoldier");
            var enforcer = prototypes.Index<JobPrototype>("CMUYautjaMilitaryCasteEnforcer");

            Assert.Multiple(() =>
            {
                AssertMilitaryCasteJob(soldier,
                    "CMUMobYautjaMilitaryCasteSoldier",
                    "CMUYautjaMilitaryCasteSoldierGear");
                AssertMilitaryCasteJob(enforcer,
                    "CMUMobYautjaMilitaryCasteEnforcer",
                    "CMUYautjaMilitaryCasteEnforcerGear");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MilitaryCasteJobsSpawnWithFixedRoleGear()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithName("Military Caste Test")
                .WithYautjaProfile(YautjaCharacterProfile.Default.WithName("Military Caste Test"));

            var soldier = stationSpawning.SpawnPlayerMob(
                map.GridCoords,
                "CMUYautjaMilitaryCasteSoldier",
                profile,
                station: null);
            var enforcer = stationSpawning.SpawnPlayerMob(
                map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)),
                "CMUYautjaMilitaryCasteEnforcer",
                profile,
                station: null);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<YautjaComponent>(soldier), Is.True);
                AssertEquippedPrototype(entMan, inventory, soldier, "ears", "CMUYautjaMilitaryCommunicator");
                AssertEquippedPrototype(entMan, inventory, soldier, "head", "CMUYautjaPoweredHelmet");
                AssertEquippedPrototype(entMan, inventory, soldier, "gloves", "CMUYautjaSoldierBracers");
                AssertEquippedPrototype(entMan, inventory, soldier, "outerClothing", "CMUYautjaPoweredArmor");
                AssertEquippedPrototype(entMan, inventory, soldier, "shoes", "CMUYautjaPoweredGreaves");

                Assert.That(entMan.HasComponent<YautjaComponent>(enforcer), Is.True);
                AssertEquippedPrototype(entMan, inventory, enforcer, "ears", "CMUYautjaMilitaryCommunicator");
                AssertEquippedPrototype(entMan, inventory, enforcer, "head", "CMUYautjaPoweredHelmet");
                AssertEquippedPrototype(entMan, inventory, enforcer, "gloves", "CMUYautjaSoldierBracers");
                AssertEquippedPrototype(entMan, inventory, enforcer, "outerClothing", "CMUYautjaPoweredArmorEnforcer");
                AssertEquippedPrototype(entMan, inventory, enforcer, "shoes", "CMUYautjaPoweredGreaves");
                AssertEquippedPrototype(entMan, inventory, enforcer, "back", "CMUYautjaCannonPack");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertMilitaryCasteJob(
        JobPrototype job,
        string expectedEntity,
        string expectedStartingGear)
    {
        Assert.That(job.Hidden, Is.True);
        Assert.That(job.Whitelisted, Is.False);
        Assert.That(job.CanBeAntag, Is.False);
        Assert.That(job.JoinNotifyCrew, Is.False);
        Assert.That(job.UsePlayerProfile, Is.False);
        Assert.That(job.JobEntity, Is.EqualTo(expectedEntity));
        Assert.That(job.JobPreviewEntity?.ToString(), Is.EqualTo(expectedEntity));
        Assert.That(job.StartingGear?.ToString(), Is.EqualTo(expectedStartingGear));
    }

    private static void AssertEquippedPrototype(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        string expectedPrototype)
    {
        Assert.That(inventory.TryGetSlotEntity(wearer, slot, out var equipped), Is.True, slot);
        var meta = entMan.GetComponent<MetaDataComponent>(equipped.Value);
        Assert.That(meta.EntityPrototype?.ID, Is.EqualTo(expectedPrototype), slot);
    }
}
