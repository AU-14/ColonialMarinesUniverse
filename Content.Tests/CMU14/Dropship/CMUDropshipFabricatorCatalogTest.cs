using Content.Client.CMU14.Dropship.Fabricator;
using NUnit.Framework;
using static Content.Shared._RMC14.Dropship.Fabricator.DropshipFabricatorPrintableComponent;

namespace Content.Tests.CMU14.Dropship;

[TestFixture]
public sealed class CMUDropshipFabricatorCatalogTest
{
    private const string Bofors = "L/60 40mm BOFORS Autocannon";
    private const string Gau = "GAU-21 30mm Cannon";

    private static readonly CMUDropshipFabricatorEntry RmcBofors = new("RMCDropshipAttachmentBOFORS", Bofors, 800, CategoryType.Equipment, Bofors);
    private static readonly CMUDropshipFabricatorEntry CmuBofors = new("CMUDropshipAttachmentBOFORS", Bofors, 800, CategoryType.Equipment, Bofors);
    private static readonly CMUDropshipFabricatorEntry BoforsHeat = new("CMUDropshipAmmoBOFORSMagHEAT", "40mm HEAT tray", 600, CategoryType.Ammo, Bofors);
    private static readonly CMUDropshipFabricatorEntry GauAp = new("RMCDropshipAttachmentAmmoGAUAP", "PGU-105 AP", 450, CategoryType.Ammo, Gau);
    private static readonly CMUDropshipFabricatorEntry Medevac = new("RMCDropshipMedevac", "Medevac system", 300, CategoryType.Equipment, null);

    private static readonly CMUDropshipFabricatorEntry[] Entries = [RmcBofors, CmuBofors, BoforsHeat, GauAp, Medevac];

    [Test]
    public void WeaponsAreDistinctAcrossReparentedCopies()
    {
        Assert.That(CMUDropshipFabricatorCatalog.GetWeapons(Entries), Is.EqualTo(new[] { Gau, Bofors }));
    }

    [Test]
    public void WeaponFilterShowsWeaponAndItsAmmoOnly()
    {
        var filter = CMUDropshipFabricatorFilter.ForWeapon(Bofors.ToUpperInvariant());

        Assert.Multiple(() =>
        {
            Assert.That(CMUDropshipFabricatorCatalog.IsVisible(RmcBofors, filter, null), Is.True);
            Assert.That(CMUDropshipFabricatorCatalog.IsVisible(CmuBofors, filter, null), Is.True);
            Assert.That(CMUDropshipFabricatorCatalog.IsVisible(BoforsHeat, filter, null), Is.True);
            Assert.That(CMUDropshipFabricatorCatalog.IsVisible(GauAp, filter, null), Is.False);
            Assert.That(CMUDropshipFabricatorCatalog.IsVisible(Medevac, filter, null), Is.False);
        });
    }

    [Test]
    public void SupportFilterShowsOnlyGearWithoutWeapon()
    {
        Assert.That(CMUDropshipFabricatorCatalog.Count(Entries, CMUDropshipFabricatorFilter.Support, null), Is.EqualTo(1));
        Assert.That(CMUDropshipFabricatorCatalog.IsVisible(Medevac, CMUDropshipFabricatorFilter.Support, null), Is.True);
    }

    [Test]
    public void SearchMatchesItemOrWeaponNameAndCombinesWithFilter()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CMUDropshipFabricatorCatalog.Count(Entries, CMUDropshipFabricatorFilter.All, "  bofors "), Is.EqualTo(3));
            Assert.That(CMUDropshipFabricatorCatalog.Count(Entries, CMUDropshipFabricatorFilter.All, "heat"), Is.EqualTo(1));
            Assert.That(CMUDropshipFabricatorCatalog.Count(Entries, CMUDropshipFabricatorFilter.ForWeapon(Gau), "heat"), Is.EqualTo(0));
            Assert.That(CMUDropshipFabricatorCatalog.Count(Entries, CMUDropshipFabricatorFilter.All, ""), Is.EqualTo(Entries.Length));
        });
    }
}
