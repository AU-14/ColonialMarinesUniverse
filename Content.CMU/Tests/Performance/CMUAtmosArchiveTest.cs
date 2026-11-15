#pragma warning disable RA0002 // Exercise archive ownership and source replacement without exposing production internals.
using System;
using System.Reflection;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.CMU14.Performance;

[TestFixture]
public sealed class CMUAtmosArchiveTest
{
    private static Action<TileAtmosphere, int> CreateArchive()
        => typeof(AtmosphereSystem).GetMethod("Archive", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action<TileAtmosphere, int>>(new AtmosphereSystem());

    [Test]
    public void ArchiveTracksCyclesWithoutAliasingLiveOrOtherTileAir()
    {
        var archive = CreateArchive();
        var air = new GasMixture(100) { Temperature = 300 };
        air.SetMoles(Gas.Oxygen, 12);
        var tile = new TileAtmosphere(EntityUid.Invalid, Vector2i.Zero, air);
        var other = new TileAtmosphere(EntityUid.Invalid, Vector2i.One, air);
        archive(tile, 1);
        archive(other, 1);

        air.SetMoles(Gas.Oxygen, 3);
        air.SetMoles(Gas.Nitrogen, 7);
        air.Temperature = 450;
        air.Volume = 200;
        Assert.That(tile.AirArchived!.GetMoles(Gas.Oxygen), Is.EqualTo(12));
        Assert.That(tile.AirArchived.Temperature, Is.EqualTo(300));

        archive(tile, 2);
        Assert.Multiple(() =>
        {
            Assert.That(tile.ArchivedCycle, Is.EqualTo(2));
            Assert.That(tile.AirArchived.GetMoles(Gas.Oxygen), Is.EqualTo(3));
            Assert.That(tile.AirArchived.GetMoles(Gas.Nitrogen), Is.EqualTo(7));
            Assert.That(tile.AirArchived.Temperature, Is.EqualTo(450));
            Assert.That(tile.AirArchived.Volume, Is.EqualTo(200));
            Assert.That(other.AirArchived!.GetMoles(Gas.Oxygen), Is.EqualTo(12));
            Assert.That(other.AirArchived.Temperature, Is.EqualTo(300));
        });
        tile.AirArchived.SetMoles(Gas.Oxygen, 99);
        Assert.That(air.GetMoles(Gas.Oxygen), Is.EqualTo(3));
    }

    [Test]
    public void InitiallySharedImmutableArchiveDetachesAndCanRefresh()
    {
        var archive = CreateArchive();
        var air = new GasMixture(100) { Temperature = 300 };
        air.SetMoles(Gas.Oxygen, 12);
        air.MarkImmutable();
        var tile = new TileAtmosphere(EntityUid.Invalid, Vector2i.Zero, air);
        archive(tile, 1);

        // This is the same replacement that map-atmosphere revalidation can perform.
        tile.Air = new GasMixture(200) { Temperature = 450 };
        tile.Air.SetMoles(Gas.Nitrogen, 7);
        archive(tile, 2);
        Assert.Multiple(() =>
        {
            Assert.That(air.GetMoles(Gas.Oxygen), Is.EqualTo(12));
            Assert.That(air.GetMoles(Gas.Nitrogen), Is.Zero);
            Assert.That(tile.AirArchived!.GetMoles(Gas.Oxygen), Is.Zero);
            Assert.That(tile.AirArchived.GetMoles(Gas.Nitrogen), Is.EqualTo(7));
            Assert.That(tile.AirArchived.Temperature, Is.EqualTo(450));
            Assert.That(tile.AirArchived.Volume, Is.EqualTo(200));
        });
    }
}
