using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Requisitions;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Shared._RMC14.Medal;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared.Antag;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.CMU14.PlayTimeTracking;
using Content.Shared.CMU14.Traits.NicotineAddiction;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class NovemberTwentySevenRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    // BUG-1569: the medal tier enum is a byte, so walking down past Bronze wrapped to 255 and froze the Stats window
    [Test]
    public void MedalTiersStopAtBronze()
    {
        Assert.That(CMUPlaytimeMedals.Descending(RMCPlaytimeMedalType.Gold),
            Is.EqualTo(new[] { RMCPlaytimeMedalType.Gold, RMCPlaytimeMedalType.Silver, RMCPlaytimeMedalType.Bronze }));
        Assert.That(CMUPlaytimeMedals.Descending(RMCPlaytimeMedalType.Bronze), Is.EqualTo(new[] { RMCPlaytimeMedalType.Bronze }));
    }

    // BUG-1568: a storage item whose max item size is >= the bag's can't go in, and the tattoo gun said Normal
    [Test]
    public async Task TattooGunFitsInABackpack()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var bag = SEntMan.SpawnEntity("CMBackpackMarine", map.GridCoords);
            var gun = SEntMan.SpawnEntity("AU14TattooGun", map.GridCoords);
            Assert.That(SEntMan.System<SharedStorageSystem>().Insert(bag, gun, out _, playSound: false), Is.True);
        });
    }

    // BUG-1564: the sapper's base caste was the abstract CMXenoBoiler, so the destrain choice never showed
    [Test]
    public async Task EveryStrainBaseCasteIsAPlayableCaste()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var checkedStrains = 0;
            foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.TryGetComponent(out XenoStrainComponent? strain, factory) ||
                    strain.BaseCaste is not { } baseCaste)
                {
                    continue;
                }

                checkedStrains++;
                Assert.That(SProtoMan.TryIndex(baseCaste, out var caste) && !caste.Abstract, Is.True,
                    $"{proto.ID} destrains into {baseCaste}, which can't be spawned");
            }

            Assert.That(checkedStrains, Is.GreaterThan(10));
        });
    }

    // BUG-1508: the M2C door gun fell back to its RMC parent's "scoped M56D" description
    [Test]
    public async Task M2CDoorGunHasItsOwnDescription()
    {
        await Server.WaitAssertion(() =>
        {
            var gun = SProtoMan.Index<EntityPrototype>("CMUM2CDoorGunDropship");
            Assert.That(gun.Description, Does.Contain("M2C"));
            Assert.That(gun.Description, Does.Not.Contain("M56D"));
        });
    }

    // BUG-1518: overwatch consoles spend from "GOVFOR" while the account is "govfor", which opened an empty account
    [Test]
    public async Task SupplyDropsSpendFromTheFactionAccountWhateverTheCase()
    {
        await Server.WaitAssertion(() =>
        {
            var requisitions = SEntMan.System<RequisitionsSystem>();
            var before = requisitions.GetFunds("govfor");
            Assert.That(before, Is.GreaterThan(100));
            Assert.That(requisitions.TrySpendFunds("GOVFOR", 100), Is.True);
            Assert.That(requisitions.GetFunds("govfor"), Is.EqualTo(before - 100));

            var accounts = 0;
            var query = SEntMan.EntityQueryEnumerator<RequisitionsAccountComponent>();
            while (query.MoveNext(out var account))
            {
                var faction = account.Faction;
                if (string.Equals(faction, "govfor", StringComparison.OrdinalIgnoreCase))
                    accounts++;
            }

            Assert.That(accounts, Is.EqualTo(1), "GOVFOR and govfor are the same account");
        });
    }

    // BUG-1573: the catalog resale cap capped a refilled canister at half the empty canister's price
    [Test]
    public async Task FrezonPumpedIntoACatalogCanisterStillSells()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var requisitions = SEntMan.System<RequisitionsSystem>();

            var empty = SEntMan.SpawnEntity("CMUCanisterEmpty", map.GridCoords);
            var emptyShipment = new RequisitionsSystem.AsrsShipment();
            Assert.That(requisitions.TryCollectAsrsCargo(empty, emptyShipment), Is.True);
            Assert.That(emptyShipment.Regular, Is.LessThanOrEqualTo(75), "an unchanged catalog canister still resells at half");

            var filled = SEntMan.SpawnEntity("CMUCanisterEmpty", map.GridCoords);
            SEntMan.GetComponent<GasCanisterComponent>(filled).Air.SetMoles(Gas.Frezon, 5000);
            var filledShipment = new RequisitionsSystem.AsrsShipment();
            Assert.That(requisitions.TryCollectAsrsCargo(filled, filledShipment), Is.True);
            Assert.That(filledShipment.Regular, Is.GreaterThan(emptyShipment.Regular + 500),
                "5000 mol of frezon must pay well above the empty canister");
        });
    }

    // BUG-1524: antag gear for a slot the job already filled was deleted, so a WY agent with full pockets had no data link
    [Test]
    public async Task AntagGearForAFullSlotEndsUpInTheirBag()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var agent = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(inventory.TryEquip(agent, SEntMan.SpawnEntity("CMJumpsuitColonist", map.GridCoords), "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(agent, SEntMan.SpawnEntity("CMBackpackMarine", map.GridCoords), "back", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(agent, SEntMan.SpawnEntity("RMCLighter", map.GridCoords), "pocket1", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(agent, SEntMan.SpawnEntity("RMCLighter", map.GridCoords), "pocket2", silent: true, force: true), Is.True);

            var rule = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var selection = SEntMan.AddComponent<AntagSelectionComponent>(rule);
            var def = SProtoMan.Index<AntagSpecifierPrototype>("WeylandYutaniAgent");
            var ev = new AfterAntagEntitySelectedEvent(null, agent, (rule, selection), def);
            SEntMan.EventBus.RaiseLocalEvent(rule, ref ev, true);

            var carried = inventory.GetHandOrInventoryEntities(agent)
                .SelectMany(Contents)
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID)
                .ToList();
            Assert.That(carried, Does.Contain("CMUCorporateDataLink"));
            Assert.That(carried, Does.Contain("CMUIDCardWYAgent"));
        });
    }

    // BUG-1500: synthetic bodies can't metabolize nicotine, so a runaway synth's craving could never be met
    [Test]
    public async Task SynthsLoseNicotineAddiction()
    {
        var map = await Pair.CreateTestMap();
        EntityUid synth = default;
        await Server.WaitPost(() =>
        {
            synth = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<NicotineAddictionComponent>(synth);
            SEntMan.EnsureComponent<SynthComponent>(synth);
        });

        await Server.WaitRunTicks(5);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<NicotineAddictionComponent>(synth), Is.False));
    }

    // BUG-1539: hurled debris stayed on the floor and anyone could keep throwing 150 structural damage with it.
    // The bare test map has no z-level ground, so a real throw never lands here; raise the landing directly.
    [Test]
    public async Task HurledDebrisCrumblesWhenItLands()
    {
        var map = await Pair.CreateTestMap();
        EntityUid debris = default;
        await Server.WaitPost(() =>
        {
            debris = SEntMan.SpawnEntity("CMUApeDebrisRock", map.GridCoords);
            var land = new LandEvent(null, false);
            SEntMan.EventBus.RaiseLocalEvent(debris, ref land);
        });

        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(debris), Is.True));
    }

    private IEnumerable<EntityUid> Contents(EntityUid uid)
    {
        yield return uid;
        if (!SEntMan.TryGetComponent(uid, out ContainerManagerComponent? manager))
            yield break;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                foreach (var inner in Contents(contained))
                    yield return inner;
            }
        }
    }
}
