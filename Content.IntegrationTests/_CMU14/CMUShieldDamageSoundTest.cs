using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Blocking;
using Content.Shared.Blocking.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;

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
            var blocking = SEntMan.GetComponent<BlockingComponent>(shield);
            var audio = Server.System<SharedAudioSystem>();
            var impactSound = audio.GetAudioPath(audio.ResolveSound(blocking.BlockSound));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, shield), Is.True);
            Assert.That(SEntMan.System<BlockingSystem>().RaiseShield((shield, blocking), user), Is.True);
            var damage = SEntMan.System<DamageableSystem>();
            var sounds = ShieldImpactCount();
            var shieldDamage = damage.GetTotalDamage(shield);
            damage.TryChangeDamage(user, new DamageSpecifier { DamageDict = { ["Bloodloss"] = 5 } });
            Assert.That(damage.GetTotalDamage(shield), Is.EqualTo(shieldDamage));
            Assert.That(ShieldImpactCount(), Is.EqualTo(sounds),
                "Damage unsupported by the shield must not sound like an impact.");

            damage.TryChangeDamage(user, new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } }, impact: DamageImpact.MeleeSlash);
            Assert.That(damage.GetTotalDamage(shield), Is.GreaterThan(shieldDamage));
            Assert.That(ShieldImpactCount(), Is.GreaterThan(sounds));

            int ShieldImpactCount() => SEntMan.EntityQuery<AudioComponent>().Count(audio => audio.FileName == impactSound);
        });
    }
}
