using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.IntelligenceSection;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Medical;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Roles;

/// <summary>
///     The Case Officer spawns with a briefcase cuffed to the hand and its key in a pocket. Only that key uncuffs or
///     opens it, wire cutters cut the cuff, and a swallowed key comes back up with vomit.
/// </summary>
[TestFixture]
public sealed class CMUIntelCuffedBriefcaseTest : GameTest
{
    private EntityUid _officer;
    private EntityUid _briefcase;
    private EntityUid _key;

    [TestCase(true)]
    [TestCase(false)]
    public async Task DeletingPairedItemClearsSurvivingLink(bool deleteBriefcase)
    {
        await SpawnOfficer();

        await Server.WaitAssertion(() =>
        {
            SEntMan.DeleteEntity(deleteBriefcase ? _briefcase : _key);

            if (deleteBriefcase)
            {
                Assert.That(SComp<CMUBriefcaseKeyComponent>(_key).Briefcase, Is.Null,
                    "the surviving key must not serialize a deleted briefcase");
                Assert.That(SEntMan.HasComponent<CMUCuffedBriefcaseWearerComponent>(_officer), Is.False,
                    "the officer must no longer be linked to the deleted briefcase");
            }
            else
            {
                Assert.That(SComp<CMUCuffedBriefcaseComponent>(_briefcase).Key, Is.Null,
                    "the surviving briefcase must not serialize a deleted key");
                Assert.That(SComp<CMUCuffedBriefcaseComponent>(_briefcase).CuffedTo, Is.EqualTo(_officer),
                    "destroying the key must not unlock the cuff");
            }
        });

        // Exercise state serialization after the deletion, which previously logged a missing metadata error.
        await RunTicksSync(10);
    }

    [Test]
    public async Task CaseOfficerBriefcaseCuffsUnlocksAndCuts()
    {
        await SpawnOfficer();

        await Server.WaitAssertion(() =>
        {
            var inventory = Server.System<InventorySystem>();
            var hands = Server.System<SharedHandsSystem>();
            var comp = SComp<CMUCuffedBriefcaseComponent>(_briefcase);

            // Spawned cuffed, with the paired key in the free pocket.
            Assert.That(comp.CuffedTo, Is.EqualTo(_officer), "the briefcase should spawn cuffed to the officer");
            Assert.That(SEntMan.HasComponent<UnremoveableComponent>(_briefcase), Is.True);
            Assert.That(inventory.TryGetSlotEntity(_officer, "pocket2", out var pocketed), Is.True, "the key should be in the free pocket");
            Assert.That(pocketed, Is.EqualTo(_key));
            Assert.That(SComp<CMUBriefcaseKeyComponent>(_key).Briefcase, Is.EqualTo(_briefcase));

            // Cuffed: it cannot be dropped.
            Assert.That(hands.TryDrop(_officer, _briefcase, checkActionBlocker: false), Is.False, "a cuffed briefcase was dropped");

            // Shut unless the key is in hand.
            var storage = Server.System<SharedStorageSystem>();
            Assert.That(storage.CanInteract(_officer, (_briefcase, SComp<StorageComponent>(_briefcase))), Is.False,
                "the briefcase opened without its key in hand");
            Assert.That(inventory.TryUnequip(_officer, "pocket2", silent: true, force: true), Is.True);
            Assert.That(hands.TryPickupAnyHand(_officer, _key, checkActionBlocker: false), Is.True);
            Assert.That(storage.CanInteract(_officer, (_briefcase, SComp<StorageComponent>(_briefcase))), Is.True,
                "the briefcase should open for whoever holds its key");

            // Another briefcase's key does not fit.
            var otherCase = SEntMan.SpawnEntity("CMUIntelCuffedBriefcase", SComp<TransformComponent>(_officer).Coordinates);
            var otherKey = SComp<CMUCuffedBriefcaseComponent>(otherCase).Key;
            Assert.That(otherKey, Is.Not.Null, "a new briefcase should come with its own key");
            Use(otherKey!.Value, _briefcase);
        });

        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUCuffedBriefcaseComponent>(_briefcase).CuffedTo, Is.EqualTo(_officer), "the wrong key uncuffed the briefcase");

            // The right key uncuffs it.
            Use(_key, _briefcase);
        });

        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUCuffedBriefcaseComponent>(_briefcase).CuffedTo, Is.Null, "the key should uncuff the briefcase");
            Assert.That(SEntMan.HasComponent<UnremoveableComponent>(_briefcase), Is.False);
            Assert.That(SEntMan.HasComponent<CMUCuffedBriefcaseWearerComponent>(_officer), Is.False);

            // Using the key again while holding the case cuffs it back on.
            Use(_key, _briefcase);
        });

        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUCuffedBriefcaseComponent>(_briefcase).CuffedTo, Is.EqualTo(_officer), "the key should cuff the briefcase back on");

            // Swap the key for wire cutters and cut the cuff.
            var hands = Server.System<SharedHandsSystem>();
            Assert.That(hands.TryDrop(_officer, _key, checkActionBlocker: false), Is.True);
            var cutters = SEntMan.SpawnEntity("CMWirecutter", SComp<TransformComponent>(_officer).Coordinates);
            Assert.That(hands.TryPickupAnyHand(_officer, cutters, checkActionBlocker: false), Is.True);
            Use(cutters, _briefcase);
        });

        await RunSeconds(35);
        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUCuffedBriefcaseComponent>(_briefcase);
            Assert.That(comp.CuffedTo, Is.Null, "wire cutters should cut the cuff in 30 seconds");
            Assert.That(comp.CuffCut, Is.True);
            Assert.That(Server.System<SharedHandsSystem>().TryDrop(_officer, _briefcase, checkActionBlocker: false), Is.True,
                "a cut-free briefcase should drop");
        });
    }

    [Test]
    public async Task SwallowedKeyComesBackUpWithVomit()
    {
        await SpawnOfficer();

        await Server.WaitAssertion(() =>
        {
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryUnequip(_officer, "pocket2", silent: true, force: true), Is.True);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(_officer, _key, checkActionBlocker: false), Is.True);
            Server.System<CMUCuffedBriefcaseSystem>().TryStartSwallowing((_key, SComp<CMUBriefcaseKeyComponent>(_key)), _officer);
        });

        await RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainingContainer(_key, out var container), Is.True);
            Assert.That(container!.ID, Is.EqualTo(CMUSwallowedItemsComponent.ContainerId), "the key should be swallowed");
            Assert.That(container.Owner, Is.EqualTo(_officer));

            Server.System<VomitSystem>().Vomit(_officer, force: true);
            Assert.That(containers.IsEntityInContainer(_key), Is.False, "vomiting should bring the key back up");
        });
    }

    private async Task SpawnOfficer()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            _officer = Server.System<StationSpawningSystem>()
                .SpawnPlayerMob(map.GridCoords, new ProtoId<JobPrototype>("CMUJobIntelCaseOfficer"), new HumanoidCharacterProfile(), station: null);

            _briefcase = Server.System<SharedHandsSystem>().EnumerateHeld(_officer)
                .FirstOrDefault(held => SEntMan.HasComponent<CMUCuffedBriefcaseComponent>(held));
            Assert.That(_briefcase.Valid, Is.True, "the Case Officer should spawn holding the cuffed briefcase");
            _key = SComp<CMUCuffedBriefcaseComponent>(_briefcase).Key!.Value;

            // The section's phone spawns in the other hand; put it down to free that hand, as a player would.
            var hands = Server.System<SharedHandsSystem>();
            foreach (var held in hands.EnumerateHeld(_officer).ToList())
            {
                TestContext.Progress.WriteLine($"Case Officer spawned holding {SComp<MetaDataComponent>(held).EntityPrototype?.ID}");
                if (held != _briefcase)
                    Assert.That(hands.TryDrop(_officer, held, checkActionBlocker: false), Is.True);
            }
        });
    }

    private void Use(EntityUid used, EntityUid target)
    {
        Server.System<SharedInteractionSystem>().InteractUsing(_officer, used, target,
            SComp<TransformComponent>(target).Coordinates, checkCanInteract: false, checkCanUse: false);
    }

    private Task RunSeconds(int seconds)
    {
        return RunTicksSync(seconds * Server.ResolveDependency<IGameTiming>().TickRate);
    }
}
