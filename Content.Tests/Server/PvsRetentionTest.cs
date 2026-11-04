using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.Server.CMU14.ZLevels.Core;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Server;

[TestFixture]
public sealed class PvsRetentionTest
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void PublicationDropsOversizedBuffersAndRoundCleanupReleasesRetainedCapacity()
    {
        var system = new CMUZLevelsSystem();
        typeof(CMUZLevelsSystem).GetField("_zLevelsEnabled", Private)!.SetValue(system, false);
        var cells = (Dictionary<(EntityUid, Vector2i), List<(EntityUid, Vector2)>>)
            typeof(CMUZLevelsSystem).GetField("_overheadPvsCells", Private)!.GetValue(system)!;
        for (var i = 0; i < 100; i++)
            cells.Add((new EntityUid(1), new Vector2i(i, 0)), new(16));
        cells.Add((new EntityUid(1), new Vector2i(-1, 0)), new(10000));
        system.PrepareOverheadPvs();
        var pooled = system.GetPvsStorageUsage();
        Assert.That(pooled.ActiveCells, Is.Zero);
        Assert.That(pooled.PooledBuffers, Is.EqualTo(64));
        Assert.That(pooled.PooledMemberCapacity, Is.EqualTo(64 * 16));
        typeof(CMUZLevelsSystem).GetMethod("ClearOverheadPvsStorage", Private)!.Invoke(system, null);
        var cleared = system.GetPvsStorageUsage();
        Assert.That(cleared.PooledBuffers, Is.Zero);
        Assert.That(cleared.PooledMemberCapacity, Is.Zero);
        Assert.That(cleared.DictionaryCapacity, Is.LessThanOrEqualTo(12), "Only minimum dictionary bookkeeping may remain.");
    }
}
