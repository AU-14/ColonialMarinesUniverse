using System.Collections.Generic;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Tests.Server._CMU14.Spawners;

[TestFixture]
public sealed class CMUContainerSpawnPointBatchSnapshotTest
{
    private static readonly ProtoId<JobPrototype> MarineJob = "CMUMarine";
    private static readonly ProtoId<JobPrototype> OfficerJob = "CMUOfficer";

    [Test]
    public void MatchPreservesStationJobAndRoundPhaseSemantics()
    {
        var station = new EntityUid(100);
        var otherStation = new EntityUid(200);

        Assert.Multiple(() =>
        {
            Assert.That(Matches(station, null, SpawnPointType.Unset, station, MarineJob, false), Is.True);
            Assert.That(Matches(station, MarineJob, SpawnPointType.Unset, station, MarineJob, true), Is.True);
            Assert.That(Matches(station, OfficerJob, SpawnPointType.Unset, station, MarineJob, false), Is.False);
            Assert.That(Matches(station, OfficerJob, SpawnPointType.Job, station, null, false), Is.True);
            Assert.That(Matches(station, MarineJob, SpawnPointType.Job, station, MarineJob, true), Is.False);
            Assert.That(Matches(station, OfficerJob, SpawnPointType.LateJoin, station, MarineJob, true), Is.True);
            Assert.That(Matches(otherStation, MarineJob, SpawnPointType.Unset, station, MarineJob, false), Is.False);
        });
    }

    [Test]
    public void CandidateBucketsPreserveQueryOrderAndWildcardSemantics()
    {
        var station = new EntityUid(100);
        var otherStation = new EntityUid(200);
        var snapshot = new CMUContainerSpawnPointBatchSnapshot();
        snapshot.Add(Entry(1, station, null, SpawnPointType.Unset));
        snapshot.Add(Entry(2, otherStation, MarineJob, SpawnPointType.Unset));
        snapshot.Add(Entry(3, station, OfficerJob, SpawnPointType.LateJoin));
        snapshot.Add(Entry(4, station, OfficerJob, SpawnPointType.Job));
        snapshot.Add(Entry(5, station, MarineJob, SpawnPointType.Unset));
        snapshot.Add(Entry(6, station, MarineJob, SpawnPointType.Job));
        snapshot.Add(Entry(7, station, null, SpawnPointType.LateJoin));

        var candidates = new List<CMUContainerSpawnPointEntry>();

        snapshot.CopyCandidates(station, MarineJob, inRound: false, candidates);
        AssertUids(candidates, 1, 5, 6);

        snapshot.CopyCandidates(station, null, inRound: false, candidates);
        AssertUids(candidates, 1, 4, 6);

        snapshot.CopyCandidates(station, MarineJob, inRound: true, candidates);
        AssertUids(candidates, 1, 3, 5, 7);

        snapshot.CopyCandidates(null, MarineJob, inRound: false, candidates);
        AssertUids(candidates, 1, 2, 5, 6);

        snapshot.CopyCandidates(new EntityUid(300), MarineJob, inRound: false, candidates);
        Assert.That(candidates, Is.Empty);

        snapshot.Clear();
        snapshot.CopyCandidates(null, MarineJob, inRound: false, candidates);

        Assert.That(snapshot.Count, Is.Zero);
        Assert.That(candidates, Is.Empty);
    }

    private static bool Matches(
        EntityUid? candidateStation,
        ProtoId<JobPrototype>? candidateJob,
        SpawnPointType spawnType,
        EntityUid? requestedStation,
        ProtoId<JobPrototype>? requestedJob,
        bool inRound)
    {
        return CMUContainerSpawnPointBatchSnapshot.Matches(
            candidateStation,
            candidateJob,
            spawnType,
            requestedStation,
            requestedJob,
            inRound);
    }

    private static CMUContainerSpawnPointEntry Entry(
        int uid,
        EntityUid? station,
        ProtoId<JobPrototype>? job,
        SpawnPointType spawnType)
    {
        return new CMUContainerSpawnPointEntry(
            new EntityUid(uid),
            station,
            job,
            spawnType,
            "storage");
    }

    private static void AssertUids(List<CMUContainerSpawnPointEntry> entries, params int[] expected)
    {
        Assert.That(entries, Has.Count.EqualTo(expected.Length));
        for (var i = 0; i < expected.Length; i++)
            Assert.That(entries[i].Uid, Is.EqualTo(new EntityUid(expected[i])), $"Candidate at index {i}");
    }
}
