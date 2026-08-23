using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaRankSpawnTest
{
    [TestCase(YautjaRank.Unblooded, false)]
    [TestCase(YautjaRank.Blooded, false)]
    [TestCase(YautjaRank.Elite, false)]
    [TestCase(YautjaRank.Elder, false)]
    [TestCase(YautjaRank.Leader, true)]
    [TestCase(YautjaRank.Ancient, true)]
    public void NormalRanksUseHunterShipAndOnlySeniorRanksBypassSlots(YautjaRank rank, bool bypass)
    {
        var policy = YautjaPredatorRoundSystem.GetRankSpawnPolicy(rank);

        Assert.Multiple(() =>
        {
            Assert.That(policy.SpawnKind, Is.EqualTo(YautjaSpawnKind.HunterShipClan));
            Assert.That(policy.BypassSlotCap, Is.EqualTo(bypass));
        });
    }

    [Test]
    public void YoungbloodKeepsSpecialHuntingGroundSpawn()
    {
        var policy = YautjaPredatorRoundSystem.GetRankSpawnPolicy(YautjaRank.YoungBlood);

        Assert.Multiple(() =>
        {
            Assert.That(policy.SpawnKind, Is.EqualTo(YautjaSpawnKind.HuntingGroundsYoungblood));
            Assert.That(policy.BypassSlotCap, Is.False);
        });
    }

    [Test]
    public async Task HunterShipSpawnMarkersAreSeparatedByRankPool()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var factory = pair.Server.EntMan.ComponentFactory;
            var clan = prototypes.Index<EntityPrototype>("CMUHunterShipMarkerClanSpawn");
            var youngblood = prototypes.Index<EntityPrototype>("CMUHunterShipMarkerPredatorSpawn");

            Assert.Multiple(() =>
            {
                Assert.That(clan.TryGetComponent<YautjaPredatorSpawnPointComponent>(out var clanPoint, factory), Is.True);
                Assert.That(clanPoint!.Kind, Is.EqualTo(YautjaSpawnKind.HunterShipClan));
                Assert.That(youngblood.TryGetComponent<YautjaPredatorSpawnPointComponent>(out var youngbloodPoint, factory), Is.True);
                Assert.That(youngbloodPoint!.Kind, Is.EqualTo(YautjaSpawnKind.HuntingGroundsYoungblood));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void InvalidRankFallsBackToBloodedHunterShipPolicy()
    {
        var policy = YautjaPredatorRoundSystem.GetRankSpawnPolicy((YautjaRank) 99);

        Assert.Multiple(() =>
        {
            Assert.That(policy.SpawnKind, Is.EqualTo(YautjaSpawnKind.HunterShipClan));
            Assert.That(policy.BypassSlotCap, Is.False);
        });
    }

    [TestCase(3, 2, false)]
    [TestCase(2, 2, true)]
    [TestCase(0, 2, true)]
    [TestCase(2, 0, false)]
    public void OrdinaryRanksCannotConsumeSeniorReservations(int available, int bypassSlotsRemaining, bool expectedReserved)
    {
        Assert.That(
            YautjaPredatorRoundSystem.IsHunterSlotReservedForOrdinaryRank(available, bypassSlotsRemaining),
            Is.EqualTo(expectedReserved));
    }

    [Test]
    public void UnlimitedHunterSlotsAreNeverReserved()
    {
        Assert.That(
            YautjaPredatorRoundSystem.IsHunterSlotReservedForOrdinaryRank(null, 2),
            Is.False);
    }

    [TestCase(YautjaRank.Blooded, 2, 2, true)]
    [TestCase(YautjaRank.Elder, 2, 2, true)]
    [TestCase(YautjaRank.Leader, 2, 2, false)]
    [TestCase(YautjaRank.Blooded, 3, 2, false)]
    public void RoundStartCandidateFilterProtectsSeniorReservations(
        YautjaRank rank,
        int available,
        int bypassSlotsRemaining,
        bool expectedExcluded)
    {
        Assert.That(
            YautjaPredatorRoundSystem.ShouldExcludeOrdinaryRankFromHunterCandidates(
                rank,
                available,
                bypassSlotsRemaining),
            Is.EqualTo(expectedExcluded));
    }

    [TestCase(YautjaRank.Blooded, 2, 2, true)]
    [TestCase(YautjaRank.Elder, 2, 2, true)]
    [TestCase(YautjaRank.Leader, 0, 2, false)]
    [TestCase(YautjaRank.Blooded, 3, 2, false)]
    public void ExplicitHunterJobIsClearedOnlyForOrdinaryReservedRanks(
        YautjaRank rank,
        int available,
        int bypassSlotsRemaining,
        bool expectedCleared)
    {
        Assert.That(
            YautjaPredatorRoundSystem.ShouldClearExplicitHunterJob(
                rank,
                available,
                bypassSlotsRemaining),
            Is.EqualTo(expectedCleared));
    }
}
