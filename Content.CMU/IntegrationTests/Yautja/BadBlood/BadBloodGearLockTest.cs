using Content.IntegrationTests.Fixtures;
using Content.Shared.Clothing.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
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

    // CMU14 ECS-602: what a grunt lands in, against the honourable pieces they are copied from
    private const string GruntMask = "CMUYautjaMaskPred01Crimson";
    private const string GruntMesh = "CMUYautjaBadBloodBodyMesh";
    private const string GruntHarness = "CMUYautjaBadBloodCloakPackPlain";
    private const string HonourableMask = "CMUYautjaMask";
    private const string HonourableMesh = "CMUYautjaBodyMesh";
    private const string HonourableHarness = "CMUYautjaCloakPack";

    // gear marines are meant to handle: the uplink kit, the star chart and the bracer's ID chip
    private static readonly HashSet<string> MarineHandled =
    [
        "CMUBadBloodFieldRelayKit",
        "CMUBadBloodClanStarChart",
        "CMUYautjaBadBloodBracerIdChip",
    ];

    // CMU14 TEST-602: worn kit pieces that stay unlocked on purpose
    private static readonly Dictionary<string, string> WornAllowlist = new()
    {
        // holds the trophies a Bad Blood has taken; a human wearing it gains nothing, and it is honourable gear
        ["CMUYautjaTrophyBelt"] = "a trophy holder, not wearable tech",
        // a pocket consumable, not clothing; YautjaTechItem already stops a human using it in hand
        ["CMUYautjaHealthShard"] = "a pocket consumable",
    };

    // CMU14 TEST-602: carried (in-hand or packed) clothing and weapons that stay unlocked on purpose
    private static readonly Dictionary<string, string> CarriedAllowlist = new()
    {
        // a hide canteen (CMCanteen): its only melee is the drink base's bottle bash, and marines carry canteens anyway
        ["CMUYautjaWaterFlask"] = "a canteen",
    };

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
            var gruntMask = SEntMan.SpawnEntity(GruntMask, map.GridCoords);
            var gruntMesh = SEntMan.SpawnEntity(GruntMesh, map.GridCoords);
            var gruntHarness = SEntMan.SpawnEntity(GruntHarness, map.GridCoords);
            var honourableMask = SEntMan.SpawnEntity(HonourableMask, map.GridCoords);
            var honourableMesh = SEntMan.SpawnEntity(HonourableMesh, map.GridCoords);
            var honourableHarness = SEntMan.SpawnEntity(HonourableHarness, map.GridCoords);

            var locks = Server.System<BadBloodGearLockSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(locks.IsAllowed(human), Is.False);
                Assert.That(locks.IsAllowed(grunt), Is.True);

                Assert.That(inventory.TryEquip(human, armor, "outerClothing", silent: true), Is.False,
                    "a human cannot wear Bad Blood armor");

                // CMU14 ECS-602: the grunt's crimson mask, mesh and harness are locked; the honourable pieces they
                // copy are not, so the same human in the same slots proves the lock is what refuses them
                Assert.That(inventory.TryEquip(human, gruntMask, "mask", silent: true), Is.False,
                    "a human cannot wear a Bad Blood grunt's mask");
                Assert.That(inventory.TryEquip(human, gruntMesh, "jumpsuit", silent: true), Is.False,
                    "a human cannot wear a Bad Blood grunt's mesh");
                Assert.That(inventory.TryEquip(human, gruntHarness, "back", silent: true), Is.False,
                    "a human cannot wear a Bad Blood grunt's harness");
                Assert.That(inventory.TryEquip(human, honourableMask, "mask", silent: true), Is.True,
                    "the honourable mask is not locked");
                Assert.That(inventory.TryEquip(human, honourableMesh, "jumpsuit", silent: true), Is.True,
                    "the honourable mesh is not locked");
                Assert.That(inventory.TryEquip(human, honourableHarness, "back", silent: true), Is.True,
                    "the honourable harness is not locked");

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

            foreach (var uid in new[]
                     {
                         human, grunt, armor, blades, gruntMask, gruntMesh, gruntHarness, honourableMask, honourableMesh,
                         honourableHarness,
                     })
            {
                // the honourable pieces went with the human who wore them
                if (SEntMan.EntityExists(uid))
                    SEntMan.DeleteEntity(uid);
            }
        });
    }

    // every Bad Blood item a player can hold carries the lock, so new gear cannot slip through. This sweep goes by ID;
    // EveryBadBloodKitItemIsLocked below walks what the mobs actually land with
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

    // CMU14 TEST-602: walk what every Bad Blood mob lands with (loadout starting gear: worn, in hand, packed, plus the
    // bracer's weapon choices) instead of trusting IDs, which let the honourable harness, mesh and mask through
    [Test]
    public async Task EveryBadBloodKitItemIsLocked()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var factory = SEntMan.ComponentFactory;
            var mobs = 0;
            var allowed = new HashSet<string>();

            bool Has<T>(EntityPrototype proto) where T : IComponent, new()
            {
                return proto.TryComp<T>(out _, factory);
            }

            // worn gear must be locked: YautjaTechItem does not stop a human wearing it
            void CheckWorn(string source, EntProtoId id)
            {
                if (WornAllowlist.ContainsKey(id))
                {
                    allowed.Add(id);
                    return;
                }

                var proto = protos.Index(id);
                Assert.That(Has<BadBloodGearLockComponent>(proto), Is.True,
                    $"{source}: {id} is worn Bad Blood kit without BadBloodGearLock");

                // the bracer's deployable weapons, including the one a grunt picks
                if (!proto.TryComp<YautjaGearContainerComponent>(out var bay, factory))
                    return;

                if (proto.TryComp<YautjaBadBloodGearChoiceComponent>(out var choice, factory))
                {
                    foreach (var kind in choice.Choices)
                    {
                        Assert.That(bay.GearPrototypes.ContainsKey(kind), Is.True,
                            $"{source}: {id} offers {kind} but has no prototype for it");
                    }
                }

                foreach (var (kind, weapon) in bay.GearPrototypes)
                {
                    Assert.That(Has<BadBloodGearLockComponent>(protos.Index(weapon)), Is.True,
                        $"{source}: {id} {kind} weapon {weapon} has no BadBloodGearLock");
                }
            }

            // carried clothing and weapons need the lock or YautjaTechItem; supplies are not checked
            void CheckCarried(string source, EntProtoId id)
            {
                var proto = protos.Index(id);
                if (!Has<ClothingComponent>(proto) && !Has<MeleeWeaponComponent>(proto) && !Has<GunComponent>(proto))
                    return;

                if (CarriedAllowlist.ContainsKey(id))
                {
                    allowed.Add(id);
                    return;
                }

                Assert.That(Has<BadBloodGearLockComponent>(proto) || Has<YautjaTechItemComponent>(proto), Is.True,
                    $"{source}: {id} is carried Bad Blood kit without BadBloodGearLock or YautjaTechItem");
            }

            Assert.Multiple(() =>
            {
                foreach (var mob in protos.EnumeratePrototypes<EntityPrototype>())
                {
                    if (mob.Abstract ||
                        !Has<YautjaBadBloodComponent>(mob) ||
                        !mob.TryComp<LoadoutComponent>(out var loadout, factory) ||
                        loadout.StartingGear is not { } gears)
                    {
                        continue;
                    }

                    mobs++;
                    foreach (var gearId in gears)
                    {
                        var gear = protos.Index(gearId);
                        var kit = $"{mob.ID} / {gearId}";
                        foreach (var (slot, item) in gear.Equipment)
                        {
                            CheckWorn($"{kit} {slot}", item);
                        }

                        foreach (var item in gear.Inhand)
                        {
                            CheckCarried($"{kit} in hand", item);
                        }

                        foreach (var (slot, items) in gear.Storage)
                        {
                            foreach (var item in items)
                            {
                                CheckCarried($"{kit} {slot} storage", item);
                            }
                        }
                    }
                }
            });

            Assert.That(mobs, Is.GreaterThanOrEqualTo(2), "the Bad Blood grunt and Leader were not found");
            Assert.That(allowed, Is.EquivalentTo(WornAllowlist.Keys.Concat(CarriedAllowlist.Keys)),
                "an allowlist entry is no longer in any Bad Blood kit; remove it");
        });
    }
}
