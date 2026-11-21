using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Threats;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Cuffs.Components;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Content.Shared.Station;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Roles;

/// <summary>
///     The kits of the teams the corporate affairs console can call: that they go on whole, and that
///     each team carries what its job depends on. Which exact items they carry is a design choice and
///     isn't pinned here.
/// </summary>
[TestFixture]
public sealed class CMUCorporateTeamKitTest
{
    /// <summary>
    ///     Every job in every party on the corporate console, and its starting gear.
    /// </summary>
    private static List<(string Job, ProtoId<StartingGearPrototype> Gear)> CorporateTeamGear(IPrototypeManager protos, IComponentFactory factory)
    {
        var gear = new List<(string, ProtoId<StartingGearPrototype>)>();
        foreach (var console in protos.EnumeratePrototypes<EntityPrototype>())
        {
            if (console.Abstract || !console.TryGetComponent<CorporateConsoleComponent>(out var comp, factory))
                continue;

            foreach (var partyId in comp.CallableParties.Keys)
            {
                var spawn = protos.Index(protos.Index<ThirdPartyPrototype>(partyId).PartySpawn);
                foreach (var member in spawn.LeadersToSpawn.Keys.Concat(spawn.GruntsToSpawn.Keys))
                {
                    if (!protos.Index<EntityPrototype>(member).TryGetComponent<GhostRoleComponent>(out var role, factory) ||
                        role.JobProto is not { } jobId ||
                        protos.Index(jobId).StartingGear is not { } gearId)
                    {
                        continue;
                    }

                    gear.Add((jobId, gearId));
                }
            }
        }

        return gear.Distinct().ToList();
    }

    private static EntityUid Equip(Pair.TestPair pair, EntityCoordinates coords, string gear)
    {
        var mob = pair.Server.EntMan.SpawnEntity("CMMobHuman", coords);
        pair.Server.System<SharedStationSpawningSystem>().EquipStartingGear(mob, gear);
        return mob;
    }

    private static List<EntityUid> Stored(IEntityManager entities, SharedContainerSystem containers, EntityUid uid)
    {
        return containers.TryGetContainer(uid, StorageComponent.ContainerId, out var container)
            ? container.ContainedEntities.ToList()
            : new List<EntityUid>();
    }

    /// <summary>
    ///     Nothing in a team's kit is left on the floor: every slot it names is worn and every stored
    ///     item fits where it's put.
    /// </summary>
    [Test]
    public async Task CorporateTeamKitsGoOnWhole()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var inventory = server.System<InventorySystem>();
            var containers = server.System<SharedContainerSystem>();
            var lookup = server.System<EntityLookupSystem>();
            var teams = CorporateTeamGear(server.ProtoMan, server.ResolveDependency<IComponentFactory>());
            Assert.That(teams, Is.Not.Empty, "Found no corporate teams to check.");

            foreach (var (job, gearId) in teams)
            {
                var gear = server.ProtoMan.Index(gearId);
                var mob = Equip(pair, map.GridCoords, gearId);

                foreach (var slot in gear.Equipment.Keys)
                {
                    Assert.That(inventory.TryGetSlotEntity(mob, slot, out _), Is.True, $"{job} ({gearId}) doesn't wear anything in {slot}.");
                }

                var loose = lookup.GetEntitiesInRange(map.GridCoords, 3f)
                    .Where(uid => uid != mob && entities.HasComponent<Content.Shared.Item.ItemComponent>(uid) && !containers.IsEntityInContainer(uid))
                    .Select(uid => entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID)
                    .ToList();
                Assert.That(loose, Is.Empty, $"{job} ({gearId}) dropped items that didn't fit.");

                foreach (var uid in lookup.GetEntitiesInRange(map.GridCoords, 3f).Where(uid => uid != map.Grid.Owner && uid != map.MapUid).ToList())
                {
                    if (!containers.IsEntityInContainer(uid))
                        entities.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The riot team works in its own tear gas, so every member's face is behind a fitted filter.
    /// </summary>
    [TestCase("CMUGearWYRiotLeader")]
    [TestCase("CMUGearWYRiotSpecialist")]
    [TestCase("CMUGearWYRiotGrenadier")]
    public async Task RiotTeamIsFilteredAgainstItsOwnGas(string gear)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var inventory = server.System<InventorySystem>();
            var slots = server.System<ItemSlotsSystem>();
            var mob = Equip(pair, map.GridCoords, gear);

            var filtered = new[] { "mask", "head" }.Any(slot =>
                inventory.TryGetSlotEntity(mob, slot, out var worn) && slots.GetItemOrNull(worn.Value, "filter") != null);
            Assert.That(filtered, Is.True, $"{gear} has no filter over the face.");

            server.EntMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The riot grenadier's launcher takes the team's tear gas and flashbangs.
    /// </summary>
    [Test]
    public async Task RiotGrenadierLauncherTakesTearGasAndFlashbangs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var inventory = server.System<InventorySystem>();
            var whitelist = server.System<EntityWhitelistSystem>();
            var mob = Equip(pair, map.GridCoords, "CMUGearWYRiotGrenadier");

            Assert.That(inventory.TryGetSlotEntity(mob, "suitstorage", out var launcher), Is.True);
            var ammo = entities.GetComponent<BallisticAmmoProviderComponent>(launcher!.Value);
            foreach (var round in new[] { "CMU14TearGasGrenade", "RMCGrenadeFlashBang" })
            {
                var grenade = entities.SpawnEntity(round, map.GridCoords);
                Assert.That(whitelist.IsWhitelistPassOrNull(ammo.Whitelist, grenade), Is.True, $"The grenadier's launcher won't take {round}.");
                entities.DeleteEntity(grenade);
            }

            entities.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The containment unit's armoured bio suit carries a weapon, and the bio hood doubles as a
    ///     gas mask with a filter fitted.
    /// </summary>
    [Test]
    public async Task ContainmentUnitIsArmedAndSealed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var inventory = server.System<InventorySystem>();
            var slots = server.System<ItemSlotsSystem>();
            var mob = Equip(pair, map.GridCoords, "CMUGearWYBioTechnician");

            Assert.That(inventory.TryGetSlotEntity(mob, "suitstorage", out var gun), Is.True, "The bio suit carries no weapon.");
            Assert.That(entities.HasComponent<GunComponent>(gun!.Value), Is.True);
            Assert.That(inventory.TryGetSlotEntity(mob, "head", out var hood), Is.True);
            Assert.That(slots.GetItemOrNull(hood!.Value, "filter"), Is.Not.Null, "The bio hood has no filter.");

            entities.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Auditors are armed, but nothing shows: the sidearm is under the jacket, not on a belt.
    /// </summary>
    [Test]
    public async Task AuditorsCarryAConcealedSidearm()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var inventory = server.System<InventorySystem>();
            var containers = server.System<SharedContainerSystem>();
            var mob = Equip(pair, map.GridCoords, "CMUGearWYIAAuditor");

            Assert.That(inventory.TryGetSlotEntity(mob, "belt", out _), Is.False, "The auditor wears a belt holster.");
            Assert.That(inventory.TryGetSlotEntity(mob, "jumpsuit", out var suit), Is.True);
            var webbing = entities.GetComponent<Content.Shared._RMC14.Webbing.WebbingClothingComponent>(suit!.Value).Webbing;
            Assert.That(webbing, Is.Not.Null, "The auditor's suit has no shoulder holster.");
            Assert.That(Stored(entities, containers, webbing!.Value).Any(entities.HasComponent<GunComponent>), Is.True,
                "The shoulder holster is empty.");

            entities.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A security detail can arrest people: a primary weapon, a less-lethal one and restraints.
    /// </summary>
    [TestCase("CMUGearWYSecurityTeamOfficer")]
    [TestCase("CMUGearWYSecurityTeamSergeant")]
    public async Task SecurityTeamCanArrest(string gear)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var inventory = server.System<InventorySystem>();
            var containers = server.System<SharedContainerSystem>();
            var mob = Equip(pair, map.GridCoords, gear);

            Assert.That(inventory.TryGetSlotEntity(mob, "suitstorage", out var rifle), Is.True);
            Assert.That(entities.HasComponent<GunComponent>(rifle!.Value), Is.True, $"{gear} has no primary weapon.");

            Assert.That(inventory.TryGetSlotEntity(mob, "back", out var bag), Is.True);
            var stored = Stored(entities, containers, bag!.Value);
            Assert.That(stored.Any(entities.HasComponent<HandcuffComponent>), Is.True, $"{gear} carries no restraints.");
            Assert.That(stored.Any(entities.HasComponent<GunComponent>), Is.True, $"{gear} carries no less-lethal sidearm.");

            entities.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }
}
