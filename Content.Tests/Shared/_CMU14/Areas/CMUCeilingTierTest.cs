using Content.Shared._RMC14.Areas;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Areas;

[TestFixture]
public sealed class CMUCeilingTierTest
{
    // Permissions: CAS, OB, Fulton, supply drop, mortar fire, mortar placement,
    // lasing, medevac, paradropping. Each single restriction must close all weaker tiers.
    [TestCase(0, 511, TestName = "OpenSkyAllowsAllActions")]
    [TestCase(1, 2, TestName = "CASRoofAllowsOnlyOB")]
    [TestCase(2, 0, TestName = "OBRoofAlsoBlocksCASAndParadrops")]
    [TestCase(4, 3, TestName = "FultonRoofBlocksAllTierTwoActions")]
    [TestCase(8, 3, TestName = "SupplyRoofBlocksAllTierTwoActions")]
    [TestCase(16, 3, TestName = "MortarRoofBlocksAllTierTwoActions")]
    [TestCase(32, 31, TestName = "MortarPlacementRoofBlocksAllTierOneActions")]
    [TestCase(64, 31, TestName = "LasingRoofBlocksAllTierOneActions")]
    [TestCase(128, 31, TestName = "MedevacRoofBlocksAllTierOneActions")]
    [TestCase(256, 31, TestName = "ParadropRoofBlocksAllTierOneActions")]
    [TestCase(511, 0, TestName = "FullyProtectedAreaStaysProtected")]
    public void EnforcesCumulativeRestrictions(int blocked, int expectedAllowed)
    {
        // Construct legacy prototype data without loading a game world.
#pragma warning disable RA0002
        var area = new AreaComponent
        {
            CAS = (blocked & 1) == 0,
            OB = (blocked & 2) == 0,
            Fulton = (blocked & 4) == 0,
            SupplyDrop = (blocked & 8) == 0,
            MortarFire = (blocked & 16) == 0,
            MortarPlacement = (blocked & 32) == 0,
            Lasing = (blocked & 64) == 0,
            Medevac = (blocked & 128) == 0,
            Paradropping = (blocked & 256) == 0,
        };
#pragma warning restore RA0002

        AreaSystem.ApplyCeilingTier(area);

        Assert.Multiple(() =>
        {
            Assert.That(area.CAS, Is.EqualTo((expectedAllowed & 1) != 0), "CAS");
            Assert.That(area.OB, Is.EqualTo((expectedAllowed & 2) != 0), "OB");
            Assert.That(area.Fulton, Is.EqualTo((expectedAllowed & 4) != 0), "Fulton");
            Assert.That(area.SupplyDrop, Is.EqualTo((expectedAllowed & 8) != 0), "Supply drop");
            Assert.That(area.MortarFire, Is.EqualTo((expectedAllowed & 16) != 0), "Mortar fire");
            Assert.That(area.MortarPlacement, Is.EqualTo((expectedAllowed & 32) != 0), "Mortar placement");
            Assert.That(area.Lasing, Is.EqualTo((expectedAllowed & 64) != 0), "Lasing");
            Assert.That(area.Medevac, Is.EqualTo((expectedAllowed & 128) != 0), "Medevac");
            Assert.That(area.Paradropping, Is.EqualTo((expectedAllowed & 256) != 0), "Paradropping");
        });
    }
}
