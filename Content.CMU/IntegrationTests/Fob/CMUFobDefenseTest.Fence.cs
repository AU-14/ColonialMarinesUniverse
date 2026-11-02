using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobDefenseTest
{
    [TestCase("CMMobHuman")]
    [TestCase("CMXenoDrone")]
    public async Task PoweredFenceStunsMeleeAttackersAndRespectsCooldownAndPower(string prototype)
    {
        await SetupGuns(ammo: 0);
        var fence = await DeployEquipment("CMUFobFence", new Vector2(0, 3));
        EntityUid attacker = default;
        FixedPoint2 shocked = default;
        var hit = new DamageSpecifier { DamageDict = new() { ["Blunt"] = 1 } };
        var blocker = Server.System<ActionBlockerSystem>();
        var session = ServerSession!;
        var original = session.AttachedEntity;
        try
        {
            await Server.WaitAssertion(() =>
            {
                attacker = SEntMan.SpawnAtPosition(prototype,
                    SEntMan.GetComponent<TransformComponent>(fence).Coordinates.Offset(Vector2.UnitX));
                Server.PlayerMan.SetAttachedEntity(session, attacker);
                Assert.That(blocker.CanMove(attacker), Is.True);
                Damage.TryChangeDamage(fence, hit, origin: attacker);
                Assert.That(Damage.GetTotalDamage(attacker), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(SEntMan.HasComponent<StunnedComponent>(attacker), Is.False);
                Assert.That(Reserve.TryActivate(_network), Is.True);
            });
            await RunSeconds(1);
            await Server.WaitAssertion(() =>
            {
                Damage.TryChangeDamage(fence, hit, origin: attacker);
                shocked = Damage.GetTotalDamage(attacker);
                Assert.That(shocked, Is.GreaterThan(FixedPoint2.Zero), "Powered fences must hurt humans and xenos.");
                Assert.That(SEntMan.HasComponent<StunnedComponent>(attacker), Is.True);
                Assert.That(blocker.CanMove(attacker), Is.False, "The shock must actually prevent movement.");
                Damage.TryChangeDamage(fence, hit, origin: attacker);
                Assert.That(Damage.GetTotalDamage(attacker), Is.EqualTo(shocked), "Repeated hits share the fence cooldown.");
            });
            await RunSeconds(1.2f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(blocker.CanMove(attacker), Is.True, "The brief stun must expire before another shock is available.");
                Damage.TryChangeDamage(fence, hit, origin: attacker);
                Assert.That(Damage.GetTotalDamage(attacker), Is.EqualTo(shocked));
                Assert.That(SEntMan.HasComponent<StunnedComponent>(attacker), Is.False, "Cooldown blocks another stun too.");
            });
            await RunSeconds(1);
            await Server.WaitAssertion(() =>
            {
                Damage.TryChangeDamage(fence, hit, origin: attacker);
                Assert.That(Damage.GetTotalDamage(attacker), Is.GreaterThan(shocked));
                Assert.That(blocker.CanMove(attacker), Is.False);
                shocked = Damage.GetTotalDamage(attacker);
                Assert.That(Reserve.TryDeactivate(_network), Is.True);
            });
            await RunSeconds(2.2f);
            await Server.WaitAssertion(() =>
            {
                Damage.TryChangeDamage(fence, hit, origin: attacker);
                Assert.That(Damage.GetTotalDamage(attacker), Is.EqualTo(shocked), "Power loss must disable shocks after cooldown expires.");
                Assert.That(blocker.CanMove(attacker), Is.True);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(attacker), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, original));
        }
    }
}
