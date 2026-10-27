using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;

namespace Content.IntegrationTests.CMU14.TacticalMap;

[TestFixture]
[TestOf(typeof(CMUReconstructionCacheSystem))]
public sealed class CMUReconstructionCacheIdentityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task CachedPersonalSurveyIsDiscardedWhenAnyFactionAccessChanges()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netActor = default;
        await Server.WaitPost(() =>
        {
            var actor = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<TacticalMapUserComponent>(actor);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, actor);
            netActor = SEntMan.GetNetEntity(actor);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var actor = CEntMan.GetEntity(netActor);
            var user = CEntMan.GetComponent<TacticalMapUserComponent>(actor);
            var cache = CEntMan.System<CMUReconstructionCacheSystem>();
            var scene = new CMUReconSnapshotMessage(1, Vector2i.Zero, 0, 1, [], [], false, 32, 32)
            {
                LoadedChunks = 1,
                TotalChunks = 1,
            };
            var unchanged = new CMUReconRenderData();
            cache.Remember(actor, actor, scene, default, unchanged);
            Assert.That(cache.TryTake(actor, actor, out var restored, out _, out var render), Is.True);
            Assert.That(restored, Is.SameAs(scene));
            Assert.That(render, Is.SameAs(unchanged), "unchanged access must reuse its rendering resources");
            render.Dispose();

            // Simulate each replicated permission changing before the next window opens.
            foreach (var flag in new[]
                     {
                         nameof(user.Marines), nameof(user.Xenos), nameof(user.Govfor), nameof(user.Opfor),
                         nameof(user.Clf), nameof(user.WeYu), nameof(user.Abomination), nameof(user.Yautja),
                     })
            {
                var field = typeof(TacticalMapUserComponent).GetField(flag)!;
                var original = (bool) field.GetValue(user)!;
                var previous = new CMUReconRenderData();
                cache.Remember(actor, actor, scene, default, previous);
                field.SetValue(user, !original);
                Assert.That(cache.TryTake(actor, actor, out _, out _, out _), Is.False,
                    $"changing {flag} must invalidate the old faction view");
                Assert.That(previous.Disposed, Is.True, "invalidated resources must be released");
                field.SetValue(user, original);
            }
        });
    }
}
