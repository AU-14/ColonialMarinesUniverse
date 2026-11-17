using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.TacticalMap;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.TacticalMap;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// the Lodge sits in the Yautja bucket, so a marine tactical map console shows it only while it is revealed. marine
// consoles start with no faction, and an unassigned console used to show the whole Yautja bucket: the Lodge stayed on
// it before, during and after every reveal
[TestFixture]
public sealed class BadBloodLodgeMapRevealTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

#pragma warning disable RA0002 // reads the published console feed and the map's buckets directly
    [Test]
    public async Task ConsolesShowTheLodgeOnlyWhileItIsRevealed()
    {
        var map = await Pair.CreateTestMap();

        // the tracker files blips only on a grid with a tactical map, and only for entities standing on it.
        // CreateTestMap lays a single tile at (0,0): an anchored Lodge spawned off-tile is detached to the map and
        // never tracked, so floor everything this test spawns on
        await Server.WaitPost(() =>
        {
            Server.EntMan.EnsureComponent<TacticalMapComponent>(map.Grid);
            var maps = Server.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            for (var y = -1; y <= 4; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
        });

        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        EntityUid lodge = default;
        EntityUid unassigned = default;
        EntityUid marine = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                lodge = entMan.SpawnEntity(Lodge, map.GridCoords.Offset(new Vector2(0, 3)));

                // the default marine CIC console, map table or command tablet: no faction until someone draws on it
                unassigned = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(-2, 0)));
                entMan.EnsureComponent<TacticalMapComputerComponent>(unassigned);

                marine = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(-3, 0)));
                var marineComputer = entMan.EnsureComponent<TacticalMapComputerComponent>(marine);
                Server.System<TacticalMapSystem>().SetComputerFaction((marine, marineComputer), "MARINES");
            });

            // let the tracker pick the Lodge up
            await Pair.RunSeconds(1);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var tactical = Server.System<TacticalMapSystem>();
                var camp = Server.System<BadBloodCampSystem>();

                Assert.That(Server.System<BadBloodCampRuleSystem>().GetLodge(), Is.EqualTo(lodge));
                Assert.That(tactical.TryGetTacticalMap(out var live) && live.Owner == map.Grid.Owner, Is.True,
                    "the consoles read this test's tactical map");
                Assert.That(live.Comp.YautjaBlips.ContainsKey(lodge.Id), Is.True, "the Lodge is tracked in the Yautja bucket");

                Assert.That(camp.IsLodgeRevealed(), Is.False);
                Assert.That(entMan.HasComponent<TacticalMapAlwaysVisibleComponent>(lodge), Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(Feed(unassigned).ContainsKey(lodge.Id), Is.False,
                        "an unassigned console does not show a hidden Lodge");
                    Assert.That(Feed(marine).ContainsKey(lodge.Id), Is.False,
                        "a marine console does not show a hidden Lodge");
                });

                Assert.That(camp.RevealLodge(TimeSpan.FromSeconds(5), BadBloodRevealSource.TotemFell), Is.True);
                Assert.That(entMan.HasComponent<TacticalMapAlwaysVisibleComponent>(lodge), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(Feed(unassigned).ContainsKey(lodge.Id), Is.True,
                        "an unassigned console shows the revealed Lodge");
                    Assert.That(Feed(marine).ContainsKey(lodge.Id), Is.True,
                        "a marine console shows the revealed Lodge");
                });
            });

            await Pair.RunSeconds(7);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(Server.System<BadBloodCampSystem>().IsLodgeRevealed(), Is.False, "the reveal has run out");
                Assert.That(entMan.HasComponent<TacticalMapAlwaysVisibleComponent>(lodge), Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(Feed(unassigned).ContainsKey(lodge.Id), Is.False,
                        "the Lodge leaves an unassigned console when the reveal ends");
                    Assert.That(Feed(marine).ContainsKey(lodge.Id), Is.False,
                        "the Lodge leaves a marine console when the reveal ends");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                foreach (var uid in party.All.Append(lodge).Append(unassigned).Append(marine))
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }
    }

    // what the console publishes after a refresh. call on the server thread
    private Dictionary<int, TacticalMapBlip> Feed(EntityUid console)
    {
        var computer = Server.EntMan.GetComponent<TacticalMapComputerComponent>(console);
        Server.System<TacticalMapSystem>().RefreshReconstructionContacts((console, computer));
        return computer.Blips;
    }
#pragma warning restore RA0002
}
