using Robust.Client.UserInterface.Controls;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.CorporateRelations;
using Content.Client.CMU14.CorporateWork;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.CorporateRelations;
using Content.Shared.CMU14.CorporateWork;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.CorporateWork;

/// <summary>
///     Every corporate window builds, takes a full state from each side and lays out without
///     errors. XAML names and dynamic rows only fail at runtime, so this is the cheap catch.
/// </summary>
[TestFixture, NonParallelizable]
public sealed class CMUCorporateWindowsTest : GameTest
{
    private static readonly Vector2 WindowSize = new(560, 600);

    [TestCase(CMUContractsSide.Corporate)]
    [TestCase(CMUContractsSide.Colony)]
    [TestCase(CMUContractsSide.Ambassador)]
    public async Task ContractsWindowShowsEverySide(CMUContractsSide side)
    {
        await Client.WaitAssertion(() =>
        {
            var window = new CMUContractsWindow();
            var borrower = side switch
            {
                CMUContractsSide.Colony => "colony",
                CMUContractsSide.Ambassador => "UPP",
                _ => null,
            };

            window.UpdateState(new CMUContractsBuiState
            {
                Side = side,
                Borrower = borrower,
                CorporateBudget = 123456,
                BorrowerBudget = 4321,
                NextCycleAt = TimeSpan.FromMinutes(20),
                Rent = 1500,
                RentCap = 1500,
                CorporateArrears = 750,
                ResearchLicense = false,
                TaxBreak = false,
                WorkOrdersFrozen = false,
                WithdrawalAt = TimeSpan.FromMinutes(5),
                Withdrawn = false,
                MaxPremiumPercent = 40,
                MaxWorkOrders = 3,
                MaxLoan = 10000,
                Goods = new List<CMUWorkOrderGoodInfo> { new("CMUWorkOrderSteelOre", "cmu-work-order-good-steel-ore", 10, 200) },
                WorkOrders = new List<CMUWorkOrderInfo>
                {
                    new(0, "cmu-work-order-good-salvage-rare", 20, 5, 40, TimeSpan.FromMinutes(10)),
                    new(1, "cmu-work-order-good-cotton", 200, 150, 25, TimeSpan.FromMinutes(10)),
                },
                Loans = new List<CMULoanInfo>
                {
                    new("colony", "the colony", CMULoanStage.Active, 1500, 0, 1000, 2, true),
                    new("UPP", "UPP", CMULoanStage.AwaitingCorporate, 9999, 100, 0, 0, false),
                    new("UA", "UA", CMULoanStage.AwaitingBorrower, 5000, 35, 0, 0, false),
                },
            });

            AssertLaysOut(window, "contracts");
        });
    }

    [Test]
    public async Task MinigameWindowsShowCases()
    {
        await Client.WaitAssertion(() =>
        {
            var audit = new CMUAuditTerminalWindow();
            var entries = Enumerable.Range(1, 12)
                .Select(i => new CMUAuditEntry($"cmu-audit-item-{i}", $"cmu-audit-department-{i % 7 + 1}", 2500))
                .ToList();
            audit.UpdateState(new CMUAuditTerminalBuiState(
                new List<CMUAuditTierInfo>
                {
                    new("cmu-audit-tier-routine", 6, 1, 100),
                    new("cmu-audit-tier-standard", 9, 2, 250),
                    new("cmu-audit-tier-forensic", 12, 3, 400),
                },
                2, entries, entries, TimeSpan.Zero, null));
            AssertLaysOut(audit, "audit");

            var logistics = new CMULogisticsTerminalWindow();
            logistics.UpdateState(new CMULogisticsTerminalBuiState(
                new List<CMULogisticsTierInfo>
                {
                    new("cmu-logistics-tier-light", 6, 75),
                    new("cmu-logistics-tier-standard", 8, 125),
                    new("cmu-logistics-tier-heavy", 10, 200),
                },
                2,
                Enumerable.Range(1, 10).Select(i => new CMULogisticsCrate($"cmu-logistics-cargo-{i}", 10, 3, 1080)).ToList(),
                40, 12, TimeSpan.Zero, null));
            AssertLaysOut(logistics, "logistics");

            var transmissions = new CMUTransmissionsTerminalWindow();
            transmissions.UpdateState(new CMUTransmissionsTerminalBuiState(
                new List<CMUTransmissionsTierInfo>
                {
                    new("cmu-transmissions-tier-commercial", 4, 6, 10, 300),
                    new("cmu-transmissions-tier-military", 4, 8, 10, 450),
                    new("cmu-transmissions-tier-blacksite", 5, 8, 10, 600),
                },
                2,
                Enumerable.Range(0, 9).Select(_ => new CMUTransmissionsAttempt(new[] { 0, 1, 2, 3, 4 }, 2, 2)).ToList(),
                TimeSpan.Zero, null));
            AssertLaysOut(transmissions, "transmissions");

            // A random walk of candles, with a few news markers, at both trader types.
            var random = new Random(4);
            var candles = new List<CMUMarketCandle>();
            var price = 120f;
            for (var i = 0; i < 60; i++)
            {
                var close = price * (1 + (float) (random.NextDouble() - 0.5) * 0.08f);
                candles.Add(new CMUMarketCandle(price, MathF.Max(price, close) * 1.01f, MathF.Min(price, close) * 0.99f, close, random.Next(0, 40)));
                price = close;
            }

            foreach (var corporate in new[] { true, false })
            {
                var markets = new CMUMarketsTerminalWindow();
                markets.UpdateState(new CMUMarketsTerminalBuiState(
                    new List<CMUMarketQuote>
                    {
                        new("CMUMarketPhoron", CMUMarketKind.Goods, "PHR", "cmu-markets-commodity-phoron", price, 3.2f, 12, 118, candles, new List<int> { 10, 42 }),
                        new("CMUMarketSynthetics", CMUMarketKind.Goods, "SYN", "cmu-markets-commodity-synthetics", 140, -6.7f, 0, 0, candles, new List<int>()),
                        new("CMUMarketAtmospherics", CMUMarketKind.Goods, "ATM", "cmu-markets-commodity-atmospherics", 40, 0, 0, 0, new List<CMUMarketCandle>(), new List<int>()),
                        new("CMUStockWeylandYutani", CMUMarketKind.Stock, "WY", "cmu-markets-stock-weyland-yutani", 318, 1.1f, 2, 300, candles, new List<int>()),
                    },
                    Enumerable.Range(0, 8).Select(_ => new CMUMarketNews(TimeSpan.FromMinutes(83), "[color=#ff4e5e]▼ SYN[/color] Synthetic labour expected to fall: regulators have opened an inquiry.")).ToList(),
                    corporate,
                    corporate ? "Weyland-Yutani corporate account" : "Account #48213",
                    12500, 1572, corporate ? 10000 : 1500, 2f, 0.5f, TimeSpan.Zero));
                AssertLaysOut(markets, "markets");

                // Headlines carry colour markup. Fed in as plain text it gets escaped and shows up
                // on screen as literal [color=...] tags.
                foreach (var row in markets.NewsRows.Children.OfType<RichTextLabel>())
                    Assert.That(row.Text, Does.Not.Contain("\\["), "A news line rendered its markup as text.");
            }
        });
    }

    private static void AssertLaysOut(DefaultWindow window, string name)
    {
        Assert.DoesNotThrow(() =>
        {
            window.Contents.Measure(WindowSize);
            window.Contents.Arrange(UIBox2.FromDimensions(Vector2.Zero, WindowSize));
        }, $"The {name} window failed to lay out.");
    }
}
