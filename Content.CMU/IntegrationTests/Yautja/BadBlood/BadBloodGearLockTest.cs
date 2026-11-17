using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-01: a human cannot wear or swing Bad Blood gear. Yautja can. Picking it up stays open so marines can loot or
// carry it off.
[TestFixture]
public sealed class BadBloodGearLockTest : GameTest
{
    private const string Human = "CMMobHuman";
    private const string Armor = "CMUYautjaBadBloodArmorPatchwork";
    private const string Blades = "CMUYautjaBadBloodWristBlades";

    // gear marines are meant to handle: the uplink kit, the star chart and the bracer's ID chip
    private static readonly HashSet<string> MarineHandled =
    [
        "CMUBadBloodFieldRelayKit",
        "CMUBadBloodClanStarChart",
        "CMUYautjaBadBloodBracerIdChip",
    ];

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task HumansCannotWearOrSwingBadBloodGear()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var inventory = Server.System<InventorySystem>();
            var human = SEntMan.SpawnEntity(Human, map.GridCoords);
            var grunt = SEntMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
            var armor = SEntMan.SpawnEntity(Armor, map.GridCoords);
            var blades = SEntMan.SpawnEntity(Blades, map.GridCoords);

            var locks = Server.System<BadBloodGearLockSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(locks.IsAllowed(human), Is.False);
                Assert.That(locks.IsAllowed(grunt), Is.True);

                Assert.That(inventory.TryEquip(human, armor, "outerClothing", silent: true), Is.False,
                    "a human cannot wear Bad Blood armor");

                var humanSwing = new AttemptMeleeEvent(human);
                SEntMan.EventBus.RaiseLocalEvent(blades, ref humanSwing);
                Assert.That(humanSwing.Cancelled, Is.True, "a human cannot swing Bad Blood blades");

                var gruntSwing = new AttemptMeleeEvent(grunt);
                SEntMan.EventBus.RaiseLocalEvent(blades, ref gruntSwing);
                Assert.That(gruntSwing.Cancelled, Is.False, "a Bad Blood can");

                var pickup = new GettingPickedUpAttemptEvent(human, armor, false);
                SEntMan.EventBus.RaiseLocalEvent(armor, pickup);
                Assert.That(pickup.Cancelled, Is.False, "anyone may still pick it up");
            });

            foreach (var uid in new[] { human, grunt, armor, blades })
            {
                SEntMan.DeleteEntity(uid);
            }
        });
    }

    // every Bad Blood item a player can hold carries the lock, so new gear cannot slip through
    [Test]
    public async Task EveryBadBloodItemIsLocked()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var factory = SEntMan.ComponentFactory;
            foreach (var proto in protos.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.ID.Contains("BadBlood") || MarineHandled.Contains(proto.ID) ||
                    !proto.TryComp<ItemComponent>(out _, factory) ||
                    proto.TryComp<Content.Shared.Stacks.StackComponent>(out _, factory) ||
                    proto.TryComp<Content.Shared.Mobs.Components.MobStateComponent>(out _, factory) ||
                    proto.ID.Contains("Organ") || proto.ID.StartsWith("CMUHunterShipPlaced"))
                {
                    continue;
                }

                Assert.That(proto.TryComp<BadBloodGearLockComponent>(out _, factory), Is.True,
                    $"{proto.ID} is Bad Blood gear without BadBloodGearLock");
            }
        });
    }
}
