#pragma warning disable RA0002 // Exercise real projectile hits, armor, shields and delayed explosions.

using System.Numerics;
using Content.Shared._RMC14.Shields;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobDefenseTest
{
    private async Task<EntityUid> PrepareWarheadTarget(string prototype)
    {
        await SetupGuns();
        NetCoordinates position = default;
        await Server.WaitPost(() => position = SEntMan.GetNetCoordinates(_gunCoords.Offset(new Vector2(20, 0))));
        await SetTile(Plating, position);
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetCoordinates(position);
            var map = Server.System<SharedMapSystem>();
            Assert.That(map.TryFindGridAt(Transform.ToMapCoordinates(coords), out var ground, out var grid), Is.True);
            coords = Transform.WithEntityId(coords, ground);
            for (var x = -3; x <= 3; x++)
                for (var y = -3; y <= 3; y++)
                    map.SetTile(ground, grid, coords.Offset(new Vector2(x, y)), new Tile(TileMan[Plating].TileId));
            target = SEntMan.SpawnAtPosition(prototype, coords);
            Transform.SetWorldRotation(target, Angle.Zero);
            Transform.SetCoordinates(SPlayer, coords.Offset(new Vector2(3, 0)));
        });
        return target;
    }

    private EntityUid HitWithFobRocket(EntityUid target)
    {
        var rocket = SEntMan.SpawnAtPosition("CMUFob72mmProjectile", SEntMan.GetComponent<TransformComponent>(target).Coordinates);
        var projectile = SEntMan.GetComponent<ProjectileComponent>(rocket);
        var projectiles = Server.System<SharedProjectileSystem>();
        projectiles.SetShooter(rocket, projectile, SPlayer);
        projectiles.ProjectileCollide((rocket, projectile, SEntMan.GetComponent<PhysicsComponent>(rocket)), target);
        Server.System<TriggerSystem>().Trigger(rocket, SPlayer);
        return rocket;
    }

    [TestCase("RMCXenoBoiler")]
    [TestCase("RMCXenoCrusher")]
    [TestCase("RMCXenoCrusherCharger")]
    public async Task RocketDirectHitBarelyCritsHealthyT3IncludingItsDelayedBlast(string prototype)
    {
        var target = await PrepareWarheadTarget(prototype);
        EntityUid rocket = default;
        await Server.WaitAssertion(() => rocket = HitWithFobRocket(target));
        await RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            var critical = Server.System<MobThresholdSystem>().GetThresholdForState(target, MobState.Critical);
            var damage = Damage.GetTotalDamage(target);
            Assert.That(damage.Float(), Is.GreaterThan(critical.Float()).And.LessThan(critical.Float() + 20),
                "One direct hit and its blast should leave a healthy T3 just beyond critical, across passive armor and health differences.");
            Assert.That(SEntMan.GetComponent<MobStateComponent>(target).CurrentState, Is.EqualTo(MobState.Critical));
            Assert.That(SEntMan.Deleted(rocket), Is.True, "The primary damage budget must survive projectile deletion until the blast resolves.");
        });
    }

    [Test]
    public async Task RocketDamageLimitDoesNotProtectAgainstOtherAttacksOrAnotherRocket()
    {
        var target = await PrepareWarheadTarget("RMCXenoCrusher");
        MobStateComponent state = default!;
        await Server.WaitAssertion(() => HitWithFobRocket(target));
        await RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            var before = Damage.GetTotalDamage(target);
            Hurt(target, 20);
            Assert.That(Damage.GetTotalDamage(target), Is.EqualTo(before + 20), "A rocket's exhausted allowance cannot block another source of damage.");
            state = SEntMan.GetComponent<MobStateComponent>(target);
            HitWithFobRocket(target);
        });
        await RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(state.CurrentState,
            Is.EqualTo(MobState.Dead), "An already wounded T3 can die, and a new rocket has its own allowance."));
    }

    [Test]
    public async Task RocketCriticalLimitPreservesActiveShieldsAndNormalSplash()
    {
        var target = await PrepareWarheadTarget("RMCXenoCrusher");
        EntityUid nearby = default;
        await Server.WaitAssertion(() =>
        {
            nearby = SEntMan.SpawnAtPosition("RMCXenoBoiler", SEntMan.GetComponent<TransformComponent>(target).Coordinates.Offset(Vector2.UnitY));
            Assert.That(Server.System<XenoShieldSystem>().ApplyShield(target, XenoShieldSystem.ShieldType.Generic, 150), Is.True);
            HitWithFobRocket(target);
        });
        await RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            var critical = Server.System<MobThresholdSystem>().GetThresholdForState(target, MobState.Critical);
            Assert.That(Damage.GetTotalDamage(target).Float(), Is.GreaterThan(0).And.LessThan(critical.Float()),
                "The crit limit must not normalize away active defenses or reapply shield-absorbed damage as blast damage.");
            Assert.That(SEntMan.GetComponent<MobStateComponent>(target).CurrentState, Is.EqualTo(MobState.Alive));
            var nearbyCritical = Server.System<MobThresholdSystem>().GetThresholdForState(nearby, MobState.Critical);
            Assert.That(Damage.GetTotalDamage(nearby).Float(), Is.GreaterThan(0).And.LessThan(nearbyCritical.Float()),
                "Nearby xenos receive normal splash, without the primary target's payload or damage limit.");
        });
    }
}
