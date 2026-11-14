using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// A clan must not be able to feed the Lodge its own hounds. A hellhound is a xeno to the trophy code, so without
// the clan-kind check a free landing hound was worth 5 Superiority Points dead.
[TestFixture]
public sealed class BadBloodClanKindTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("CMUMobYautjaBadBloodHound", false)]
    [TestCase("CMUMobYautjaHellhound", false)]
    [TestCase("CMUMobYautja", false)]
    [TestCase("CMMobHuman", true)]
    [TestCase("CMXenoDrone", true)]
    public async Task OnlyRealPreyIsWorthSuperiorityPoints(string prototype, bool worthy)
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var provenance = Server.System<BadBloodProvenanceSystem>();
                body = Server.EntMan.SpawnEntity(prototype, map.GridCoords);

                Assert.That(provenance.IsWorthyPrey(body), Is.EqualTo(worthy), $"{prototype}: worthy prey");

                // a trophy cut from it carries the same answer to the Lodge and the spit
                var trophy = Server.EntMan.SpawnEntity("CMUYautjaXenoSkullTrophy", map.GridCoords);
                provenance.StampTrophy(trophy, body);
                var origin = Server.EntMan.GetComponent<BadBloodTrophyOriginComponent>(trophy);
                Assert.That(origin.IsYautja, Is.EqualTo(provenance.IsClanKind(body)), $"{prototype}: stamp");
                Assert.That(provenance.IsClanKind(body), Is.EqualTo(!worthy), $"{prototype}: clan kind");
                Server.EntMan.DeleteEntity(trophy);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (Server.EntMan.EntityExists(body))
                    Server.EntMan.DeleteEntity(body);
            });
        }
    }
}
