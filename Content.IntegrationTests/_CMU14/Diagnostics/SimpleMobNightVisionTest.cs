using Content.Shared._RMC14.NightVision;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class SimpleMobNightVisionTest
{
    [Test]
    public async Task SmallHostsAndSimpleAnimalsReachTheNightVisionRenderQuery()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var mobs = new Dictionary<string, NetEntity>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var observer = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, observer);
            foreach (var prototype in new[] { "CMMobSmallHostMonkey", "CMMobSmallHostKobold", "RMCMobCat" })
                mobs[prototype] = entities.GetNetEntity(entities.SpawnEntity(prototype, map.GridCoords));
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var rendered = new HashSet<EntityUid>();
            var query = entities.EntityQueryEnumerator<RMCNightVisionVisibleComponent, SpriteComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out _, out _))
                rendered.Add(uid);
            foreach (var (prototype, net) in mobs)
                Assert.That(rendered, Does.Contain(entities.GetEntity(net)), $"{prototype} is missing from the night-vision overlay's query.");
        });
        await pair.CleanReturnAsync();
    }
}
