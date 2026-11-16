using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Yautja.BadBlood;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-506: a marine reads a desecrated Totem, with no Bad Blood near, once per desecration, for the Lodge's
// 10-tile grid square and the clan's ship progress
[TestFixture]
public sealed class BadBloodTotemIntelTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Totem = "CMUBadBloodTotem";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task OnlyADesecratedTotemWithNoHunterNearIsReadOncePerDesecration()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 30, 31, 32);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var intel = Server.System<BadBloodTotemIntelSystem>();
                var totems = Server.System<BadBloodTotemSystem>();
                var transform = Server.System<SharedTransformSystem>();
                var probe = Server.System<BadBloodTotemProbeSystem>();
                probe.Clear();

                var lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                spawned.Add(lodge);
                var totem = entMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30, 0)));
                spawned.Add(totem);
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(31, 0));
                spawned.Add(marine);

                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                var readable = entMan.GetComponent<BadBloodTotemReadableComponent>(totem);
                Assert.Multiple(() =>
                {
                    Assert.That(readable.ReadTime, Is.EqualTo(TimeSpan.FromSeconds(25)));
                    Assert.That(readable.GridSize, Is.EqualTo(10));
                });

                // a consecrated Totem tells a marine nothing
                Assert.Multiple(() =>
                {
                    Assert.That(intel.GetReadDenial(marine, totem), Is.EqualTo("cmu-bad-blood-totem-read-consecrated"));
                    Assert.That(intel.TryStartRead(marine, totem), Is.False);
                    Assert.That(intel.TryCompleteRead(marine, totem), Is.False);
                    Assert.That(readable.ReadThisDesecration, Is.False);
                    Assert.That(probe.Read, Is.Empty);
                });

                totems.SetDesecrated(new Entity<BadBloodTotemComponent>(totem, comp), true);
                Assert.That(comp.Desecrated, Is.True);

                // a Bad Blood cannot read it, and a living Bad Blood beside it stops a marine reading
                Assert.That(intel.GetReadDenial(grunt, totem), Is.Not.Null);
                transform.SetCoordinates(grunt, map.GridCoords.Offset(new Vector2(32, 0)));
                Assert.That(intel.GetReadDenial(marine, totem), Is.EqualTo("cmu-bad-blood-totem-read-hunter-near"));
                transform.SetCoordinates(grunt, map.GridCoords);

                Assert.That(intel.GetReadDenial(marine, totem), Is.Null);
                Assert.That(intel.TryStartRead(marine, totem), Is.True, "the read do-after starts");

                // the Lodge's square, on the same grid the rangefinder reads
                var lodgeCoords = transform.GetMapCoordinates(lodge);
                var position = lodgeCoords.Position.Floored();
                if (Server.System<RMCPlanetSystem>().TryGetOffset(lodgeCoords, out var offset))
                    position += offset;

                var expected = new Vector2i(
                    (int) Math.Floor(position.X / 10.0) * 10,
                    (int) Math.Floor(position.Y / 10.0) * 10);
                Assert.That(intel.TryGetLodgeSquare(10, out var corner), Is.True);
                Assert.That(corner, Is.EqualTo(expected));
                Assert.That(intel.GetReadReport(10), Does.Contain(Robust.Shared.Localization.Loc.GetString(
                    "cmu-bad-blood-totem-read-lodge",
                    ("x1", expected.X),
                    ("x2", expected.X + 9),
                    ("y1", expected.Y),
                    ("y2", expected.Y + 9))));

                // the do-after's completion
                Assert.That(intel.TryCompleteRead(marine, totem), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(readable.ReadThisDesecration, Is.True);
                    Assert.That(probe.Read, Is.EqualTo(new List<(EntityUid, EntityUid)> { (totem, marine) }));
                    Assert.That(intel.GetReadDenial(marine, totem), Is.EqualTo("cmu-bad-blood-totem-read-already"));
                    Assert.That(intel.TryCompleteRead(marine, totem), Is.False, "each desecration is read once");
                    Assert.That(probe.Read, Has.Count.EqualTo(1));
                });

                // the clan takes it back: the next desecration can be read again
                totems.SetDesecrated(new Entity<BadBloodTotemComponent>(totem, comp), false);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Reconsecrated, Does.Contain(totem));
                    Assert.That(readable.ReadThisDesecration, Is.False, "reconsecration clears the read");
                    Assert.That(intel.GetReadDenial(marine, totem), Is.EqualTo("cmu-bad-blood-totem-read-consecrated"));
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned.Concat(party.All))
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    private async Task LayFloor(TestMapData map, params int[] columns)
    {
        await Server.WaitPost(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            foreach (var x in columns)
            {
                maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(x, 0), map.Tile.Tile);
            }
        });
    }
}

// records the broadcast Totem events FLOW-503 and FLOW-506 raise
public sealed class BadBloodTotemProbeSystem : EntitySystem
{
    public readonly List<EntityUid> Desecrated = new();
    public readonly List<EntityUid> Reconsecrated = new();
    public readonly List<(EntityUid Totem, EntityUid Reader)> Read = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodTotemDesecratedEvent>(OnDesecrated);
        SubscribeLocalEvent<BadBloodTotemReconsecratedEvent>(OnReconsecrated);
        SubscribeLocalEvent<BadBloodTotemReadEvent>(OnRead);
    }

    public void Clear()
    {
        Desecrated.Clear();
        Reconsecrated.Clear();
        Read.Clear();
    }

    private void OnDesecrated(ref BadBloodTotemDesecratedEvent ev)
    {
        Desecrated.Add(ev.Totem);
    }

    private void OnReconsecrated(ref BadBloodTotemReconsecratedEvent ev)
    {
        Reconsecrated.Add(ev.Totem);
    }

    private void OnRead(ref BadBloodTotemReadEvent ev)
    {
        Read.Add((ev.Totem, ev.Reader));
    }
}
