using Content.IntegrationTests.Pair;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.ActionBlocker;

namespace Content.IntegrationTests._RMC14.Xenonids;

[TestFixture, TestOf(typeof(QueenEyeSystem))]
public sealed class QueenEyeRestingTest
{
    [Test]
    public async Task RestingQueenCanMoveEyeButNotBody()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var queen = entMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            var blocker = entMan.System<ActionBlockerSystem>();
            entMan.AddComponent<XenoRestingComponent>(queen);

            Assert.That(blocker.UpdateCanMove(queen), Is.False);

            var eyeAction = new QueenEyeActionEvent
            {
                Performer = queen,
            };
            entMan.EventBus.RaiseLocalEvent(queen, eyeAction);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<QueenEyeActionComponent>(queen).Eye, Is.Not.Null);
                Assert.That(blocker.CanMove(queen), Is.True);
            });

            entMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent
            {
                Performer = queen,
            });

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<QueenEyeActionComponent>(queen).Eye, Is.Null);
                Assert.That(blocker.CanMove(queen), Is.False);
            });

            entMan.DeleteEntity(queen);
        });

        await pair.CleanReturnAsync();
    }
}
