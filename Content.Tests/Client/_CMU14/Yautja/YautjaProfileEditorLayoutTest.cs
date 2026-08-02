using System.Linq;
using Content.Client._CMU14.Yautja.Lobby;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.Yautja;

[TestFixture]
public sealed class YautjaProfileEditorLayoutTest
{
    [Test]
    public void CategoriesExposeAllNavigationGroupsInDesignOrder()
    {
        Assert.That(
            YautjaProfileEditorLayout.Categories,
            Has.Exactly(5).Items);
        Assert.That(
            YautjaProfileEditorLayout.Categories.Select(info => info.Id),
            Is.EqualTo(new[]
            {
                YautjaProfileEditorCategory.Appearance,
                YautjaProfileEditorCategory.Equipment,
                YautjaProfileEditorCategory.Sets,
                YautjaProfileEditorCategory.Technology,
                YautjaProfileEditorCategory.Description,
            }));
    }

    [TestCase(YautjaProfileEditorCategory.Appearance, YautjaProfileEditorCategory.Appearance, true)]
    [TestCase(YautjaProfileEditorCategory.Appearance, YautjaProfileEditorCategory.Equipment, false)]
    public void OnlyTheActiveCategoryPageIsVisible(
        YautjaProfileEditorCategory active,
        YautjaProfileEditorCategory candidate,
        bool expected)
    {
        Assert.That(YautjaProfileEditorLayout.IsCategoryActive(active, candidate), Is.EqualTo(expected));
    }

    [TestCase(YautjaRank.Unblooded, true)]
    [TestCase(YautjaRank.YoungBlood, true)]
    [TestCase(YautjaRank.Blooded, true)]
    [TestCase(YautjaRank.Elite, false)]
    [TestCase(YautjaRank.Elder, false)]
    [TestCase(YautjaRank.Leader, false)]
    [TestCase(YautjaRank.Ancient, false)]
    public void UniqueSetsAreLockedUntilElite(YautjaRank rank, bool locked)
    {
        var profile = YautjaCharacterProfile.Default.WithRank(rank);

        Assert.That(
            YautjaProfileEditorLayout.IsUniqueSetLocked(profile, YautjaUniqueSet.Anubys),
            Is.EqualTo(locked));
    }

    [Test]
    public void NoneOptionIsNeverLocked()
    {
        var profile = YautjaCharacterProfile.Default.WithRank(YautjaRank.Blooded);

        Assert.That(
            YautjaProfileEditorLayout.IsUniqueSetLocked(profile, YautjaUniqueSet.None),
            Is.False);
    }

    [Test]
    public void TechnologyOptionsUseVerticalLocalizationSafeLayout()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YautjaProfileEditorLayout.TechOptionSpacing, Is.GreaterThan(0));
            Assert.That(YautjaProfileEditorLayout.TechOptionBottomMargin, Is.GreaterThanOrEqualTo(10));
        });
    }

    [TestCase(false, YautjaLegacySet.Dragon, true)]
    [TestCase(true, YautjaLegacySet.Dragon, false)]
    [TestCase(false, YautjaLegacySet.None, false)]
    public void LegacySetsFollowServerCapability(bool canUseLegacy, YautjaLegacySet legacy, bool locked)
    {
        var capabilities = new YautjaProfileCapabilities(YautjaRank.Blooded, false, canUseLegacy);

        Assert.That(
            YautjaProfileEditorLayout.IsLegacySetLocked(capabilities, legacy),
            Is.EqualTo(locked));
    }

    [TestCase(YautjaRank.Blooded, YautjaCapeStyle.Ceremonial, true)]
    [TestCase(YautjaRank.Elite, YautjaCapeStyle.Ceremonial, true)]
    [TestCase(YautjaRank.Elder, YautjaCapeStyle.Ceremonial, true)]
    [TestCase(YautjaRank.Leader, YautjaCapeStyle.Ceremonial, false)]
    [TestCase(YautjaRank.Ancient, YautjaCapeStyle.Ceremonial, false)]
    [TestCase(YautjaRank.Blooded, YautjaCapeStyle.Full, false)]
    public void CeremonialCapeRequiresLeaderOrAncient(
        YautjaRank rank,
        YautjaCapeStyle cape,
        bool locked)
    {
        var capabilities = new YautjaProfileCapabilities(rank, false, false);

        Assert.That(
            YautjaProfileEditorLayout.IsCapeLocked(capabilities, cape),
            Is.EqualTo(locked));
    }

    [TestCase(YautjaRank.Blooded, false, YautjaBracerMaterial.Bronze, true)]
    [TestCase(YautjaRank.Blooded, false, YautjaBracerMaterial.Crimson, true)]
    [TestCase(YautjaRank.Blooded, false, YautjaBracerMaterial.Bone, true)]
    [TestCase(YautjaRank.Elite, false, YautjaBracerMaterial.Bronze, false)]
    [TestCase(YautjaRank.Elder, false, YautjaBracerMaterial.Crimson, false)]
    [TestCase(YautjaRank.Leader, false, YautjaBracerMaterial.Bone, false)]
    [TestCase(YautjaRank.Blooded, false, YautjaBracerMaterial.Ebony, false)]
    [TestCase(YautjaRank.Ancient, false, YautjaBracerMaterial.Dragon, true)]
    [TestCase(YautjaRank.Blooded, true, YautjaBracerMaterial.Dragon, false)]
    [TestCase(YautjaRank.Ancient, true, YautjaBracerMaterial.Collector, false)]
    public void BracersFollowRankAndLegacyWhitelist(
        YautjaRank rank,
        bool canUseLegacy,
        YautjaBracerMaterial bracer,
        bool locked)
    {
        var capabilities = new YautjaProfileCapabilities(rank, false, canUseLegacy);

        Assert.That(
            YautjaProfileEditorLayout.IsBracerLocked(capabilities, bracer),
            Is.EqualTo(locked));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void UniqueSetsFollowServerCapability(bool canUseUnique, bool locked)
    {
        var capabilities = new YautjaProfileCapabilities(YautjaRank.Blooded, canUseUnique, false);

        Assert.That(
            YautjaProfileEditorLayout.IsUniqueSetLocked(capabilities, YautjaUniqueSet.Anubys),
            Is.EqualTo(locked));
    }

    [Test]
    public void SummarySelectionUsesUniqueSetAndCurrentGear()
    {
        var profile = YautjaCharacterProfile.Default
            .WithRank(YautjaRank.Elite)
            .WithUnique(YautjaUniqueSet.Anubys)
            .WithArmor(YautjaGearMaterial.Silver, 2)
            .WithMask(YautjaGearMaterial.Bronze, 3)
            .WithGreaves(YautjaGearMaterial.Bone, 1)
            .WithCapeStyle(YautjaCapeStyle.Full)
            .WithBracer(YautjaBracerMaterial.Crimson)
            .WithCaster(YautjaBracerMaterial.Silver);

        var selection = YautjaProfileEditorLayout.GetSummarySelection(profile);

        Assert.That(selection.Unique, Is.EqualTo(YautjaUniqueSet.Anubys));
        Assert.That(selection.Legacy, Is.EqualTo(YautjaLegacySet.None));
        Assert.That(selection.ArmorMaterial, Is.EqualTo(YautjaGearMaterial.Silver));
        Assert.That(selection.ArmorStyle, Is.EqualTo(2));
        Assert.That(selection.MaskMaterial, Is.EqualTo(YautjaGearMaterial.Bronze));
        Assert.That(selection.MaskStyle, Is.EqualTo(3));
        Assert.That(selection.GreavesMaterial, Is.EqualTo(YautjaGearMaterial.Bone));
        Assert.That(selection.GreavesStyle, Is.EqualTo(1));
        Assert.That(selection.CapeStyle, Is.EqualTo(YautjaCapeStyle.Full));
        Assert.That(selection.BracerMaterial, Is.EqualTo(YautjaBracerMaterial.Crimson));
        Assert.That(selection.CasterMaterial, Is.EqualTo(YautjaBracerMaterial.Silver));
    }

    [Test]
    public void BuildSummaryUsesUniqueSetAndCurrentGearNames()
    {
        var profile = YautjaCharacterProfile.Default
            .WithRank(YautjaRank.Elite)
            .WithUnique(YautjaUniqueSet.Anubys)
            .WithArmor(YautjaGearMaterial.Silver, 2)
            .WithMask(YautjaGearMaterial.Bronze, 3)
            .WithGreaves(YautjaGearMaterial.Bone, 1)
            .WithCapeStyle(YautjaCapeStyle.Full)
            .WithBracer(YautjaBracerMaterial.Crimson)
            .WithCaster(YautjaBracerMaterial.Silver);

        var summary = YautjaProfileEditorLayout.BuildSummary(profile);

        Assert.That(summary.Set, Is.EqualTo(YautjaCharacterProfile.GetUniqueDisplayName(YautjaUniqueSet.Anubys)));
        Assert.That(summary.Armor, Is.EqualTo(YautjaCharacterProfile.GetArmorStyleDisplayName(YautjaGearMaterial.Silver, 2)));
        Assert.That(summary.Mask, Is.EqualTo(YautjaCharacterProfile.GetMaskStyleDisplayName(YautjaGearMaterial.Bronze, 3)));
        Assert.That(summary.Greaves, Is.EqualTo(YautjaCharacterProfile.GetGreavesStyleDisplayName(YautjaGearMaterial.Bone, 1)));
        Assert.That(summary.Cape, Is.EqualTo(YautjaCharacterProfile.GetCapeDisplayName(YautjaCapeStyle.Full)));
        Assert.That(summary.Bracer, Is.EqualTo(YautjaCharacterProfile.GetBracerDisplayName(YautjaBracerMaterial.Crimson)));
        Assert.That(summary.Caster, Is.EqualTo(YautjaCharacterProfile.GetCasterDisplayName(YautjaBracerMaterial.Silver)));
    }

    [TestCase(760, 6, 6)]
    [TestCase(340, 6, 3)]
    [TestCase(220, 4, 1)]
    public void ResponsiveColumnsFitTheAvailableWidth(float availableWidth, int preferredColumns, int expected)
    {
        Assert.That(
            YautjaProfileEditorLayout.GetResponsiveColumnCount(availableWidth, preferredColumns),
            Is.EqualTo(expected));
    }

    [TestCase(0, true)]
    [TestCase(749, true)]
    [TestCase(750, false)]
    [TestCase(1100, false)]
    public void WorkAreaStacksWhenWidthCannotFitFixedColumns(float availableWidth, bool expected)
    {
        Assert.That(
            YautjaProfileEditorLayout.ShouldStackWorkArea(availableWidth),
            Is.EqualTo(expected));
    }
}
