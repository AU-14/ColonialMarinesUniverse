using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Emote;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.Trigger;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class LogTargetLifetimeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task DeletedCollisionTargetCannotEmote()
    {
        await Server.WaitAssertion(() =>
        {
            var target = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.DeleteEntity(target);
            Assert.DoesNotThrow(() => Server.System<RMCEmoteSystem>().TryEmoteWithChat(target, "Scream"));
        });
    }

    [Test]
    public async Task TriggerFromAnObjectDoesNotStartASnareStruggle()
    {
        await Server.WaitAssertion(() =>
        {
            var trap = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var target = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<SapperSnareComponent>(trap);
            var trigger = new TriggerEvent(target);
            SEntMan.EventBus.RaiseLocalEvent(trap, ref trigger);
            Assert.That(SEntMan.HasComponent<SapperSnaredComponent>(target), Is.False);
            SEntMan.DeleteEntity(trap);
            SEntMan.DeleteEntity(target);
        });
    }
}
