using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Yautja;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Actions;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using ThirdPartySystem = Content.Server.CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMilitaryCasteErtTest
{
    private static readonly ProtoId<ThirdPartyPrototype> DeathsquadParty = "CMUYautjaMilitaryCaste";
    private static readonly ProtoId<ThirdPartyPrototype> LowThreatParty = "CMUYautjaMilitaryCasteLowThreat";
    private static readonly ProtoId<SatiationTypePrototype> Hunger = "Hunger";

    [Test]
    public async Task MilitaryCasteErtIsAdminOnlyWithAnEnforcerAndSevenSoldiers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var (partyId, enforcerId, soldierId) in new[]
                     {
                         (DeathsquadParty, "CMUMobYautjaMilitaryCasteEnforcer", "CMUMobYautjaMilitaryCasteSoldier"),
                         (LowThreatParty, "CMUMobYautjaMilitaryCasteEnforcerLowThreat", "CMUMobYautjaMilitaryCasteSoldierLowThreat"),
                     })
            {
                var party = prototypes.Index(partyId);
                var spawn = prototypes.Index<PartySpawnPrototype>(party.PartySpawn);

                Assert.Multiple(() =>
                {
                    Assert.That(party.MaxPlayers, Is.LessThan(0), $"{partyId} must never be rolled automatically");
                    Assert.That(party.EntryMethod, Is.EqualTo("shuttle"), partyId.Id);
                    Assert.That(party.dropshippath.ToString(), Is.EqualTo("/Maps/CMU14/Dropships/mcaste_shuttle.yml"));
                    Assert.That(spawn.LeadersToSpawn, Is.EquivalentTo(new Dictionary<string, int> { [enforcerId] = 1 }));
                    Assert.That(spawn.GruntsToSpawn, Is.EquivalentTo(new Dictionary<string, int> { [soldierId] = 7 }));
                });

                AssertGhostRole(prototypes, factory, enforcerId, "CMUYautjaMilitaryCasteEnforcer", YautjaMilitaryCaste.Enforcer);
                AssertGhostRole(prototypes, factory, soldierId, "CMUYautjaMilitaryCasteSoldier", YautjaMilitaryCaste.Soldier);
            }

            var roles = server.EntMan.System<SharedRoleSystem>();
            Assert.That(roles.GetRoleRequirements(new ProtoId<JobPrototype>("CMUYautjaMilitaryCasteEnforcer"))?.OfType<RoleTimeRequirement>().ToList(), Is.Not.Null.And.Not.Empty,
                "cmss13 only gives the Enforcer slot to someone past the squad leader timelock");
            Assert.That(roles.GetRoleRequirements(new ProtoId<JobPrototype>("CMUYautjaMilitaryCasteSoldier"))?.OfType<RoleTimeRequirement>() ?? [], Is.Empty);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MilitaryCasteBodiesCarryTheCmss13Kit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var soldier = entMan.SpawnEntity("CMUMobYautjaMilitaryCasteSoldier", map.GridCoords);
            var enforcer = entMan.SpawnEntity("CMUMobYautjaMilitaryCasteEnforcer", map.GridCoords);
            var weakSoldier = entMan.SpawnEntity("CMUMobYautjaMilitaryCasteSoldierLowThreat", map.GridCoords);
            var weakEnforcer = entMan.SpawnEntity("CMUMobYautjaMilitaryCasteEnforcerLowThreat", map.GridCoords);

            Assert.Multiple(() =>
            {
                foreach (var body in new[] { soldier, enforcer, weakSoldier, weakEnforcer })
                {
                    Assert.That(entMan.HasComponent<YautjaComponent>(body), Is.True);
                    Assert.That(entMan.GetComponent<SkillsComponent>(body).Preset?.Id, Is.EqualTo("CMUSkillPresetYautjaSoldier"));
                    AssertSlot(entMan, inventory, body, "ears", "CMUYautjaMilitaryCommunicator");
                    AssertSlot(entMan, inventory, body, "head", "CMUYautjaPoweredHelmet");
                    AssertSlot(entMan, inventory, body, "gloves", "CMUYautjaSoldierBracers");
                    AssertSlot(entMan, inventory, body, "shoes", "CMUYautjaPoweredGreaves");
                    AssertSlot(entMan, inventory, body, "pocket1", "CMUYautjaMcasteHerbContainerFilled");
                    AssertSlot(entMan, inventory, body, "pocket2", "CMUYautjaMedicompFull");

                    var belt = BeltContents(entMan, inventory, body);
                    Assert.That(belt, Does.Contain("CMUMREFoodCLFWrappedMeatPie"));
                    Assert.That(belt, Does.Contain("CMUYautjaStrongXenoRestraints"));
                    Assert.That(belt, Does.Contain("RMCHandcuffs"));
                    Assert.That(belt.Count(id => id == "CMUYautjaWristBladesAttachment"), Is.EqualTo(2));

                    Assert.That(inventory.TryGetSlotEntity(body, "shoes", out var greaves), Is.True);
                    var dagger = entMan.System<ItemSlotsSystem>().GetItemOrNull(greaves!.Value, "item");
                    Assert.That(dagger, Is.Not.Null, "powered greaves are cmss13 knife greaves");
                }

                AssertSlot(entMan, inventory, soldier, "back", "CMUYautjaHeavyGelDefoliatorDeathsquad");
                AssertSlot(entMan, inventory, soldier, "outerClothing", "CMUYautjaPoweredArmor");
                AssertSlot(entMan, inventory, soldier, "suitstorage", "CMUYautjaPlasmaCarbine");
                Assert.That(BeltContents(entMan, inventory, soldier).Count(id => id == "CMUYautjaDefoliatorFuelTankDeathsquad"), Is.EqualTo(3));

                AssertSlot(entMan, inventory, enforcer, "back", "CMUYautjaCannonPack");
                AssertSlot(entMan, inventory, enforcer, "outerClothing", "CMUYautjaPoweredArmorEnforcer");
                AssertSlot(entMan, inventory, enforcer, "suitstorage", "CMUYautjaPlasmaCarbine");
                Assert.That(BeltContents(entMan, inventory, enforcer).Any(id => id.StartsWith("CMUYautjaDefoliatorFuelTank")), Is.False);

                AssertSlot(entMan, inventory, weakSoldier, "back", "CMUYautjaHeavyGelDefoliator");
                AssertSlot(entMan, inventory, weakSoldier, "suitstorage", "CMUYautjaPlasmaRifle");
                Assert.That(BeltContents(entMan, inventory, weakSoldier).Count(id => id == "CMUYautjaDefoliatorFuelTank"), Is.EqualTo(3));

                AssertSlot(entMan, inventory, weakEnforcer, "back", "CMUYautjaCannonPack");
                AssertSlot(entMan, inventory, weakEnforcer, "outerClothing", "CMUYautjaPoweredArmor");
                AssertSlot(entMan, inventory, weakEnforcer, "suitstorage", "CMUYautjaPlasmaRifle");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SoldierBracerKeepsMenuSelfDestructAndTranslatorButNoCloak()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var soldier = entMan.SpawnEntity("CMUMobYautjaMilitaryCasteSoldier", map.GridCoords);
            var actions = entMan.System<SharedActionsSystem>().GetActions(soldier)
                .Select(action => entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(actions, Does.Contain("CMUActionYautjaOpenBracerMenu"));
                Assert.That(actions, Does.Contain("CMUActionYautjaSelfDestruct"));
                Assert.That(actions, Does.Contain("CMUActionYautjaTranslator"));
                Assert.That(actions, Does.Not.Contain("CMUActionYautjaToggleCloak"));
                Assert.That(actions, Does.Not.Contain("CMUActionYautjaCallDisc"));
                Assert.That(actions, Does.Not.Contain("CMUActionYautjaToggleCaster"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TakingAMilitaryCasteBodyGivesACodenameAndAFullStomach()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid enforcer = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var session = server.PlayerMan.Sessions.Single();
            var minds = entMan.System<SharedMindSystem>();
            enforcer = entMan.SpawnEntity("CMUMobYautjaMilitaryCasteEnforcer", map.GridCoords);

            Assert.That(entMan.GetComponent<YautjaComponent>(enforcer).EntryMessageSent, Is.True,
                "the hunter honor code entry lines are not for the military caste");

            var mind = minds.CreateMind(session.UserId, "Military Caste Test");
            minds.TransferTo(mind, enforcer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var caste = entMan.GetComponent<YautjaMilitaryCasteComponent>(enforcer);
            var name = entMan.GetComponent<MetaDataComponent>(enforcer).EntityName;
            var satiation = entMan.GetComponent<SatiationComponent>(enforcer);
            var satiationSystem = entMan.System<SatiationSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(caste.SetupComplete, Is.True);
                Assert.That(name, Does.StartWith("Enforcer ").Or.StartWith("High Enforcer "));
                Assert.That(satiationSystem.GetValueOrNull((enforcer, satiation), Hunger),
                    Is.EqualTo((float?) satiationSystem.GetMaximumValue((enforcer, satiation), Hunger)).Within(1f));
            });

            entMan.DeleteEntity(enforcer);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CodenamesFollowCmss13Prefixes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var system = server.EntMan.System<YautjaMilitaryCasteSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(system.GetCodename(YautjaMilitaryCaste.Soldier, false), Does.Not.StartWith("Elite ").And.Not.StartWith("Enforcer "));
                Assert.That(system.GetCodename(YautjaMilitaryCaste.Soldier, true), Does.StartWith("Elite "));
                Assert.That(system.GetCodename(YautjaMilitaryCaste.Enforcer, false), Does.StartWith("Enforcer "));
                Assert.That(system.GetCodename(YautjaMilitaryCaste.Enforcer, true), Does.StartWith("High Enforcer "));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdminCallSpawnsTheSquadAboardTheMilitaryCasteShuttle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var party = prototypes.Index(DeathsquadParty);
            var spawn = prototypes.Index<PartySpawnPrototype>(party.PartySpawn);

            Assert.That(entMan.System<ThirdPartySystem>().SpawnThirdPartyImmediately(party, spawn, true), Is.True);

            EntityUid? shuttle = null;
            var consoles = entMan.EntityQueryEnumerator<YautjaShuttleConsoleComponent, TransformComponent>();
            while (consoles.MoveNext(out var uid, out _, out var xform))
            {
                if (entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "CMUYautjaMilitaryCasteShuttleConsole")
                    shuttle = xform.GridUid;
            }

            Assert.That(shuttle, Is.Not.Null, "the squad should arrive on its own copy of the hunter shuttle");

            var enforcers = 0;
            var soldiers = 0;
            var bodies = entMan.EntityQueryEnumerator<YautjaMilitaryCasteComponent, TransformComponent>();
            while (bodies.MoveNext(out var uid, out var caste, out var xform))
            {
                Assert.That(xform.GridUid, Is.EqualTo(shuttle));
                Assert.That(entMan.HasComponent<GhostRoleComponent>(uid), Is.True);
                if (caste.Caste == YautjaMilitaryCaste.Enforcer)
                    enforcers++;
                else
                    soldiers++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(enforcers, Is.EqualTo(1));
                Assert.That(soldiers, Is.EqualTo(7));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertGhostRole(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string mob,
        string job,
        YautjaMilitaryCaste caste)
    {
        var prototype = prototypes.Index<EntityPrototype>(mob);
        Assert.Multiple(() =>
        {
            Assert.That(prototype.TryComp<GhostRoleComponent>(out var ghostRole, factory), Is.True, mob);
            Assert.That(ghostRole?.JobProto?.Id, Is.EqualTo(job), mob);
            Assert.That(prototype.TryComp<YautjaMilitaryCasteComponent>(out var casteComp, factory), Is.True, mob);
            Assert.That(casteComp?.Caste, Is.EqualTo(caste), mob);
        });
    }

    private static void AssertSlot(IEntityManager entMan, InventorySystem inventory, EntityUid wearer, string slot, string expected)
    {
        Assert.That(inventory.TryGetSlotEntity(wearer, slot, out var item), Is.True, $"{slot} empty");
        Assert.That(entMan.GetComponent<MetaDataComponent>(item!.Value).EntityPrototype?.ID, Is.EqualTo(expected), slot);
    }

    private static List<string> BeltContents(IEntityManager entMan, InventorySystem inventory, EntityUid wearer)
    {
        if (!inventory.TryGetSlotEntity(wearer, "belt", out var belt) ||
            !entMan.TryGetComponent(belt, out StorageComponent? storage))
        {
            return new List<string>();
        }

        return storage.Container.ContainedEntities
            .Select(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID ?? string.Empty)
            .ToList();
    }
}
