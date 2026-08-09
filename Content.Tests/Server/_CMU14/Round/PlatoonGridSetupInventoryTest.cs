using Content.Server.AU14.Round;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Server._CMU14.Round;

[TestFixture]
public sealed class PlatoonGridSetupInventoryTest
{
    [Test]
    public void InitialInventoryPreservesOrderAndIsolatesShipNetworks()
    {
        var inventory = new PlatoonInitialSetupInventory();
        var govforShip = new EntityUid(1);
        var opforShip = new EntityUid(2);
        var govforGrid = new EntityUid(11);
        var opforGrid = new EntityUid(12);
        var govforMap = new EntityUid(21);
        var opforMap = new EntityUid(22);
        var govforNetwork = new EntityUid(31);
        var opforNetwork = new EntityUid(32);
        inventory.AddShip(govforShip, govforGrid, govforMap, govforNetwork);
        inventory.AddShip(opforShip, opforGrid, opforMap, opforNetwork);

        var planetMarker = new EntityUid(40);
        var opforMarker = new EntityUid(41);
        var govforUpperDeckMarker = new EntityUid(42);
        var opforSameMapMarker = new EntityUid(43);
        inventory.AddVendorMarker(planetMarker, EntityUid.Invalid, null, new EntityUid(23), null);
        inventory.AddVendorMarker(opforMarker, opforShip, opforShip, opforMap, opforNetwork);
        inventory.AddVendorMarker(
            govforUpperDeckMarker,
            EntityUid.Invalid,
            new EntityUid(13),
            new EntityUid(24),
            govforNetwork);
        inventory.AddVendorMarker(
            opforSameMapMarker,
            EntityUid.Invalid,
            new EntityUid(14),
            opforMap,
            opforNetwork);

        var govforPhone = new EntityUid(51);
        var opforPhone = new EntityUid(52);
        inventory.AddPhone(govforPhone, EntityUid.Invalid, govforGrid);
        inventory.AddPhone(opforPhone, opforShip, opforGrid);

        Assert.Multiple(() =>
        {
            Assert.That(inventory.Ships, Is.EqualTo(new[] { govforShip, opforShip }));
            Assert.That(
                inventory.VendorMarkers,
                Is.EqualTo(new[] { planetMarker, opforMarker, govforUpperDeckMarker, opforSameMapMarker }));
            Assert.That(inventory.GetShipMarkers(govforShip), Is.EqualTo(new[] { govforUpperDeckMarker }));
            Assert.That(inventory.GetShipMarkers(opforShip), Is.EqualTo(new[] { opforMarker }));
            Assert.That(inventory.GetShipPhones(govforShip), Is.EqualTo(new[] { govforPhone }));
            Assert.That(inventory.GetShipPhones(opforShip), Is.EqualTo(new[] { opforPhone }));
            Assert.That(inventory.IndexedPhones, Is.EqualTo(2));
            Assert.That(inventory.ShipMarkerAssignments, Is.EqualTo(2));
            Assert.That(inventory.ShipPhoneAssignments, Is.EqualTo(2));
        });
    }

    [Test]
    public void GroupsMarkersByExactPrototype()
    {
        var inventory = new PlatoonGridSetupInventory(new EntityUid(1));
        var navigation = new EntityUid(2);
        var weapons = new EntityUid(3);

        inventory.AddMarker("NavigationMarker", navigation);
        inventory.AddMarker("WeaponsMarker", weapons);

        Assert.Multiple(() =>
        {
            Assert.That(inventory.GetMarkers("NavigationMarker"), Is.EqualTo(new[] { navigation }));
            Assert.That(inventory.GetMarkers("WeaponsMarker"), Is.EqualTo(new[] { weapons }));
            Assert.That(inventory.GetMarkers("navigationmarker"), Is.Empty);
            Assert.That(inventory.GetMarkers("MissingMarker"), Is.Empty);
        });
    }

    [Test]
    public void DestinationPoolDeduplicatesAndTracksUsedEntries()
    {
        var pool = new PlatoonDestinationPool();
        var first = new EntityUid(1);
        var second = new EntityUid(2);

        pool.Add(first);
        pool.Add(first);
        pool.Add(second);

        Assert.Multiple(() =>
        {
            Assert.That(pool.Destinations, Is.EqualTo(new[] { first, second }));
            Assert.That(pool.Count, Is.EqualTo(2));
            Assert.That(pool.IsUsed(first), Is.False);
        });

        pool.MarkUsed(first);

        Assert.Multiple(() =>
        {
            Assert.That(pool.IsUsed(first), Is.True);
            Assert.That(pool.IsUsed(second), Is.False);
        });
    }
}
