using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.Punch;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

// cmss13 only slows ishuman_strict with the warrior punch and bone chips, preds take the hit unslowed
[TestFixture]
public sealed class YautjaXenoSlowParityTest : GameTest
{
    [Test]
    public async Task WarriorPunchSlowsHumansButNotYautja()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(-1, 0)));

            Punch(warrior, human);
            Punch(warrior, hunter);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<RMCSlowdownComponent>(human), Is.True,
                    "the punch should still slow a marine");
                Assert.That(SEntMan.HasComponent<RMCSlowdownComponent>(hunter), Is.False,
                    "the warrior punch slowed a pred");
            });

            // the pred can be slowed at all, so the punch check is what kept it off
            Assert.That(Server.System<RMCSlowSystem>().TrySlowdown(hunter, TimeSpan.FromSeconds(1)), Is.True);

            SEntMan.DeleteEntity(warrior);
            SEntMan.DeleteEntity(human);
            SEntMan.DeleteEntity(hunter);
        });
    }

    [TestCase("XenoBoneChipsProjectile")]
    [TestCase("XenoHedgehogSpikeProjectileSpread")]
    public async Task BoneChipsSlowHumansButNotYautja(string projectile)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);

            Hit(projectile, human);
            Hit(projectile, hunter);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<RMCSlowdownComponent>(human), Is.True,
                    "bone chips should still slow a marine");
                Assert.That(SEntMan.HasComponent<RMCSlowdownComponent>(hunter), Is.False,
                    "bone chips slowed a pred");
            });

            SEntMan.DeleteEntity(human);
            SEntMan.DeleteEntity(hunter);
        });

        void Hit(string proto, EntityUid target)
        {
            var chips = SEntMan.SpawnEntity(proto, map.GridCoords);
            var ev = new ProjectileHitEvent(new DamageSpecifier(), target);
            SEntMan.EventBus.RaiseLocalEvent(chips, ref ev);
            SEntMan.DeleteEntity(chips);
        }
    }

    private void Punch(EntityUid warrior, EntityUid target)
    {
        var ev = new XenoPunchActionEvent { Performer = warrior, Target = target };
        SEntMan.EventBus.RaiseLocalEvent(warrior, ev);
        Assert.That(ev.Handled, Is.True, "the punch never went off");
    }
}
