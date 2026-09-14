using System;
using System.IO;
using Content.Server.CMU14.PersistentEconomy;
using NUnit.Framework;

namespace Content.Tests.CMU.PersistentEconomy;

// CMU14: exercise actual database transactions, including duplicate and failed commands.
[TestFixture]
public sealed class CMUEconomyStoreTest
{
    private CMUEconomyStore _store;
    private Guid _player;

    [SetUp]
    public void Setup()
    {
        SQLitePCL.Batteries_V2.Init();
        _store = new CMUEconomyStore(":memory:");
        _player = Guid.NewGuid();
        Assert.That(_store.Mutate(_player, 1, "seed", op => op.Change(8000, "AdminAdjustment", "Fixture")), Is.True);
    }

    [TearDown]
    public void TearDown() => _store.Dispose();

    [Test]
    public void DocumentExampleAndGrossDepositLimit()
    {
        Assert.That(_store.Mutate(_player, 1, "deployment", op => op.Deploy(500, 10, 2000, 200, "rifleman")), Is.True);
        var round = _store.ReadRound(_player, 1);
        Assert.Multiple(() =>
        {
            Assert.That(_store.ReadAccount(_player).Balance, Is.EqualTo(6750));
            Assert.That(round.Stake, Is.EqualTo(750));
            Assert.That(round.SettlementCap, Is.EqualTo(1500));
        });
        Assert.That(_store.Mutate(_player, 1, "deposit", op => op.DepositToEscrow(400)), Is.True);
        Assert.That(_store.Mutate(_player, 1, "withdraw", op => op.Change(-400, "CashWithdrawal", "ATM")), Is.True);
        Assert.That(_store.Mutate(_player, 1, "over-cap", op => op.DepositToEscrow(1101)), Is.False);
        Assert.That(_store.Mutate(_player, 1, "salary", op => op.Change(650, "Payroll", "Service")), Is.True);
        Assert.That(_store.Mutate(_player, 1, "settle", op => op.Settle(1100)), Is.True);
        Assert.That(_store.ReadAccount(_player).Balance, Is.EqualTo(8500));
        Assert.That(_store.ReadRound(_player, 1).CashCredited, Is.EqualTo(1500));
        Assert.That(_store.ReadAccount(_player).LifetimeSpent, Is.EqualTo(500));
    }

    [Test]
    public void DeploymentAndPurchaseCannotBeRepeatedWithNewKeys()
    {
        Assert.That(_store.Mutate(_player, 1, "deploy", op => op.Deploy(0, 10, 2000, 200, "job")), Is.True);
        Assert.That(_store.Mutate(_player, 1, "reconnect", op => op.Deploy(0, 10, 2000, 200, "job")), Is.False);
        Assert.That(_store.Mutate(_player, 1, "buy", op => op.Buy("scarf", 100)), Is.True);
        Assert.That(_store.Mutate(_player, 1, "buy-again", op => op.Buy("scarf", 100)), Is.False);
        Assert.That(_store.ReadAccount(_player).Balance, Is.EqualTo(7100));
    }

    [Test]
    public void TransferIsConservedAndDuplicateKeysAreRejected()
    {
        var recipient = Guid.NewGuid();
        Assert.That(_store.Mutate(_player, 1, "transfer", op => op.Transfer(recipient, 3000)), Is.True);
        Assert.That(_store.Mutate(_player, 1, "transfer", op => op.Transfer(recipient, 3000)), Is.False);
        Assert.That(_store.Mutate(_player, 1, "too-much", op => op.Transfer(recipient, 6000)), Is.False);
        Assert.That(_store.Mutate(_player, 1, "negative", op => op.Transfer(recipient, -1)), Is.False);
        Assert.That(_store.Mutate(_player, 1, "self", op => op.Transfer(_player, 1)), Is.False);
        Assert.That(_store.ReadAccount(_player).Balance + _store.ReadAccount(recipient).Balance, Is.EqualTo(8000));
    }

    [Test]
    public void FailedMultiStepOperationRollsBackBalancesAndLedger()
    {
        var recipient = Guid.NewGuid();
        Assert.That(_store.Mutate(recipient, 1, "max", op => op.Change(CMUEconomyStore.MaximumBalance, "AdminAdjustment", "Fixture")), Is.True);
        Assert.That(_store.Mutate(_player, 1, "overflow-recipient", op => op.Transfer(recipient, 100)), Is.False);
        Assert.That(_store.ReadAccount(_player).Balance, Is.EqualTo(8000));
        Assert.That(_store.History(_player).Count, Is.EqualTo(1));
        Assert.Throws<InvalidOperationException>(() => _store.Mutate(_player, 1, "spawn-failure", op =>
        {
            op.Deploy(500, 10, 2000, 200, "job");
            throw new InvalidOperationException("Spawn failed");
        }));
        Assert.That(_store.ReadRound(_player, 1).DeploymentIssued, Is.False);
        Assert.That(_store.ReadAccount(_player).Balance, Is.EqualTo(8000));
    }

    [Test]
    public void ZeroStakeStillAllowsSalaryAndTransfers()
    {
        Assert.That(_store.Mutate(_player, 1, "optout", op => { op.Account.StakeEnabled = false; return true; }), Is.True);
        Assert.That(_store.Mutate(_player, 1, "deploy", op => op.Deploy(0, 10, 2000, 200, "job")), Is.True);
        Assert.That(_store.Mutate(_player, 1, "cash", op => op.DepositToEscrow(1)), Is.False);
        Assert.That(_store.Mutate(_player, 1, "salary", op => op.Change(6, "Payroll", "Minute")), Is.True);
        Assert.That(_store.ReadAccount(_player).Balance, Is.EqualTo(8006));
    }

    [TestCase(2000, 2000)]
    [TestCase(0, 10000)]
    public void StakeCapCanBeDisabled(int cap, long expected)
    {
        var player = Guid.NewGuid();
        _store.Mutate(player, 1, "rich", op => op.Change(100000, "AdminAdjustment", "Fixture"));
        Assert.That(_store.Mutate(player, 1, "rich-deploy", op => op.Deploy(0, 10, cap, 200, "job")), Is.True);
        Assert.That(_store.ReadRound(player, 1).Stake, Is.EqualTo(expected));
    }

    [Test]
    public void SettlementClosesCashPathAndDoesNotConsumePayrollCapacity()
    {
        _store.Mutate(_player, 1, "deploy", op => op.Deploy(0, 10, 2000, 200, "job"));
        Assert.That(_store.Mutate(_player, 1, "escrow", op => op.DepositToEscrow(400)), Is.True);
        Assert.That(_store.Mutate(_player, 1, "settle-part", op => op.Settle(0)), Is.True);
        Assert.That(_store.Mutate(_player, 1, "after-settlement", op => op.DepositToEscrow(1)), Is.False);
        Assert.That(_store.ReadRound(_player, 1).Settled, Is.True);
        Assert.That(_store.Mutate(_player, 1, "repeat-settlement", op => op.Settle(400)), Is.False);
        Assert.That(_store.Mutate(_player, 1, "award", op => op.Change(100, "HazardPay", "Verified event")), Is.True);
        Assert.That(_store.ReadRound(_player, 1).CashCredited, Is.EqualTo(400));
    }

    [Test]
    public void EscrowWithdrawalRestoresCashCapacityWithoutCreditingBank()
    {
        _store.Mutate(_player, 1, "deploy", op => op.Deploy(0, 10, 2000, 200, "job"));
        Assert.That(_store.Mutate(_player, 1, "escrow", op => op.DepositToEscrow(600)), Is.True);
        Assert.That(_store.Mutate(_player, 1, "withdraw-escrow", op => op.WithdrawEscrow(250)), Is.True);

        var round = _store.ReadRound(_player, 1);
        Assert.Multiple(() =>
        {
            Assert.That(round.RoundEscrow, Is.EqualTo(350));
            Assert.That(round.CashCredited, Is.Zero);
            Assert.That(_store.ReadAccount(_player).Balance, Is.EqualTo(7200));
        });
        Assert.That(_store.Mutate(_player, 1, "withdraw-too-much", op => op.WithdrawEscrow(351)), Is.False);
    }

    [Test]
    public void InvalidDeploymentAndAmountInputsDoNotChangeAccount()
    {
        Assert.That(_store.Mutate(_player, 1, "expensive", op => op.Deploy(8001, 10, 2000, 200, "job")), Is.False);
        Assert.That(_store.Mutate(_player, 1, "invalid-percent", op => op.Deploy(0, 101, 2000, 200, "job")), Is.False);
        Assert.That(_store.Mutate(_player, 1, "underflow", op => op.Change(long.MinValue, "AdminAdjustment", "Bad input")), Is.False);
        Assert.That(_store.Mutate(_player, 1, "overflow", op => op.Change(long.MaxValue, "AdminAdjustment", "Bad input")), Is.False);
        Assert.That(_store.ReadAccount(_player).Balance, Is.EqualTo(8000));
        Assert.That(_store.ReadRound(_player, 1).DeploymentIssued, Is.False);
        Assert.That(_store.History(_player).Count, Is.EqualTo(1));
    }

    [Test]
    public void DatabaseReopenPreservesEscrowLimitsAndPurchases()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cmu-economy-test-{Guid.NewGuid()}.sqlite");
        try
        {
            using (var store = new CMUEconomyStore(path))
            {
                store.Mutate(_player, 5, "seed", op => op.Change(8000, "AdminAdjustment", "Fixture"));
                store.Mutate(_player, 5, "deploy", op => op.Deploy(0, 10, 2000, 200, "job"));
                store.Mutate(_player, 5, "deposit", op => op.DepositToEscrow(300));
                store.Mutate(_player, 5, "purchase", op => op.Buy("scarf", 100));
            }
            using (var store = new CMUEconomyStore(path))
            {
                Assert.That(store.ReadRound(_player, 5).RoundEscrow, Is.EqualTo(300));
                Assert.That(store.RoundPlayers(5), Does.Contain(_player));
                Assert.That(store.ReadAccount(_player).Purchases.ContainsKey("scarf"), Is.True);
                Assert.That(store.Mutate(_player, 5, "deposit", op => op.DepositToEscrow(300)), Is.False);
                Assert.That(store.Mutate(_player, 5, "new-deploy", op => op.Deploy(0, 10, 2000, 200, "job")), Is.False);
                Assert.That(store.Mutate(_player, 6, "next-round", op => op.Deploy(0, 10, 2000, 200, "job")), Is.True);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                File.Delete(path + suffix);
        }
    }
}
