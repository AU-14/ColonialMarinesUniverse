using Content.Server._CMU14.Spawners;
using Content.Server.Spawners.Components;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Tests.Server._CMU14.Spawners;

[TestFixture]
public sealed class CMURoundSpawnPointSnapshotTest
{
    private static readonly ProtoId<JobPrototype> MarineJob = "CMUMarine";
    private static readonly ProtoId<JobPrototype> OfficerJob = "CMUOfficer";
    private static readonly ProtoId<JobPrototype> UnknownJob = "CMUUnknown";

    [Test]
    public void GenericSelectionUsesStationJobAndRoundPhaseBuckets()
    {
        var snapshot = new CMURoundSpawnPointSnapshot();
        var random = new RobustRandom();
        random.SetSeed(7);
        var station = new EntityUid(100);
        var otherStation = new EntityUid(200);

        snapshot.Add(Entry(1, station, null, SpawnPointType.Job));
        snapshot.Add(Entry(2, station, MarineJob, SpawnPointType.Job));
        snapshot.Add(Entry(3, otherStation, MarineJob, SpawnPointType.Job));
        snapshot.Add(Entry(4, station, null, SpawnPointType.LateJoin));

        var roundStart = snapshot.PickGeneric(station, MarineJob, false, random, out var roundStartUid);
        var lateJoin = snapshot.PickGeneric(station, MarineJob, true, random, out var lateJoinUid);
        var fallback = snapshot.PickGeneric(new EntityUid(300), MarineJob, false, random, out var fallbackUid);

        Assert.Multiple(() =>
        {
            Assert.That(roundStart, Is.EqualTo(CMUGenericSpawnSelection.Preferred));
            Assert.That(roundStartUid, Is.AnyOf(new EntityUid(1), new EntityUid(2)));
            Assert.That(lateJoin, Is.EqualTo(CMUGenericSpawnSelection.Preferred));
            Assert.That(lateJoinUid, Is.EqualTo(new EntityUid(4)));
            Assert.That(fallback, Is.EqualTo(CMUGenericSpawnSelection.Fallback));
            Assert.That(fallbackUid, Is.EqualTo(new EntityUid(1)));
        });
    }

    [Test]
    public void FactionSelectionPreservesPreferredAndFallbackOrdering()
    {
        var snapshot = new CMURoundSpawnPointSnapshot();
        var random = new RobustRandom();
        random.SetSeed(11);

        snapshot.Add(Entry(1, null, MarineJob, SpawnPointType.Job, onAnyShip: true, onGovforShip: true));
        snapshot.Add(Entry(2, null, OfficerJob, SpawnPointType.Unset, onAnyShip: true, onGovforShip: true));
        snapshot.Add(Entry(3, null, MarineJob, SpawnPointType.Observer, onAnyShip: true, onGovforShip: true));
        snapshot.Add(Entry(4, null, MarineJob, SpawnPointType.Job));
        snapshot.Add(Entry(5, null, OfficerJob, SpawnPointType.Job));
        snapshot.Add(Entry(6, null, null, SpawnPointType.LateJoinGovfor));
        snapshot.Add(Entry(7, null, null, SpawnPointType.LateJoinOpfor));

        Assert.Multiple(() =>
        {
            Assert.That(
                snapshot.TryPickFactionShip(RoundJobSide.Govfor, MarineJob, random, out var exactShip),
                Is.True);
            Assert.That(exactShip, Is.EqualTo(new EntityUid(1)));

            Assert.That(
                snapshot.TryPickFactionShip(RoundJobSide.Govfor, UnknownJob, random, out var fallbackShip),
                Is.True);
            Assert.That(fallbackShip, Is.AnyOf(new EntityUid(1), new EntityUid(2)));

            Assert.That(
                snapshot.TryPickFactionPlanet(RoundJobSide.Govfor, MarineJob, random, out var exactPlanet),
                Is.True);
            Assert.That(exactPlanet, Is.EqualTo(new EntityUid(4)));

            Assert.That(
                snapshot.TryPickFactionPlanet(RoundJobSide.Govfor, UnknownJob, random, out var govforFallback),
                Is.True);
            Assert.That(govforFallback, Is.EqualTo(new EntityUid(6)));

            Assert.That(
                snapshot.TryPickFactionPlanet(RoundJobSide.Opfor, UnknownJob, random, out var opforFallback),
                Is.True);
            Assert.That(opforFallback, Is.EqualTo(new EntityUid(7)));
        });
    }

    [Test]
    public void GenericWildcardAndExactCandidatesPreserveQueryOrder()
    {
        var snapshot = new CMURoundSpawnPointSnapshot();
        var station = new EntityUid(100);
        var expectedOrder = new[] { new EntityUid(1), new EntityUid(2), new EntityUid(3) };

        snapshot.Add(Entry(1, station, MarineJob, SpawnPointType.Job));
        snapshot.Add(Entry(2, station, null, SpawnPointType.Job));
        snapshot.Add(Entry(3, station, MarineJob, SpawnPointType.Job));
        snapshot.Add(Entry(4, station, OfficerJob, SpawnPointType.Job));

        for (var seed = 0; seed < 64; seed++)
        {
            var actualRandom = new RobustRandom();
            var expectedRandom = new RobustRandom();
            actualRandom.SetSeed(seed);
            expectedRandom.SetSeed(seed);
            var expected = expectedOrder[expectedRandom.Next(expectedOrder.Length)];
            var selection = snapshot.PickGeneric(station, MarineJob, false, actualRandom, out var actual);

            Assert.That(selection, Is.EqualTo(CMUGenericSpawnSelection.Preferred));
            Assert.That(actual, Is.EqualTo(expected), $"Seed {seed}");
        }
    }

    [Test]
    public void GenericFallbackPreservesRandomConsumption()
    {
        var snapshot = new CMURoundSpawnPointSnapshot();
        var actualRandom = new RobustRandom();
        var expectedRandom = new RobustRandom();
        actualRandom.SetSeed(31);
        expectedRandom.SetSeed(31);
        snapshot.Add(Entry(1, new EntityUid(100), MarineJob, SpawnPointType.Job));

        expectedRandom.Next(1);
        var expectedNext = expectedRandom.Next(1000);
        var selection = snapshot.PickGeneric(
            new EntityUid(200),
            MarineJob,
            false,
            actualRandom,
            out var fallback);
        var actualNext = actualRandom.Next(1000);

        Assert.Multiple(() =>
        {
            Assert.That(selection, Is.EqualTo(CMUGenericSpawnSelection.Fallback));
            Assert.That(fallback, Is.EqualTo(new EntityUid(1)));
            Assert.That(actualNext, Is.EqualTo(expectedNext));
        });
    }

    [Test]
    public void ClearRemovesAllCachedCandidates()
    {
        var snapshot = new CMURoundSpawnPointSnapshot();
        var random = new RobustRandom();
        snapshot.Add(Entry(1, null, MarineJob, SpawnPointType.Job));

        snapshot.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Entries, Is.Empty);
            Assert.That(
                snapshot.PickGeneric(null, MarineJob, false, random, out _),
                Is.EqualTo(CMUGenericSpawnSelection.None));
            Assert.That(
                snapshot.TryPickFactionPlanet(RoundJobSide.Govfor, MarineJob, random, out _),
                Is.False);
        });
    }

    private static CMURoundSpawnPointEntry Entry(
        int uid,
        EntityUid? station,
        ProtoId<JobPrototype>? job,
        SpawnPointType spawnType,
        bool onAnyShip = false,
        bool onGovforShip = false,
        bool onOpforShip = false)
    {
        return new CMURoundSpawnPointEntry(
            new EntityUid(uid),
            station,
            job,
            spawnType,
            onAnyShip,
            onGovforShip,
            onOpforShip);
    }
}
