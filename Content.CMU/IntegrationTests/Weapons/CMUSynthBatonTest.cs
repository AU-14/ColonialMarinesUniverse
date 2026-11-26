using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.ActionBlocker;
using Content.Shared.Jittering;
using Content.Shared.Stunnable;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUSynthBatonTest : InteractionTest
{
    [Test]
    public async Task WorkingJoeOnlyFallsOnASecondHitDuringTheShake()
    {
        await AddGravity(MapData.MapUid);
        await AddAtmosphere();
        await PlaceInHands("CMStunbaton");
        var joe = ToServer(await SpawnTarget("AU14MobWorkingJoeColony"));
        await SetCombatMode(true);
        await RunSeconds(2);

        await AttemptLightAttack();
        await Server.WaitAssertion(() =>
        {
            var blocker = Server.System<ActionBlockerSystem>();
            Assert.That(SEntMan.HasComponent<JitteringComponent>(joe), Is.True);
            Assert.That(blocker.CanMove(joe), Is.True);
            Assert.That(blocker.CanAttack(joe, SPlayer), Is.True);
            Assert.That(SEntMan.HasComponent<KnockedDownComponent>(joe), Is.False);
        });

        await RunSeconds(3);
        await AttemptLightAttack();
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<KnockedDownComponent>(joe), Is.False));

        await RunSeconds(1.2f);
        await AttemptLightAttack();
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<KnockedDownComponent>(joe), Is.True));
    }
}
