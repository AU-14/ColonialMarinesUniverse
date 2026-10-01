using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Blocking;
using Content.Shared.Blocking.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Audio.Components;

namespace Content.IntegrationTests.CMU14;

[TestFixture]
public sealed class CMUShieldDamageSoundTest : GameTest
{
    [Test]
    public async Task UnsupportedDamageDoesNotPlayShieldImpactButPhysicalHitDoes()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var shield = SEntMan.SpawnEntity("AU14BallisticShieldRMC", map.GridCoords);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, shield), Is.True);
            Assert.That(SEntMan.System<BlockingSystem>().RaiseShield((shield, SEntMan.GetComponent<BlockingComponent>(shield)), user), Is.True);
            var damage = SEntMan.System<DamageableSystem>();
            var sounds = SEntMan.EntityQuery<AudioComponent>().Count();
            var shieldDamage = damage.GetTotalDamage(shield);
            damage.TryChangeDamage(user, new DamageSpecifier { DamageDict = { ["Bloodloss"] = 5 } });
            Assert.That(damage.GetTotalDamage(shield), Is.EqualTo(shieldDamage));
            Assert.That(SEntMan.EntityQuery<AudioComponent>().Count(), Is.EqualTo(sounds),
                "Damage unsupported by the shield must not sound like an impact.");

            damage.TryChangeDamage(user, new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } }, impact: DamageImpact.MeleeSlash);
            Assert.That(damage.GetTotalDamage(shield), Is.GreaterThan(shieldDamage));
            Assert.That(SEntMan.EntityQuery<AudioComponent>().Count(), Is.GreaterThan(sounds));
        });
    }
}
