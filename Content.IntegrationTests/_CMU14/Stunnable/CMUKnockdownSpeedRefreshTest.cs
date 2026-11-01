using Content.Server.Stunnable;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Stunnable;

// BUG-660: getting up from paralysis left the crawl slow baked into the mover until you
// lay down and stood up again
[TestFixture]
public sealed class CMUKnockdownSpeedRefreshTest
{
    [Test]
    public async Task RecoveringFromParalysisDoesNotKeepTheCrawlSlow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        EntityUid target = default;

        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                target = em.SpawnEntity("CMMobHuman", map.GridCoords);
                Assert.That(em.System<StunSystem>().TryParalyze(target, TimeSpan.FromSeconds(1), true), Is.True);
                Assert.That(em.HasComponent<KnockedDownComponent>(target), Is.True);
            });

            await pair.RunSeconds(9);

            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(em.HasComponent<KnockedDownComponent>(target), Is.False);
                Assert.That(em.System<StandingStateSystem>().IsDown(target), Is.False);

                var mover = em.GetComponent<MovementSpeedModifierComponent>(target);
                var walk = mover.WalkSpeedModifier;
                var sprint = mover.SprintSpeedModifier;

                // compare against a fresh refresh. the stale bug leaves ~0.4x (crawler speed), the
                // loose bound just stops slow medical drift (lungs in vacuum) from flaking this
                em.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(target);
                Assert.Multiple(() =>
                {
                    Assert.That(walk, Is.GreaterThan(mover.WalkSpeedModifier * 0.75f),
                        "stale knockdown slow left on the walk modifier after standing up");
                    Assert.That(sprint, Is.GreaterThan(mover.SprintSpeedModifier * 0.75f),
                        "stale knockdown slow left on the sprint modifier after standing up");
                });
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => em.DeleteEntity(target));
        }

        await pair.CleanReturnAsync();
    }
}
