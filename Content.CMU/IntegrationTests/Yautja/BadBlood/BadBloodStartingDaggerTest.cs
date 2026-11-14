using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// STATE-303: every Bad Blood lands with one ceremonial dagger, the tool that takes scalps and flays. It rides in the
// knife slot of the greaves, which every Bad Blood kit wears, so the pack does not need a second one.
[TestFixture]
public sealed class BadBloodStartingDaggerTest
{
    private const string Dagger = "CMUYautjaCeremonialDagger";
    private const string Hunter = "CMUMobYautja";
    private const int Spawns = 6;

    [Test]
    public async Task EveryBadBloodLandsWithExactlyOneCeremonialDagger()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                // the kit is picked at random from the role's loadouts, so several of each are checked
                foreach (var prototype in new[] { BadBloodTestHelpers.LeaderPrototype, BadBloodTestHelpers.GruntPrototype })
                {
                    for (var i = 0; i < Spawns; i++)
                    {
                        var badBlood = entMan.SpawnEntity(prototype, map.GridCoords);
                        spawned.Add(badBlood);
                        Assert.That(Count(entMan, badBlood, Dagger), Is.EqualTo(1),
                            $"{prototype} lands with exactly one ceremonial dagger");
                    }
                }

                // the honourable hunter's spawn kit is a communicator and a bracer, and stays that way
                var hunter = entMan.SpawnEntity(Hunter, map.GridCoords);
                spawned.Add(hunter);
                Assert.That(Count(entMan, hunter, Dagger), Is.Zero, "an honourable hunter's starting kit is unchanged");
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    // everything the mob wears or carries, however deep it sits
    private static int Count(IEntityManager entMan, EntityUid root, string prototype)
    {
        var count = 0;
        var children = entMan.GetComponent<TransformComponent>(root).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (entMan.GetComponent<MetaDataComponent>(child).EntityPrototype?.ID == prototype)
                count++;

            count += Count(entMan, child, prototype);
        }

        return count;
    }
}
