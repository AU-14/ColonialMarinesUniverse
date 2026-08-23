using System.Linq;
using Content.Shared.Access;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Preferences;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaRankParityTest
{
    [TestCase(YautjaRank.Unblooded, "predhud", false, false, new[] { "CMUAccessYautjaSecure" })]
    [TestCase(YautjaRank.YoungBlood, "predhud", false, false, new[] { "CMUAccessYautjaSecure" })]
    [TestCase(YautjaRank.Blooded, "predhud", false, false, new[] { "CMUAccessYautjaSecure" })]
    [TestCase(YautjaRank.Elite, "predhud", true, false, new[] { "CMUAccessYautjaSecure", "CMUAccessYautjaElite" })]
    [TestCase(YautjaRank.Elder, "predhud", true, false, new[] { "CMUAccessYautjaSecure", "CMUAccessYautjaElite", "CMUAccessYautjaElder" })]
    [TestCase(YautjaRank.Leader, "leaderhud", true, true, new[] { "CMUAccessYautjaSecure", "CMUAccessYautjaElite", "CMUAccessYautjaElder", "CMUAccessYautjaLeader" })]
    [TestCase(YautjaRank.Ancient, "councilhud", true, true, new[] { "CMUAccessYautjaSecure", "CMUAccessYautjaElite", "CMUAccessYautjaElder", "CMUAccessYautjaLeader", "CMUAccessYautjaAncient" })]
    public void RankMetadataMatchesCmss13(
        YautjaRank rank,
        string icon,
        bool unique,
        bool bypassSlots,
        string[] accessTags)
    {
        var metadata = YautjaRankMetadata.For(rank);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.IconState, Is.EqualTo(icon));
            Assert.That(metadata.UniqueSetsAllowed, Is.EqualTo(unique));
            Assert.That(metadata.BypassesPredatorSlotCap, Is.EqualTo(bypassSlots));
            Assert.That(metadata.AccessTags.Select(tag => tag.Id), Is.EqualTo(accessTags));
        });
    }

    [Test]
    public void RackAccessKeepsCmss13ElderOrAncientGate()
    {
        Assert.That(
            YautjaRankMetadata.GetRackAccessTags(YautjaRank.Elder).Select(tag => tag.Id),
            Is.EqualTo(new[] { "CMUAccessYautjaElder", "CMUAccessYautjaAncient" }));
    }

    [Test]
    public void LegacyWhitelistUsesItsOwnServerFlag()
    {
        var legacy = (YautjaWhitelistFlags) (1 << 1);
        var councilLegacy = (YautjaWhitelistFlags) (1 << 3);

        Assert.Multiple(() =>
        {
            Assert.That(legacy, Is.EqualTo(YautjaWhitelistFlags.Legacy));
            Assert.That(councilLegacy, Is.EqualTo(YautjaWhitelistFlags.CouncilLegacy));
            Assert.That(Enum.IsDefined(legacy), Is.True);
            Assert.That(Enum.IsDefined(councilLegacy), Is.True);
        });
    }

    [Test]
    public void SeniorWhitelistNormalStatusFallsBackToBlooded()
    {
        var capabilities = new YautjaProfileCapabilities(
            YautjaRank.Ancient,
            true,
            false,
            canUseCouncilStatus: true,
            canUseLeaderStatus: true);

        Assert.Multiple(() =>
        {
            Assert.That(capabilities.ResolveRank(YautjaProfileStatus.Normal), Is.EqualTo(YautjaRank.Blooded));
            Assert.That(capabilities.ResolveRank(YautjaProfileStatus.Council), Is.EqualTo(YautjaRank.Ancient));
            Assert.That(capabilities.ResolveRank(YautjaProfileStatus.Leader), Is.EqualTo(YautjaRank.Ancient));
        });
    }

    [Test]
    public void EffectiveCapabilitiesFollowSelectedSeniorStatus()
    {
        var capabilities = new YautjaProfileCapabilities(
            YautjaRank.Ancient,
            true,
            true,
            canUseCouncilStatus: true,
            canUseLeaderStatus: true);

        Assert.Multiple(() =>
        {
            Assert.That(capabilities.Rank, Is.EqualTo(YautjaRank.Ancient));
            Assert.That(capabilities.CanUseUnique, Is.True);
            Assert.That(capabilities.CanUseCape(YautjaCapeStyle.Ceremonial), Is.True);
            Assert.That(capabilities.ForStatus(YautjaProfileStatus.Normal).Rank, Is.EqualTo(YautjaRank.Blooded));
            Assert.That(capabilities.ForStatus(YautjaProfileStatus.Normal).CanUseUnique, Is.False);
            Assert.That(capabilities.ForStatus(YautjaProfileStatus.Council).Rank, Is.EqualTo(YautjaRank.Ancient));
            Assert.That(capabilities.ForStatus(YautjaProfileStatus.Council).CanUseUnique, Is.True);
        });
    }

    [Test]
    public void MissingHunterRankFallsBackToBlooded()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YautjaRankResolver.ResolveForHunter(null), Is.EqualTo(YautjaRank.Blooded));
            Assert.That(YautjaRankResolver.ResolveForHunter(YautjaCharacterProfile.Default), Is.EqualTo(YautjaRank.Blooded));
            Assert.That(
                YautjaRankResolver.ResolveForHunter(
                    YautjaCharacterProfile.Default.WithOwnerRank(YautjaBracerOwnerRank.Unblooded)),
                Is.EqualTo(YautjaRank.Blooded));
        });
    }

    [TestCase(YautjaBracerOwnerRank.Elite, YautjaRank.Elite)]
    [TestCase(YautjaBracerOwnerRank.Elder, YautjaRank.Elder)]
    [TestCase(YautjaBracerOwnerRank.Leader, YautjaRank.Leader)]
    [TestCase(YautjaBracerOwnerRank.Admin, YautjaRank.Ancient)]
    public void LegacySpecialOwnerRanksResolveToCanonicalRank(YautjaBracerOwnerRank ownerRank, YautjaRank expectedRank)
    {
        var profile = YautjaCharacterProfile.Default.WithOwnerRank(ownerRank);

        Assert.That(YautjaRankResolver.ResolveForHunter(profile), Is.EqualTo(expectedRank));
    }

    [TestCase(YautjaRank.Unblooded, YautjaBracerOwnerRank.Unblooded)]
    [TestCase(YautjaRank.YoungBlood, YautjaBracerOwnerRank.Unblooded)]
    [TestCase(YautjaRank.Blooded, YautjaBracerOwnerRank.Unblooded)]
    [TestCase(YautjaRank.Elite, YautjaBracerOwnerRank.Elite)]
    [TestCase(YautjaRank.Elder, YautjaBracerOwnerRank.Elder)]
    [TestCase(YautjaRank.Leader, YautjaBracerOwnerRank.Leader)]
    [TestCase(YautjaRank.Ancient, YautjaBracerOwnerRank.Admin)]
    public void CanonicalRankProjectsToLegacyBracerOwnerRank(YautjaRank rank, YautjaBracerOwnerRank expectedOwnerRank)
    {
        Assert.That(YautjaRankResolver.ToOwnerRank(rank), Is.EqualTo(expectedOwnerRank));
    }

    [TestCase(YautjaBracerOwnerRank.Unblooded, YautjaRank.Blooded)]
    [TestCase(YautjaBracerOwnerRank.Elite, YautjaRank.Elite)]
    [TestCase(YautjaBracerOwnerRank.Elder, YautjaRank.Elder)]
    [TestCase(YautjaBracerOwnerRank.Leader, YautjaRank.Leader)]
    [TestCase(YautjaBracerOwnerRank.Admin, YautjaRank.Ancient)]
    public void LegacyBracerOwnerRanksProjectToCanonicalHunterCompatibilityRank(
        YautjaBracerOwnerRank ownerRank,
        YautjaRank expectedRank)
    {
        Assert.That(YautjaRankResolver.FromOwnerRank(ownerRank), Is.EqualTo(expectedRank));
    }

    [Test]
    public void HumanoidProfileCloneAndEqualityKeepCanonicalClanRank()
    {
        var canonical = YautjaCharacterProfile.Default.WithClanRank(YautjaRank.Elder);
        var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
            .WithName("Kainde")
            .WithYautjaProfile(canonical);

        var clone = profile.Clone();
        var differentRank = profile.WithYautjaProfile(canonical.WithClanRank(YautjaRank.Leader));

        Assert.Multiple(() =>
        {
            Assert.That(clone.YautjaProfile.ClanRank, Is.EqualTo(YautjaRank.Elder));
            Assert.That(clone.MemberwiseEquals(profile), Is.True);
            Assert.That(profile.MemberwiseEquals(differentRank), Is.False);
        });
    }
}
