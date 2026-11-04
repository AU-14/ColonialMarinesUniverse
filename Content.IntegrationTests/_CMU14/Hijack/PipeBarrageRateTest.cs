using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Hijack;
using Content.Shared._RMC14.Hijack;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Hijack;

[TestFixture]
public sealed class PipeBarrageRateTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task PipeBarrageSchedulesTheNextWaveAfterThirtySeconds()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var pipe = SEntMan.SpawnEntity("GasPipeStraight", map.GridCoords);
            var active = SEntMan.EnsureComponent<RMCHijackActiveMapComponent>(map.MapUid);
            active.Pipes.Add(pipe);
            active.Next = TimeSpan.Zero;
            Server.System<RMCHijackRandomDamageSystem>().Update(0);
            Assert.That(active.Explode, Does.Contain(pipe), "The current wave must still warn its selected pipes.");
            Assert.That(active.Next, Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromSeconds(30)),
                "Recurring waves must run at half the previous 15-second frequency.");
            Assert.That(active.ExplodeAt, Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromSeconds(5)),
                "The warning time before each explosion must remain unchanged.");
        });
    }
}
