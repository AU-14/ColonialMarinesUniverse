using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberTwentyTwoRegressionTest : GameTest
{
    // BUG-1020: only the blight core's build marker counted toward the hive cap, so the cap never filled
    [Test]
    public async Task BuiltBlightCoresCountTowardTheHiveCap()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var core = SProtoMan.Index<EntityPrototype>("CMU14PathogenBlightCore");
            var node = SProtoMan.Index<EntityPrototype>("CMU14PathogenBlightCoreConstructionNode");
            Assert.That(core.TryGetComponent(out HiveConstructionLimitedComponent? coreLimit, factory), Is.True);
            Assert.That(node.TryGetComponent(out HiveConstructionLimitedComponent? nodeLimit, factory), Is.True);
            Assert.That(coreLimit!.Id, Is.EqualTo(nodeLimit!.Id));
        });
    }

    // BUG-1049: ghosts got the open/close cargo bay verb on the Bison
    [Test]
    public async Task GhostsDoNotGetTheCargoBayVerb()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var carrier = SEntMan.SpawnEntity("CMUCargoCarrier", map.GridCoords);
            var ghost = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
            var verbs = SEntMan.System<SharedVerbSystem>().GetLocalVerbs(carrier, ghost, typeof(AlternativeVerb), force: true);
            Assert.That(verbs.Any(v => v.Text.Contains("cargo bay")), Is.False);
        });
    }

    // BUG-1046: the chemical payload took jugs, buckets and canisters
    [TestCase("CMBeakerLarge", true)]
    [TestCase("RMCBeakerHighCapacity", false)]
    public async Task ChemicalPayloadOnlyTakesBeakerSizedContainers(string container, bool fits)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var payload = SEntMan.SpawnEntity("ChemicalPayload", map.GridCoords);
            var item = SEntMan.SpawnEntity(container, map.GridCoords);
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(payload, "BeakerSlotA", out var slot), Is.True);
            Assert.That(slots.CanInsert(payload, slot!, item, null), Is.EqualTo(fits));
        });
    }

    // BUG-1045: the colony headsets colony cargo sells have to come keyed for the colony channel
    [Test]
    public async Task CargoColonyHeadsetsCarryTheColonyKey()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var crate = SEntMan.SpawnEntity("CMUCrateColonyHeadsets", map.GridCoords);
            var headsets = SEntMan.GetComponent<EntityStorageComponent>(crate).Contents.ContainedEntities.ToList();
            Assert.That(headsets, Has.Count.EqualTo(5));
            foreach (var headset in headsets)
            {
                Assert.That(SEntMan.GetComponent<EncryptionKeyHolderComponent>(headset).Channels,
                    Does.Contain(new ProtoId<RadioChannelPrototype>("Colony")));
            }
        });
    }

    // BUG-1050: shells and loaded shotguns put through a supply crate
    [Test]
    public async Task ShotgunShellsSurviveASupplyCrate()
    {
        var map = await Pair.CreateTestMap();
        EntityUid crate = default, shells = default, shotgun = default;
        var roundsBefore = 0;
        await Server.WaitAssertion(() =>
        {
            var storage = SEntMan.System<SharedEntityStorageSystem>();
            crate = SEntMan.SpawnEntity("RMCCrateSupply", map.GridCoords);
            shells = SEntMan.SpawnEntity("CMShellShotgunBuckshot", map.GridCoords);
            shotgun = SEntMan.SpawnEntity("RMCWeaponShotgunM37A1", map.GridCoords);

            var ev = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(shotgun, ref ev);
            roundsBefore = ev.Count;

            storage.OpenStorage(crate);
            Assert.That(storage.Insert(shells, crate), Is.True);
            Assert.That(storage.Insert(shotgun, crate), Is.True);
            storage.CloseStorage(crate);
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedEntityStorageSystem>().OpenStorage(crate);
            Assert.That(SEntMan.Deleted(shells), Is.False, "shells deleted");
            Assert.That(SEntMan.GetComponent<StackComponent>(shells).Count, Is.EqualTo(5));
            Assert.That(SEntMan.Deleted(shotgun), Is.False, "shotgun deleted");

            var ev = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(shotgun, ref ev);
            Assert.That(ev.Count, Is.EqualTo(roundsBefore));
        });
    }
}
