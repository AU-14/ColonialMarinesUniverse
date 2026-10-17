using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Gravity;
using Content.Server.Light.EntitySystems;
using Content.Server.Stunnable;
using Content.Shared._RMC14.Tackle;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Stun;

[TestFixture]
[NonParallelizable]
public sealed class RMCTackleRecoveryTest : GameTest
{
    [TestCase(true, 3f)]
    [TestCase(false, 3f)]
    [TestCase(true, 5f)]
    public async Task PulledTargetCanBeTackledAtRmcDisarmRange(bool pulling, float distance)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var xeno = SSpawnAtPosition("CMXenoRunner", map.GridCoords);
            var target = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            Server.System<SharedCombatModeSystem>().SetInCombatMode(xeno, true);
            if (pulling)
                Assert.That(Server.System<PullingSystem>().TryStartPull(xeno, target), Is.True);
            Server.System<SharedTransformSystem>().SetCoordinates(target, map.GridCoords.Offset(new Vector2(distance, 0)));
            var melee = Server.System<SharedMeleeWeaponSystem>();
            Assert.That(melee.TryGetWeapon(xeno, out var weapon, out var comp), Is.True);
            var range = Server.System<SharedRMCMeleeWeaponSystem>().RMCGetUserDisarmRange(xeno, target, comp);
            Assert.That(range, Is.EqualTo(pulling ? 4f : comp.Range));
            Assert.That(melee.AttemptDisarmAttack(xeno, weapon, comp, target), Is.True);
            Assert.That(SEntMan.HasComponent<TackledRecentlyByComponent>(target), Is.EqualTo(pulling && distance < 4),
                "the server must accept the same pull-target tackle range advertised to the client");
        });
    }

    [Test]
    public async Task AlwaysPoweredFixtureBreaksOnXenoAttack()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var xeno = SSpawnAtPosition("CMXenoRunner", map.GridCoords);
            var fixture = SSpawnAtPosition("RMCLightFixtureAlwaysPowered", map.GridCoords);
            var lights = Server.System<SharedPointLightSystem>();
            Assert.That(lights.TryGetLight(fixture, out var light), Is.True);
            Assert.That(light.Enabled, Is.True);
            var melee = Server.System<SharedMeleeWeaponSystem>();
            Assert.That(melee.TryGetWeapon(xeno, out var weapon, out var comp), Is.True);
            Assert.That(melee.AttemptLightAttack(xeno, weapon, comp, fixture, requireCombatMode: false), Is.True);
            Assert.That(light.Enabled, Is.False, "always-powered RMC lights must also break on attack");
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DamageDoesNotExtendRmcParalysisRecovery(bool paralysis)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        await Server.WaitPost(() => target = SSpawnAtPosition("CMMobHuman", map.GridCoords));
        // RMC removes the input mover from unoccupied mobs after map initialization.
        await Pair.RunTicksSync(2);
        try
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, target));
            await Pair.RunTicksSync(3);
            await Server.WaitAssertion(() =>
            {
                var stun = Server.System<StunSystem>();
                Assert.That(paralysis
                    ? stun.TryParalyze(target, TimeSpan.FromSeconds(1), true)
                    : stun.TryKnockdown(target, TimeSpan.FromSeconds(0.1)), Is.True);
            });
            await Pair.RunSeconds(0.8f);
            await Server.WaitAssertion(() =>
            {
                if (!paralysis)
                    Assert.That(SEntMan.GetComponent<KnockedDownComponent>(target).DoAfterId, Is.Not.Null,
                        "the hit must occur during the stand-up action");
                var damage = new DamageSpecifier { DamageDict = new() { ["Blunt"] = FixedPoint2.New(6) } };
                Assert.That(Server.System<DamageableSystem>().TryChangeDamage(target, damage, ignoreResistances: true), Is.Not.Null);
            });
            await Pair.RunSeconds(0.4f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.False,
                    "damage must neither extend paralysis recovery nor cancel the stand-up action");
                Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }
    }

    [TestCase("CMMobHuman")]
    [TestCase("CMMobSmallHostMonkey")]
    public async Task DisarmAttackReachesTackle(string targetPrototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid xeno = default;
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            Server.System<GravitySystem>().EnableGravity(map.MapUid,
                SEntMan.EnsureComponent<GravityComponent>(map.MapUid));
            xeno = SSpawnAtPosition("CMXenoRunner", map.GridCoords);
            target = SSpawnAtPosition(targetPrototype, map.GridCoords);
            Server.System<SharedCombatModeSystem>().SetInCombatMode(xeno, true);
        });
        var tackled = false;
        for (var i = 0; i < 6 && !tackled; i++)
        {
            await Server.WaitAssertion(() =>
            {
                var melee = Server.System<SharedMeleeWeaponSystem>();
                Assert.That(melee.TryGetWeapon(xeno, out var weapon, out var comp), Is.True);
                Assert.That(weapon, Is.EqualTo(xeno), "unarmed xeno attacks must retain their tackle input");
                Assert.That(comp.AltDisarm, Is.True);
                Assert.That(melee.AttemptDisarmAttack(xeno, weapon, comp, target), Is.True);
                tackled = SEntMan.HasComponent<StunnedComponent>(target);
            });
            await Pair.RunSeconds(1);
        }
        Assert.That(tackled, Is.True, "six normal disarm attacks must tackle the target");
    }

    [TestCase("CMMobHuman")]
    [TestCase("CMMobSmallHostMonkey")]
    public async Task RunnerLeapKnocksDownTarget(string targetPrototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            Server.System<GravitySystem>().EnableGravity(map.MapUid,
                SEntMan.EnsureComponent<GravityComponent>(map.MapUid));
            var xeno = SSpawnAtPosition("CMXenoRunner", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            target = SSpawnAtPosition(targetPrototype, map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            var leap = new XenoLeapDoAfterEvent(SEntMan.GetNetCoordinates(map.GridCoords.Offset(new Vector2(2.5f, 0.5f))));
            leap.DoAfter = new DoAfter(0,
                new DoAfterArgs(SEntMan, xeno, TimeSpan.Zero, leap, xeno), TimeSpan.Zero);
            SEntMan.EventBus.RaiseLocalEvent(xeno, leap);
            Assert.That(SEntMan.HasComponent<XenoLeapingComponent>(xeno), Is.True);
        });
        await Pair.RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<LeapIncapacitatedComponent>(target), Is.True);
            Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
            Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.True);
        });
    }

    [TestCase("CMXenoRunner")]
    [TestCase("CMXenoDrone")]
    public async Task XenoBreaksLightFixture(string caste)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var xeno = SSpawnAtPosition(caste, map.GridCoords);
            var fixture = SSpawnAtPosition("RMCLightFixture", map.GridCoords);
            var light = Server.System<PoweredLightSystem>();
            var bulb = light.GetBulb(fixture);
            Assert.That(bulb, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<LightBulbComponent>(bulb.Value).State, Is.EqualTo(LightBulbState.Normal));
            var melee = Server.System<SharedMeleeWeaponSystem>();
            Assert.That(melee.TryGetWeapon(xeno, out var weapon, out var weaponComp), Is.True);
            Assert.That(melee.AttemptLightAttack(xeno, weapon, weaponComp, fixture, requireCombatMode: false), Is.True);
            Assert.That(SEntMan.GetComponent<LightBulbComponent>(bulb.Value).State, Is.EqualTo(LightBulbState.Broken));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task HumanIsParalyzedAndAutomaticallyRecovers(bool tackle)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            target = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            if (tackle)
            {
                var xeno = SSpawnAtPosition("CMXenoRunner", map.GridCoords);
                // Six attempts guarantee success regardless of the tackle roll.
                for (var i = 0; i < 6 && !SEntMan.HasComponent<StunnedComponent>(target); i++)
                {
                    var ev = new CMDisarmEvent(xeno);
                    SEntMan.EventBus.RaiseLocalEvent(target, ref ev);
                    Assert.That(ev.Handled, Is.True);
                }
                SEntMan.DeleteEntity(xeno);
            }
            else
            {
                Assert.That(Server.System<StunSystem>().TryParalyze(target, TimeSpan.FromSeconds(1), true), Is.True);
            }

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True);
                Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.True);
            });
        });

        await Pair.RunSeconds(9);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.False);
                Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.False);
            });
        });
    }
}
