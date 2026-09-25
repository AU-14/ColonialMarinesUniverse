#pragma warning disable RA0002 // Arrange module damage and operator/view state explicitly.

using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Vehicle;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids.Stab;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
// CMU14
public sealed class VehicleGameplayRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: damageModifierSet
          id: VehicleGameplayHalfDamage
          coefficients:
            Piercing: 0.5

        - type: entity
          id: VehicleGameplayModule
          components:
          - type: HardpointItem
            hardpointType: Support
          - type: HardpointIntegrity
            maxIntegrity: 100

        - type: entity
          parent: VehicleGameplayModule
          id: VehicleGameplayArmor
          components:
          - type: HardpointItem
            hardpointType: Support
            damageRegion: Front
          - type: VehicleArmorHardpoint
            modifierSets: [VehicleGameplayHalfDamage]

        - type: entity
          parent: VehicleGameplayModule
          id: VehicleGameplayTurret
          components:
          - type: VehicleTurret
          - type: HardpointSlots
            slots:
            - id: child
              hardpointType: Support
              required: false
          - type: ItemSlots
            slots:
              child:
                startingItem: VehicleGameplayModule

        - type: entity
          id: VehicleGameplayChassis
          components:
          - type: Vehicle
            movementKind: Grid
            transferDamage: false
            requiresHands: false
          - type: VehicleOperatorDamage
          - type: GridVehicleMover
          - type: Physics
          - type: Fixtures
          - type: Damageable
            damageModifierSet: VehicleGameplayHalfDamage
          - type: Injurable
            damageContainer: Inorganic
          - type: HardpointIntegrity
          - type: HardpointSlots
            slots:
            - id: armor
              hardpointType: Support
              required: false
            - id: turret
              hardpointType: Support
              required: false
            - id: support
              hardpointType: Support
              required: false
          - type: ItemSlots
            slots:
              armor:
                startingItem: VehicleGameplayArmor
              turret:
                startingItem: VehicleGameplayTurret
              support:
                startingItem: VehicleGameplayModule
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task ArmorAppliesBeforeOneBudgetIncludingNestedModules(bool explosion)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleGameplayChassis", map.GridCoords);
            var attacker = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(0, -3)));
            var parts = Parts(vehicle);
            Assert.That(parts, Has.Length.EqualTo(4));
            Server.System<DamageableSystem>().TryChangeDamage(vehicle,
                new DamageSpecifier { DamageDict = { ["Piercing"] = 40 } }, origin: attacker,
                impact: explosion ? DamageImpact.Explosion : DamageImpact.Projectile);

            var losses = parts.Select(p => 100f - Integrity(p).Integrity).ToArray();
            Assert.That(losses.Sum(), Is.EqualTo(10f).Within(0.01f),
                "40 damage reduced by frame and armor once each must spend only 10 across all parts");
            Assert.That(losses.Count(x => x > 0f), Is.EqualTo(explosion ? 4 : 2));
            if (!explosion)
                Assert.That(Integrity(Part(vehicle, "armor")).Integrity, Is.EqualTo(91f).Within(0.01f));
        });
    }

    [Test]
    public async Task MinorWearKeepsFullPerformanceAndDamageSlowdownsHaveAFloor()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleGameplayChassis", map.GridCoords);
            var part = Part(vehicle, "support");
            var hardpoints = Server.System<HardpointSystem>();
            foreach (var health in new[] { 99f, 85f, 70f })
            {
                Integrity(part).Integrity = health;
                Assert.That(hardpoints.GetHardpointPerformanceMultiplier(part), Is.EqualTo(1f));
            }
            Integrity(part).Integrity = 40f;
            Assert.That(hardpoints.GetHardpointPerformanceMultiplier(part), Is.InRange(0.35f, 0.99f));
            Integrity(part).Integrity = 10f;
            Assert.That(hardpoints.GetHardpointPerformanceMultiplier(part), Is.Zero);

            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            mover.SpeedAtZeroIntegrity = 0.1f;
            Integrity(vehicle).Integrity = 1f;
            SEntMan.EnsureComponent<VehicleMechanicalFailureModifierComponent>(vehicle).ReverseSpeedMultiplier = 0.1f;
            var reverse = Invoke<float>(Server.System<GridVehicleMoverSystem>(), "GetModifiedMaxReverseSpeed", vehicle, mover);
            Assert.That(reverse, Is.EqualTo(mover.MaxReverseSpeed * 0.35f).Within(0.001f));
            mover.ImmobileUntil = SGameTiming.CurTime + TimeSpan.FromSeconds(3);
            Assert.That(Invoke<float>(Server.System<GridVehicleMoverSystem>(), "GetModifiedMaxReverseSpeed", vehicle, mover), Is.Zero,
                "the damage floor must not bypass a crash lockout");
        });
    }

    [Test]
    public async Task SmallArmsDoNotTransferIntoAnEnclosedDriver()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleHumvee", map.GridCoords);
            var driver = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var shooter = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(4, 0)));
            var vehicles = Server.System<Content.Shared.Vehicle.Systems.VehicleSystem>();
            Assert.That(vehicles.TrySetOperator((vehicle, SEntMan.GetComponent<VehicleComponent>(vehicle)), driver), Is.True);
            var damage = Server.System<DamageableSystem>();
            var driverDamage = SEntMan.GetComponent<DamageableComponent>(driver);
            var before = damage.GetPositiveDamage((driver, driverDamage)).GetTotal();
            for (var i = 0; i < 10; i++)
                damage.TryChangeDamage(vehicle, new DamageSpecifier { DamageDict = { ["Piercing"] = 30 } },
                    origin: shooter, impact: DamageImpact.Projectile);
            Assert.That(damage.GetPositiveDamage((driver, driverDamage)).GetTotal(), Is.EqualTo(before));
        });
    }

    [Test]
    public async Task RepairButtonTargetsTheSelectedInstalledModuleWithoutALoader()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default, selected = default, other = default;
        await Server.WaitAssertion(() =>
        {
            vehicle = SEntMan.SpawnEntity("VehicleGameplayChassis", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0)));
            var screwdriver = SEntMan.SpawnEntity("CMScrewdriver", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, screwdriver), Is.True);
            selected = Part(vehicle, "support");
            other = Part(vehicle, "turret");
            SEntMan.EnsureComponent<VehicleHardpointFailureComponent>(selected).ActiveFailures.Add(VehicleHardpointFailure.FeedJam);
            SEntMan.EnsureComponent<VehicleHardpointFailureComponent>(other).ActiveFailures.Add(VehicleHardpointFailure.FeedJam);
            Assert.That(Server.System<HardpointSystem>().TryRepairSelectedHardpoint(vehicle, user, "support"), Is.True);
            Assert.That(Server.System<HardpointSystem>().TryRepairSelectedHardpoint(vehicle, user, "missing"), Is.False);
        });
        await Server.WaitRunTicks(600);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<VehicleHardpointFailureComponent>(selected).RepairProgress
                .GetValueOrDefault(VehicleHardpointFailure.FeedJam), Is.EqualTo(1));
            Assert.That(SEntMan.GetComponent<VehicleHardpointFailureComponent>(other).RepairProgress, Is.Empty);
            Assert.That(Parts(vehicle), Does.Contain(selected), "servicing must leave the module installed");
        });
    }

    [TestCase("VehicleTankTreads")]
    [TestCase("VehicleTankReinforcedTreads")]
    public async Task FreshTreadsSurviveSeveralHeavyTailStabs(string prototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(vehicle, "wheel-1", out var slot), Is.True);
            if (slot.Item is { } old)
                SEntMan.DeleteEntity(old);
            var treads = SEntMan.SpawnEntity(prototype, map.GridCoords);
            Assert.That(slots.TryInsert(vehicle, slot, treads, null), Is.True);
            var ravager = SEntMan.SpawnEntity("CMXenoRavager", map.GridCoords.Offset(new Vector2(3, 0)));
            var stab = SEntMan.GetComponent<XenoTailStabComponent>(ravager).TailDamage;
            var initial = Integrity(treads).Integrity;
            for (var i = 0; i < 3; i++)
                Server.System<DamageableSystem>().TryChangeDamage(vehicle, new DamageSpecifier(stab), origin: ravager,
                    impact: DamageImpact.ForMelee(stab));
            var remaining = Integrity(treads).Integrity;
            Assert.That(remaining, Is.GreaterThan(initial * 0.7f), "three tail stabs should not cripple fresh running gear");
            Assert.That(remaining, Is.LessThan(initial), "side attacks must still reach the treads");
            TestContext.WriteLine($"{prototype}: {initial - remaining:0.##} integrity lost to three Ravager tail stabs; {remaining:0.##}/{initial} remains.");
            var hits = 3;
            while (Integrity(treads).Integrity > 0f && hits < 100)
            {
                Server.System<DamageableSystem>().TryChangeDamage(vehicle, new DamageSpecifier(stab), origin: ravager,
                    impact: DamageImpact.ForMelee(stab));
                hits++;
            }
            Assert.That(hits, Is.InRange(10, 99), "running gear must resist several hits but remain destructible");
            TestContext.WriteLine($"{prototype}: {hits} side-on Ravager tail stabs to destroy fresh treads without armor plating.");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SmashCannotPassThroughASurvivingObstacle(bool vetoDestruction)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<VehicleDestructionVetoSystem>();
            var vehicle = SEntMan.SpawnEntity("VehicleGameplayChassis", map.GridCoords);
            var obstacle = SEntMan.SpawnEntity("RMCShutterAlmayer", map.GridCoords.Offset(new Vector2(1, 0)));
            var smashable = SEntMan.GetComponent<VehicleSmashableComponent>(obstacle);
            smashable.DamageOnHit = 0;
            smashable.DeleteOnHit = vetoDestruction;
            if (vetoDestruction)
                SEntMan.EnsureComponent<VehicleDestructionVetoComponent>(obstacle);
            Assert.That(Invoke<bool>(Server.System<GridVehicleMoverSystem>(), "TrySmash", obstacle, vehicle, false, false), Is.False);
            Assert.That(SEntMan.IsQueuedForDeletion(obstacle), Is.False);
        });
    }

    [Test]
    public async Task GunnerRangeReachesExteriorHullAndResetsAfterUnsubscribe()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var original = session.AttachedEntity;
            try
            {
                var vehicle = SEntMan.SpawnEntity("VehicleGameplayChassis", map.GridCoords);
                var gunner = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(session, gunner);
                var view = SEntMan.EnsureComponent<VehicleGunnerViewUserComponent>(gunner);
                view.PvsScale = 0.35f;
                view.CursorPvsIncrease = 0.5f;
                view.CursorMaxOffset = 5f;
                Server.System<VehicleGunnerViewSystem>().RefreshView(gunner);
                var usersEye = SEntMan.GetComponent<EyeComponent>(gunner);
                var subscribers = Server.System<ViewSubscriberSystem>();
                subscribers.AddViewSubscriber(vehicle, session);
                Assert.That(SEntMan.GetComponent<EyeComponent>(vehicle).PvsScale, Is.EqualTo(usersEye.PvsScale));
                Assert.That(usersEye.PvsScale, Is.GreaterThan(1.5f));
                subscribers.RemoveViewSubscriber(vehicle, session);
                Assert.That(SEntMan.GetComponent<EyeComponent>(vehicle).PvsScale, Is.EqualTo(1f));
            }
            finally
            {
                Server.PlayerMan.SetAttachedEntity(session, original);
            }
        });
    }

    [Test]
    public async Task CannonFiresDuringTraverseAlongTheBarrel()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            var turret = Part(vehicle, "primary");
            var gun = SEntMan.SpawnEntity("VehicleTankLTBCannon", map.GridCoords);
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(turret, "turret-cannon", out var slot), Is.True);
            Assert.That(slots.TryInsert(turret, slot, gun, null), Is.True);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var parent = SEntMan.GetComponent<VehicleTurretComponent>(turret);
            parent.WorldRotation = Angle.Zero;
            parent.TargetRotation = Angle.FromDegrees(90);
            var shot = new AttemptShootEvent(user, null, map.GridCoords, map.GridCoords.Offset(new Vector2(10, 0)));
            SEntMan.EventBus.RaiseLocalEvent(gun, ref shot);
            Assert.That(shot.Cancelled, Is.False);
            var transform = Server.System<SharedTransformSystem>();
            var direction = transform.ToMapCoordinates(shot.ToCoordinates!.Value).Position -
                transform.ToMapCoordinates(shot.FromCoordinates).Position;
            var barrel = (parent.WorldRotation + transform.GetWorldRotation(vehicle)).ToWorldVec();
            Assert.That(Vector2.Dot(Vector2.Normalize(direction), barrel), Is.EqualTo(1f).Within(0.001f));
        });
    }

    [Test]
    public async Task ParkingAssistAlignsHeadingAndWheelsCannotPivotAtRest()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleGameplayChassis", map.GridCoords);
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            var grid = SEntMan.GetComponent<TransformComponent>(vehicle).GridUid!.Value;
            var gridComp = SEntMan.GetComponent<MapGridComponent>(grid);
            var transform = Server.System<SharedTransformSystem>();
            mover.AlignmentAssistDegrees = 8;
            mover.Position = map.GridCoords.Position;
            transform.SetLocalRotation(vehicle, Angle.FromDegrees(5));
            mover.CurrentSpeed = 0.5f;
            Invoke<bool>(Server.System<GridVehicleMoverSystem>(), "UpdateDynamicDriveMovement",
                vehicle, mover, grid, gridComp, 1f, 0f, 0.2f);
            Assert.That(Math.Abs(SEntMan.GetComponent<TransformComponent>(vehicle).LocalRotation.Degrees), Is.LessThan(0.01f));
            mover.CurrentSpeed = 0f;
            mover.AngularVelocityDegrees = 0f;
            mover.TurnInPlace = false;
            Invoke<bool>(Server.System<GridVehicleMoverSystem>(), "UpdateDynamicDriveMovement",
                vehicle, mover, grid, gridComp, 0f, 1f, 0.2f);
            Assert.That(mover.AngularVelocityDegrees, Is.Zero);
            mover.TurnInPlace = true;
            Invoke<bool>(Server.System<GridVehicleMoverSystem>(), "UpdateDynamicDriveMovement",
                vehicle, mover, grid, gridComp, 0f, 1f, 0.2f);
            Assert.That(mover.AngularVelocityDegrees, Is.GreaterThan(0f));
        });
    }

    [Test]
    public async Task ExteriorSpeechUsesHullBubbleAndPreservesSpeaker()
    {
        var outside = await Pair.CreateTestMap();
        var inside = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var original = session.AttachedEntity;
            try
            {
                var vehicle = SEntMan.SpawnEntity("VehicleGameplayChassis", outside.GridCoords);
                var speaker = SEntMan.SpawnEntity("CMMobHuman", inside.GridCoords);
                SEntMan.EnsureComponent<VehicleInteriorOccupantComponent>(speaker).Vehicle = vehicle;
                var listener = SEntMan.SpawnEntity("CMMobHuman", outside.GridCoords.Offset(new Vector2(1, 0)));
                Server.PlayerMan.SetAttachedEntity(session, listener);
                var recipients = new Dictionary<ICommonSession, ChatSystem.ICChatRecipientData>();
                var relay = new ExpandICChatRecipientsEvent(speaker, 7f, recipients);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, relay);
                Assert.That(recipients[session].BubbleSource, Is.EqualTo(vehicle));
                Assert.That(relay.Source, Is.EqualTo(speaker));
            }
            finally
            {
                Server.PlayerMan.SetAttachedEntity(session, original);
            }
        });
    }

    private EntityUid[] Parts(EntityUid vehicle) => Server.System<VehicleTopologySystem>().GetMountedSlots(vehicle)
        .Where(s => s.Item != null).Select(s => s.Item!.Value).ToArray();
    private EntityUid Part(EntityUid vehicle, string id) => Server.System<VehicleTopologySystem>().GetMountedSlots(vehicle)
        .Single(s => s.CompositeId == id).Item!.Value;
    private HardpointIntegrityComponent Integrity(EntityUid part) => SEntMan.GetComponent<HardpointIntegrityComponent>(part);
    private static T Invoke<T>(object system, string method, params object[] args) =>
        (T) system.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(system, args)!;
}

[RegisterComponent]
// CMU14
public sealed partial class VehicleDestructionVetoComponent : Component;

// CMU14
public sealed class VehicleDestructionVetoSystem : EntitySystem
{
    public override void Initialize() => SubscribeLocalEvent<VehicleDestructionVetoComponent, DestructionAttemptEvent>(OnDestroy);
    private void OnDestroy(Entity<VehicleDestructionVetoComponent> ent, ref DestructionAttemptEvent args) => args.Cancel();
}
