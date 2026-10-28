using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Movement;
using Content.Shared._RMC14.Stealth;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
[TestOf(typeof(YautjaCloakSystem))]
public sealed class YautjaFootstepVolumeTest : GameTest
{
    [Test]
    public async Task CloakedYautjaStepQuieterThanUncloaked()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var pred = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var yautja = SEntMan.GetComponent<YautjaComponent>(pred);

            var uncloaked = new GetMobFootstepVolumeEvent();
            SEntMan.EventBus.RaiseLocalEvent(pred, ref uncloaked);

            SEntMan.EnsureComponent<EntityTurnInvisibleComponent>(pred).Enabled = true;
            var cloaked = new GetMobFootstepVolumeEvent();
            SEntMan.EventBus.RaiseLocalEvent(pred, ref cloaked);

            Assert.Multiple(() =>
            {
                Assert.That(uncloaked.Modifier, Is.EqualTo(yautja.FootstepVolume));
                Assert.That(cloaked.Modifier, Is.EqualTo(yautja.CloakedFootstepVolume));
                Assert.That(uncloaked.Modifier, Is.LessThan(0f));
                Assert.That(cloaked.Modifier, Is.LessThan(uncloaked.Modifier));
            });

            SEntMan.DeleteEntity(pred);
        });
    }
}
