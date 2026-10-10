using System.Linq;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Marines;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Yautja;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.Damage.Systems;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using ServerHandsSystem = Content.Server.Hands.Systems.HandsSystem;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaPlaytestRegressionTest
{
    [Test]
    public async Task HunterPullsAMarineAtFullSpeed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default, marine = default;
        var before = 0f;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<MarineComponent>(marine);
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            before = entMan.GetComponent<MovementSpeedModifierComponent>(hunter).CurrentSprintSpeed;
            Assert.That(entMan.System<PullingSystem>().TryStartPull(hunter, marine), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var after = server.EntMan.GetComponent<MovementSpeedModifierComponent>(hunter).CurrentSprintSpeed;
            // the human base slowed this to 0.745x for a marine
            Assert.That(after, Is.EqualTo(before).Within(0.01f), "dragging a marine slowed the hunter down");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterOnlyFeelsPainNearDeath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var painSystem = entMan.System<SharedPainShockSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", Robust.Shared.Map.MapCoordinates.Nullspace);
            var human = entMan.SpawnEntity("CMMobHuman", Robust.Shared.Map.MapCoordinates.Nullspace);
            var hunterPain = entMan.GetComponent<PainShockComponent>(hunter);
            var humanPain = entMan.GetComponent<PainShockComponent>(human);

            hunterPain.Pain = humanPain.Pain = FixedPoint2.New(70);
            painSystem.RefreshTier(hunter);
            painSystem.RefreshTier(human);
            Assert.Multiple(() =>
            {
                Assert.That(painSystem.GetEffectiveTier(hunter, hunterPain), Is.EqualTo(PainTier.None),
                    "cmss13 preds feel nothing below the horrible threshold");
                Assert.That(painSystem.GetEffectiveTier(human, humanPain), Is.EqualTo(PainTier.Severe),
                    "humans should be untouched by the pred clamp");
            });

            hunterPain.Pain = FixedPoint2.New(95);
            painSystem.RefreshTier(hunter);
            Assert.Multiple(() =>
            {
                Assert.That(hunterPain.Tier, Is.EqualTo(PainTier.Severe),
                    "past horrible a pred only gets the distressing slowdown, never shock");
                Assert.That(hunterPain.InShock, Is.False);
            });

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThweiMendsAnUnsplintedFracture()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid treatedArm = default, untreatedArm = default;
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            EntityUid BreakArm(bool thwei)
            {
                var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                Assert.That(entMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(hunter,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
                Assert.That(entMan.System<SharedBoneSystem>().SeedFracture(arm, FractureSeverity.Simple), Is.True);
                if (thwei)
                    Assert.That(entMan.System<BloodstreamSystem>().TryAddToBloodstream(hunter, new([new("thwei", FixedPoint2.New(15))])), Is.True);

                return arm;
            }

            treatedArm = BreakArm(true);
            // control, so the test can't pass off the back of natural healing
            untreatedArm = BreakArm(false);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(30));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            FractureSeverity Severity(EntityUid arm)
                => entMan.TryGetComponent(arm, out FractureComponent? fracture) ? fracture.Severity : FractureSeverity.None;

            Assert.Multiple(() =>
            {
                Assert.That(Severity(untreatedArm), Is.Not.EqualTo(FractureSeverity.None), "the arm healed on its own, the test proves nothing");
                Assert.That(Severity(treatedArm), Is.EqualTo(FractureSeverity.None), "thwei left a pred's broken arm alone because it wasn't splinted");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VisorComesBackWhenTheBracerDoes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default, mask = default, bracer = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
            bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            foreach (var slot in new[] { "mask", "gloves", "eyes" })
            {
                if (inventory.TryGetSlotEntity(hunter, slot, out var existing))
                    entMan.DeleteEntity(existing.Value);
            }

            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", true, true), Is.True);
            Assert.That(inventory.TryEquip(hunter, mask, "mask", true, true), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() => server.EntMan.System<InventorySystem>().TryUnequip(hunter, "gloves", true, true));
        // long enough for a drain tick to notice there's no bracer
        await pair.RunTicksSync(pair.SecondsToTicks(5));
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.GetComponent<YautjaMaskComponent>(mask).VisorEnabled, Is.False));

        await server.WaitPost(() => server.EntMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", true, true));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<YautjaMaskComponent>(mask).VisorEnabled, Is.True,
                    "the visor stayed off until the mask was taken off and put back on");
                Assert.That(entMan.System<InventorySystem>().TryGetSlotEntity(hunter, "eyes", out var eyes) &&
                            entMan.HasComponent<YautjaMaskVisorGlassesComponent>(eyes), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMUYautjaPlasmaRifle")]
    [TestCase("CMUYautjaPlasmaPistol")]
    [TestCase("CMUYautjaCombistick")]
    [TestCase("CMUYautjaClanSword")]
    public async Task WeaponsFitInArmorSuitStorage(string weaponId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var armor = entMan.SpawnEntity("CMUYautjaClanArmor", map.GridCoords);
            var weapon = entMan.SpawnEntity(weaponId, map.GridCoords);
            foreach (var slot in new[] { "outerClothing", "suitstorage" })
            {
                if (inventory.TryGetSlotEntity(hunter, slot, out var existing))
                    entMan.DeleteEntity(existing.Value);
            }

            Assert.That(inventory.TryEquip(hunter, armor, "outerClothing", true, true), Is.True);
            // not forced, this goes through the same clothing-slot check as a player's click
            Assert.That(inventory.TryEquip(hunter, weapon, "suitstorage", true), Is.True,
                $"{weaponId} didn't fit in a pred's suit storage");

            entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntersAggressiveGrabCantBeClickedOff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid grabber = default, victim = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            grabber = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(entMan.System<PullingSystem>().TryStartPull(grabber, victim), Is.True);
        });

        // wait out the grab-upgrade delay, then go aggressive
        await pair.RunTicksSync(pair.SecondsToTicks(5));
        await server.WaitPost(() =>
        {
            var toggle = new RMCPullToggleEvent();
            server.EntMan.EventBus.RaiseLocalEvent(grabber, ref toggle);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.True);

            // what the being-pulled alert does
            var pullable = entMan.GetComponent<PullableComponent>(victim);
            Assert.That(entMan.System<PullingSystem>().TryStopPull(victim, pullable, victim), Is.False);
            Assert.That(pullable.Puller, Is.EqualTo(grabber), "one alert click broke an aggressive grab");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterCanThrowWhoeverItsCarrying()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default, victim = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(entMan.System<PullingSystem>().TryStartPull(hunter, victim), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(5));

        await server.WaitPost(() =>
        {
            var toggle = new RMCPullToggleEvent();
            server.EntMan.EventBus.RaiseLocalEvent(hunter, ref toggle);
        });
        await pair.RunTicksSync(5);

        // drag the grabbed human onto yourself, same as a player does to fireman carry
        await server.WaitPost(() =>
        {
            var drag = new DragDropDraggedEvent(hunter, hunter);
            server.EntMan.EventBus.RaiseLocalEvent(victim, ref drag);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(8));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<FiremanCarriableComponent>(victim).BeingCarried, Is.True, "the carry never started");

            var hands = entMan.System<SharedHandsSystem>();
            var carryHand = hands.EnumerateHands(hunter).First(hand =>
                hands.GetHeldItem(hunter, hand) is { } held &&
                entMan.TryGetComponent(held, out VirtualItemComponent? virtualItem) &&
                virtualItem.BlockingEntity == victim);
            hands.SetActiveHand(hunter, carryHand);

            var target = entMan.GetComponent<TransformComponent>(hunter).Coordinates.Offset(new System.Numerics.Vector2(3, 0));
            Assert.That(entMan.System<ServerHandsSystem>().ThrowHeldItem(hunter, target), Is.True);
            Assert.That(entMan.GetComponent<FiremanCarriableComponent>(victim).BeingCarried, Is.False,
                "a pred couldn't throw the human it was carrying");
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.GetComponent<FiremanCarriableComponent>(victim).CanThrow, Is.False,
                "a marine carrying the same guy later shouldn't inherit the pred's throw"));

        await pair.CleanReturnAsync();
    }

    // a full hunter kit used to stack its armor into near immunity, it should soften hazards, not erase them
    [Test]
    public async Task GearedHunterStillFeelsExplosionsAndFire()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid bare = default, geared = default;
        var explosion = new Dictionary<EntityUid, float>();
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            entMan.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false, new GasMixture(moles, Atmospherics.T20C));

            bare = entMan.SpawnEntity("CMUMobYautja", new MapCoordinates(new System.Numerics.Vector2(0, 200), map.MapId));
            geared = entMan.SpawnEntity("CMUMobYautja", new MapCoordinates(new System.Numerics.Vector2(60, 200), map.MapId));
            var gear = server.ResolveDependency<IPrototypeManager>().Index<StartingGearPrototype>("CMUYautjaHunterGear");
            entMan.System<StationSpawningSystem>().EquipStartingGear(geared, gear);
        });
        await pair.RunTicksSync(5);

        // a small blast, so neither hunter hits the damage cap and the ratio means something
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var hunter in new[] { bare, geared })
            {
                entMan.System<ExplosionSystem>().QueueExplosion(
                    entMan.System<SharedTransformSystem>().GetMapCoordinates(hunter), "RMC", 15, 5, 10, null, addLog: false);
            }
        });
        await pair.RunTicksSync(30);

        await server.WaitPost(() =>
        {
            var damage = server.EntMan.System<DamageableSystem>();
            foreach (var hunter in new[] { bare, geared })
            {
                explosion[hunter] = damage.GetTotalDamage(hunter).Float();
                TestContext.Out.WriteLine($"explosion {(hunter == bare ? "bare" : "geared")}: {explosion[hunter]}");
                damage.SetAllDamage(hunter, 0);
            }

            // rmc incendiary fire, the flamer path, not atmos
            var flammable = server.EntMan.System<Content.Shared._RMC14.Atmos.SharedRMCFlammableSystem>();
            Assert.That(flammable.Ignite((bare, null), 30, 20, null), Is.True);
            Assert.That(flammable.Ignite((geared, null), 30, 20, null), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(6));

        await server.WaitAssertion(() =>
        {
            var damage = server.EntMan.System<DamageableSystem>();
            var bareFire = damage.GetTotalDamage(bare).Float();
            var gearedFire = damage.GetTotalDamage(geared).Float();
            TestContext.Out.WriteLine($"fire bare: {bareFire} geared: {gearedFire}");

            Assert.Multiple(() =>
            {
                Assert.That(explosion[bare], Is.GreaterThan(0f), "the test blast didn't reach the hunter");
                Assert.That(explosion[geared], Is.GreaterThan(explosion[bare] * 0.3f),
                    "a full kit still blocks almost the whole blast");
                Assert.That(gearedFire, Is.GreaterThan(bareFire * 0.3f),
                    "a full kit still makes the hunter fireproof");
            });
        });

        await pair.CleanReturnAsync();
    }
}
