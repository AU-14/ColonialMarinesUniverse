using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.DroneOperator;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUFlamerFuelHazardTest : GameTest
{
    [TestCase("RMCNapalmB", true)]
    [TestCase("RMCNapalmX", false)]
    [TestCase("RMCNapalmEX", false)]
    [TestCase("RMCR189", true)]
    [TestCase("RMCCLF3", false)]
    public async Task AlternateFuelRupturesOnceAndPreventsWreckRepair(string reagent, bool delayed)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (drone, _, solution) = Load(map.GridCoords, reagent);
            var damage = SEntMan.System<DamageableSystem>();
            damage.TryChangeDamage(drone, new DamageSpecifier { DamageDict = { ["Blunt"] = 400 } }, ignoreResistances: true);
            var fuel = SEntMan.GetComponent<CMUFlamerDroneFuelComponent>(drone);
            Assert.That(fuel.Ruined, Is.True);
            Assert.That(solution.Comp.Solution.Volume.Float(), Is.Zero, "The rupture must consume the actual remaining fuel.");
            Assert.That(SEntMan.EntityQuery<RMCIgniteOnCollideComponent>().Any(), Is.True, "A rupture must create a real fire hazard.");
            Assert.That(SEntMan.EntityQuery<CMUFlamerFuelCookoffComponent>().Count(), Is.EqualTo(delayed ? 1 : 0));

            var repeat = new CMUCombatDroneWreckedEvent();
            SEntMan.EventBus.RaiseLocalEvent(drone, ref repeat);
            Assert.That(SEntMan.EntityQuery<CMUFlamerFuelCookoffComponent>().Count(), Is.EqualTo(delayed ? 1 : 0));

            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(6, 0)));
            var before = damage.GetTotalDamage(drone);
            var repair = new CMUCombatDroneWeldDoAfterEvent();
            repair.DoAfter = new DoAfter(0, new DoAfterArgs(SEntMan, user, TimeSpan.Zero, repair, drone, drone), SGameTiming.CurTime);
            SEntMan.EventBus.RaiseLocalEvent(drone, repair);
            Assert.That(damage.GetTotalDamage(drone), Is.EqualTo(before));
            Assert.That(SEntMan.GetComponent<CMUCombatDroneComponent>(drone).Wrecked, Is.True);
        });
        if (delayed)
        {
            await Pair.RunSeconds(5);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.EntityQuery<CMUFlamerFuelCookoffComponent>(), Is.Empty));
        }
    }

    [Test]
    public async Task StockFuelWreckDoesNotRuptureAndCanBeRepaired()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (drone, tank, solution) = Load(map.GridCoords, "RMCNapalmUT");
            var damage = SEntMan.System<DamageableSystem>();
            damage.TryChangeDamage(drone, new DamageSpecifier { DamageDict = { ["Blunt"] = 400 } }, ignoreResistances: true);
            Assert.That(SEntMan.GetComponent<CMUFlamerDroneFuelComponent>(drone).Ruined, Is.False);
            Assert.That(solution.Comp.Solution.Volume.Float(), Is.EqualTo(200));
            Assert.That(SEntMan.EntityExists(tank), Is.True);
            Assert.That(SEntMan.EntityQuery<CMUFlamerFuelCookoffComponent>(), Is.Empty);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var before = damage.GetTotalDamage(drone);
            var repair = new CMUCombatDroneWeldDoAfterEvent();
            repair.DoAfter = new DoAfter(0, new DoAfterArgs(SEntMan, user, TimeSpan.Zero, repair, drone, drone), SGameTiming.CurTime);
            SEntMan.EventBus.RaiseLocalEvent(drone, repair);
            Assert.That(damage.GetTotalDamage(drone), Is.LessThan(before));
        });
    }

    [Test]
    public async Task MixedFuelCombinesPenaltiesAndRefillClearsOnlyTemporaryEffects()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (drone, _, solution) = Load(map.GridCoords, "RMCNapalmB", 100);
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryAddReagent(solution, "RMCCLF3", 100), Is.True);
            var speed = new RefreshMovementSpeedModifiersEvent();
            SEntMan.EventBus.RaiseLocalEvent(drone, speed);
            Assert.That(speed.WalkSpeedModifier, Is.LessThan(1));
            var fuel = SEntMan.GetComponent<CMUFlamerDroneFuelComponent>(drone);
            Assert.That(fuel.RepairEfficiency, Is.LessThan(1));
            var repairEfficiency = fuel.RepairEfficiency;
            SEntMan.System<CMUFlamerDroneHazardSystem>().Update(0);
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(drone).Float(), Is.GreaterThan(0));
            solutions.RemoveAllSolution(solution);
            Assert.That(solutions.TryAddReagent(solution, "RMCNapalmUT", 200), Is.True);
            speed = new RefreshMovementSpeedModifiersEvent();
            SEntMan.EventBus.RaiseLocalEvent(drone, speed);
            Assert.That(speed.WalkSpeedModifier, Is.EqualTo(1));
            Assert.That(fuel.SpecialFraction, Is.Zero);
            Assert.That(fuel.RepairEfficiency, Is.EqualTo(repairEfficiency), "Changing tanks cannot reverse corrosion.");
        });
    }

    [TestCase("RMCNapalmX")]
    [TestCase("RMCR189")]
    public async Task FiringAlternateFuelDamagesTheDroneOrWastesExtraFuel(string reagent)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (drone, _, solution) = Load(map.GridCoords, reagent);
            var takeAmmo = new TakeAmmoEvent(1, [], map.GridCoords, drone);
            SEntMan.EventBus.RaiseLocalEvent(drone, takeAmmo);
            if (reagent == "RMCNapalmX")
                SEntMan.System<SharedSolutionContainerSystem>().RemoveAllSolution(solution);
            var shot = new GunShotEvent(drone, [], map.GridCoords, map.GridCoords.Offset(Vector2.UnitY));
            SEntMan.EventBus.RaiseLocalEvent(drone, ref shot);
            if (reagent == "RMCNapalmX")
            {
                Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(drone).Float(), Is.GreaterThan(0));
                var attempt = new AttemptShootEvent(drone, null, map.GridCoords, map.GridCoords.Offset(Vector2.UnitY));
                SEntMan.EventBus.RaiseLocalEvent(drone, ref attempt);
                Assert.That(attempt.Cancelled, Is.True);
            }
            else
                Assert.That(solution.Comp.Solution.Volume.Float(), Is.LessThan(200));
        });
    }

    private (EntityUid Drone, EntityUid Tank, Entity<SolutionComponent> Solution) Load(EntityCoordinates coordinates, string reagent, int volume = 200)
    {
        var drone = SEntMan.SpawnEntity("CMUFlamerDrone", coordinates);
        var tank = SEntMan.SpawnEntity("CMUFlamerDroneFuelTank", coordinates);
        var solutions = SEntMan.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetSolution(tank, SEntMan.GetComponent<RMCFlamerTankComponent>(tank).SolutionId, out var solution, out _), Is.True);
        solutions.RemoveAllSolution(solution!.Value);
        Assert.That(solutions.TryAddReagent(solution.Value, reagent, volume), Is.True);
        var containers = SEntMan.System<SharedContainerSystem>();
        Assert.That(containers.Insert(tank, containers.EnsureContainer<ContainerSlot>(drone, SharedGunSystem.MagazineSlot)), Is.True);
        return (drone, tank, solution.Value);
    }
}
