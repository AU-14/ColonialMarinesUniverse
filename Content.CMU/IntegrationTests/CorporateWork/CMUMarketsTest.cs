using System.Linq;
using Content.Shared.GameTicking;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.CorporateWork;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.CorporateWork;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.CorporateWork;

[TestFixture]
[TestOf(typeof(CMUMarketsSystem))]
public sealed class CMUMarketsTest
{
    private const string Commodity = "CMUMarketTitanium";

    [Test]
    public async Task TradesMovePricesChargeFeesAndRespectCaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            // The market lives for the whole round, and test servers are reused between tests.
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var entities = server.EntMan;
            var markets = server.System<CMUMarketsSystem>();
            var corporate = server.System<CorporateConsoleSystem>();
            var terminal = entities.SpawnEntity("CMUCorporateMarketsTerminal", map.GridCoords);
            var desktop = entities.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var console = entities.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            var card = entities.SpawnEntity("AU14IDCardColonySynthetic", map.GridCoords);
            try
            {
                corporate.AddToCorporateBudget(100000);
                var wy = markets.GetTrader((terminal, entities.GetComponent<CMUMarketsTerminalComponent>(terminal)), card)!;
                Assert.That(wy.Corporate, Is.True);
                Assert.That(wy.Cap, Is.EqualTo(CMUMarketsSystem.CorporateHoldingsCap));

                // Nothing held yet, so nothing to sell.
                Assert.That(markets.TryTrade(Commodity, 1, false, wy, terminal, card), Is.Null);

                // A buy pushes the price up, and selling straight back loses the fees.
                var before = markets.GetPrice(Commodity)!.Value;
                var budget = corporate.GetCorporateBudget();
                var bought = markets.TryTrade(Commodity, 40, true, wy, terminal, card)!.Value;
                Assert.That(markets.GetPrice(Commodity)!.Value, Is.GreaterThan(before));
                Assert.That(corporate.GetCorporateBudget(), Is.EqualTo(budget + bought).Within(0.5f));
                Assert.That(markets.PayDividends(), Is.GreaterThan(0));
                var sold = markets.TryTrade(Commodity, 40, false, wy, terminal, card)!.Value;
                Assert.That(sold + bought, Is.LessThan(0));

                // The civilian app trades the card's bank account, under the smaller cap.
                var id = entities.GetComponent<IdCardComponent>(card);
                id.AccountBalance = 5000;
                var civ = markets.GetTrader((desktop, entities.GetComponent<CMUMarketsTerminalComponent>(desktop)), card)!;
                Assert.That(civ.Corporate, Is.False);
                Assert.That(civ.Cap, Is.EqualTo(CMUMarketsSystem.CivilianHoldingsCap));
                Assert.That(markets.TryTrade("CMUMarketSynthetics", CMUMarketsSystem.MaxTradeUnits, true, civ, desktop, card), Is.Null, "A civilian went past the holdings cap.");

                var paid = markets.TryTrade(Commodity, 10, true, civ, desktop, card)!.Value;
                Assert.That(id.AccountBalance, Is.EqualTo(5000 + paid));
                Assert.That(markets.HoldingsValue(civ.Key), Is.GreaterThan(0));
                Assert.That(markets.HoldingsValue(wy.Key), Is.Zero, "Civilian and corporate holdings were mixed.");

                // Company stock is for colonists only.
                Assert.That(markets.TryTrade("CMUStockArmat", 1, true, wy, terminal, card), Is.Null, "The corporate terminal bought stock.");
                Assert.That(markets.TryTrade("CMUStockArmat", 1, true, civ, desktop, card), Is.Not.Null);

                // Corporate events move the company's own stock.
                var stock = markets.GetPrice("CMUStockWeylandYutani")!.Value;
                markets.Shock("CMUStockWeylandYutani", -0.15f);
                Assert.That(markets.GetPrice("CMUStockWeylandYutani")!.Value, Is.EqualTo(stock * 0.85f).Within(0.01f));

                // An empty account can't buy.
                id.AccountBalance = 0;
                Assert.That(markets.TryTrade(Commodity, 1, true, civ, desktop, card), Is.Null);
            }
            finally
            {
                entities.DeleteEntity(terminal);
                entities.DeleteEntity(desktop);
                entities.DeleteEntity(console);
                entities.DeleteEntity(card);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CivilianNewsArrivesShortlyBeforeTheMove()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var markets = server.System<CMUMarketsSystem>();
        EntityUid terminal = default;
        TimeSpan movesAt = default;
        await server.WaitAssertion(() =>
        {
            // The market lives for the whole round, and test servers are reused between tests.
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            terminal = server.EntMan.SpawnEntity("CMUCorporateMarketsTerminal", map.GridCoords);
            movesAt = markets.BreakNews(Commodity, true);
            Assert.That(markets.GetVisibleNews(true), Is.Not.Empty, "The corporate terminal didn't get the news as it broke.");
            Assert.That(markets.GetVisibleNews(false), Is.Empty, "Civilians got the news early.");
        });

        var untilCivilian = movesAt - CMUMarketsSystem.CivilianNewsLead - server.Timing.CurTime;
        await server.WaitRunTicks((int) (untilCivilian.TotalSeconds / server.Timing.TickPeriod.TotalSeconds) + 2);
        await server.WaitAssertion(() =>
        {
            Assert.That(markets.GetVisibleNews(false), Is.Not.Empty, "Civilians never got the news.");
            Assert.That(server.Timing.CurTime, Is.LessThan(movesAt), "Civilians only got the news after the move started.");
            server.EntMan.DeleteEntity(terminal);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundEventsMoveCompanyStock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            // The market lives for the whole round, and test servers are reused between tests.
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var markets = server.System<CMUMarketsSystem>();
            var terminal = server.EntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            try
            {
                const string wy = "CMUStockWeylandYutani";
                const string chigusa = "CMUStockChigusa";
                markets.GetPrice(wy);

                // Research income lifts the company that earned it.
                var before = markets.GetPrice(wy)!.Value;
                markets.ReportIncome("WY", 1000);
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before * (1 + 10 * CMUMarketsSystem.IncomeShockPer100)).Within(0.05f));

                // Withdrawing halves the stock, and a rival in the sector that isn't in the round fills the gap.
                before = markets.GetPrice(wy)!.Value;
                var rivalBefore = markets.GetPrice(chigusa)!.Value;
                Assert.That(markets.IsInRound(chigusa), Is.False);
                markets.ReportSetback("WY", -0.5f, "Colonization", "cmu-markets-setback-withdrawal");
                Assert.Multiple(() =>
                {
                    Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before * 0.5f).Within(0.05f));
                    Assert.That(markets.GetPrice(chigusa)!.Value, Is.EqualTo(rivalBefore * (1 + CMUMarketsSystem.GapFill)).Within(0.05f));
                    Assert.That(markets.GetVisibleNews(false), Has.Count.EqualTo(2), "Colonists didn't hear about the setback at once.");
                    Assert.That(markets.GetVisibleNews(true), Is.Empty, "Stock news reached the corporate terminal.");
                });
            }
            finally
            {
                server.EntMan.DeleteEntity(terminal);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProfitLiftsTheCompanyButTradingDoesNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            // The market lives for the whole round, and test servers are reused between tests.
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var entities = server.EntMan;
            var markets = server.System<CMUMarketsSystem>();
            var corporate = server.System<CorporateConsoleSystem>();
            var terminal = entities.SpawnEntity("CMUCorporateMarketsTerminal", map.GridCoords);
            var console = entities.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            try
            {
                const string wy = "CMUStockWeylandYutani";
                corporate.AddToCorporateBudget(50000);
                markets.GetPrice(wy);
                markets.UpdateRoundEvents();

                // $1000 earned in a minute lifts the stock and makes the news.
                // Other tests on a reused server can leave stories behind, so count only the new one.
                var newsBefore = markets.GetVisibleNews(false).Count;
                var before = markets.GetPrice(wy)!.Value;
                corporate.AddToCorporateBudget(1000);
                markets.UpdateRoundEvents();
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before * (1 + 10 * CMUMarketsSystem.IncomeShockPer100)).Within(0.05f));
                Assert.That(markets.GetVisibleNews(false), Has.Count.EqualTo(newsBefore + 1), "The earnings didn't make the news.");

                // Trading on the corporate account isn't earnings.
                var trader = markets.GetTrader((terminal, entities.GetComponent<CMUMarketsTerminalComponent>(terminal)), terminal)!;
                markets.TryTrade(Commodity, 40, true, trader, terminal, terminal);
                markets.TryTrade(Commodity, 40, false, trader, terminal, terminal);
                markets.UpdateRoundEvents();
                before = markets.GetPrice(wy)!.Value;
                markets.TryTrade(Commodity, 40, true, trader, terminal, terminal);
                markets.TryTrade(Commodity, 40, false, trader, terminal, terminal);
                markets.UpdateRoundEvents();
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before).Within(0.01f), "Trading moved the company's stock.");

                // Nor is cash paid into the console: staff could pump the stock with their own money.
                var cash = entities.SpawnEntity("RMCSpaceCash1000", map.GridCoords);
                var budget = corporate.GetCorporateBudget();
                entities.EventBus.RaiseLocalEvent(console, new Content.Shared.Interaction.InteractUsingEvent(console, cash, console, entities.GetComponent<Robust.Shared.GameObjects.TransformComponent>(console).Coordinates));
                Assert.That(corporate.GetCorporateBudget(), Is.GreaterThan(budget), "The deposit didn't reach the budget.");
                markets.UpdateRoundEvents();
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before).Within(0.01f), "Depositing cash moved the company's stock.");
            }
            finally
            {
                entities.DeleteEntity(terminal);
                entities.DeleteEntity(console);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CasualtiesHitTheStock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            // The market lives for the whole round, and test servers are reused between tests.
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var entities = server.EntMan;
            var markets = server.System<CMUMarketsSystem>();
            var minds = server.System<Content.Server.Mind.MindSystem>();
            var roles = server.System<Content.Shared.Roles.SharedRoleSystem>();
            var mobState = server.System<Content.Shared.Mobs.Systems.MobStateSystem>();
            var terminal = entities.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var staff = new List<EntityUid>();
            var mindIds = new List<EntityUid>();
            try
            {
                const string wy = "CMUStockWeylandYutani";
                for (var i = 0; i < 4; i++)
                {
                    var body = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                    var mind = minds.CreateMind(null);
                    minds.TransferTo(mind, body, mind: mind.Comp);
                mindIds.Add(mind);
                    roles.MindAddJobRole(mind, mind.Comp, silent: true, jobPrototype: "AU14JobWYGuard");
                    staff.Add(body);
                }

                markets.GetPrice(wy);
                Assert.That(markets.CountCasualties(wy).Staff, Is.EqualTo(4));

                // The first casualty makes the newswire and knocks the stock down, once.
                var before = markets.GetPrice(wy)!.Value;
                mobState.ChangeMobState(staff[0], Content.Shared.Mobs.MobState.Dead);
                Assert.That(markets.GetVisibleNews(false), Has.Count.EqualTo(1), "The first casualty didn't make the newswire.");
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before * (1 + CMUMarketsSystem.FirstCasualtyShock)).Within(0.05f),
                    "The first casualty didn't move the stock.");

                // More deaths that can still be revived, even most of the staff, don't.
                before = markets.GetPrice(wy)!.Value;
                mobState.ChangeMobState(staff[1], Content.Shared.Mobs.MobState.Dead);
                mobState.ChangeMobState(staff[2], Content.Shared.Mobs.MobState.Dead);
                markets.UpdateRoundEvents();
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before).Within(0.01f), "Revivable losses moved the stock.");

                // One unrevivable is a quarter, not enough. Two of four is a mass-casualty event.
                var unrevivable = server.System<Content.Shared._RMC14.Medical.Unrevivable.RMCUnrevivableSystem>();
                unrevivable.MakeUnrevivable(staff[0]);
                markets.UpdateRoundEvents();
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before).Within(0.01f), "One unrevivable of four tanked the stock.");

                unrevivable.MakeUnrevivable(staff[1]);
                Assert.That(markets.CountCasualties(wy), Is.EqualTo((4, 3, 2)));
                markets.UpdateRoundEvents();
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before * (1 + CMUMarketsSystem.MassCasualtyShock)).Within(0.05f));

                // And only once.
                before = markets.GetPrice(wy)!.Value;
                markets.UpdateRoundEvents();
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before).Within(0.01f));
            }
            finally
            {
                entities.DeleteEntity(terminal);
                foreach (var body in staff)
                    entities.DeleteEntity(body);

                // Minds outlive their bodies and would count as casualties in the next test.
                foreach (var mind in mindIds)
                    entities.DeleteEntity(mind);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GhostedStaffCountAsLostAtCandleClose()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var markets = server.System<CMUMarketsSystem>();
        const string wy = "CMUStockWeylandYutani";
        var staff = new List<EntityUid>();
        var mindIds = new List<EntityUid>();
        EntityUid terminal = default;
        var ghosts = new List<EntityUid>();
        var before = 0f;

        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var minds = server.System<Content.Server.Mind.MindSystem>();
            var roles = server.System<Content.Shared.Roles.SharedRoleSystem>();
            var mobState = server.System<Content.Shared.Mobs.Systems.MobStateSystem>();
            terminal = entities.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            markets.GetPrice(wy);

            for (var i = 0; i < 4; i++)
            {
                var body = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                var mind = minds.CreateMind(null);
                minds.TransferTo(mind, body, mind: mind.Comp);
                mindIds.Add(mind);
                roles.MindAddJobRole(mind, mind.Comp, silent: true, jobPrototype: "AU14JobWYGuard");
                staff.Add(body);
            }

            // One dead, and two who ghosted out of their bodies for good: half the staff lost for good.
            mobState.ChangeMobState(staff[0], Content.Shared.Mobs.MobState.Dead);
            for (var i = 1; i <= 2; i++)
            {
                var ghostBody = entities.SpawnEntity("MobObserver", map.GridCoords);
                ghosts.Add(ghostBody);
                Assert.That(minds.TryGetMind(staff[i], out var ghostMind, out var ghostMindComp));
                minds.TransferTo(ghostMind, ghostBody, mind: ghostMindComp);
            }

            Assert.That(markets.CountCasualties(wy), Is.EqualTo((4, 3, 2)));
            before = markets.GetPrice(wy)!.Value;
        });

        // Let the market's own loop close a candle.
        await server.WaitRunTicks((int) (65 / server.Timing.TickPeriod.TotalSeconds));
        await server.WaitAssertion(() =>
        {
            Assert.That(markets.GetPrice(wy)!.Value, Is.LessThan(before * 0.85f), "The mass-casualty event didn't hit the stock at candle close.");
            foreach (var uid in staff)
                entities.DeleteEntity(uid);
            foreach (var mind in mindIds)
                entities.DeleteEntity(mind);
            foreach (var ghost in ghosts)
                entities.DeleteEntity(ghost);
            entities.DeleteEntity(terminal);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnclaimedGhostRoleDeathsCount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var markets = server.System<CMUMarketsSystem>();
            var mobState = server.System<Content.Shared.Mobs.Systems.MobStateSystem>();
            const string wy = "CMUStockWeylandYutani";
            var bodies = new List<EntityUid>();
            try
            {
                markets.GetPrice(wy);

                // Third-party bodies nobody has taken yet still work for the Company.
                for (var i = 0; i < 3; i++)
                    bodies.Add(entities.SpawnEntity("CMUJobPMCWhiteoutDogCatcher", map.GridCoords));

                Assert.That(markets.CountCasualties(wy).Staff, Is.EqualTo(3));
                Assert.That(markets.IsInRound(wy), Is.True);

                var news = markets.GetVisibleNews(false).Count;
                mobState.ChangeMobState(bodies[0], Content.Shared.Mobs.MobState.Dead);
                Assert.That(markets.GetVisibleNews(false), Has.Count.EqualTo(news + 1), "An unclaimed body's death didn't make the newswire.");

                mobState.ChangeMobState(bodies[1], Content.Shared.Mobs.MobState.Dead);
                Assert.That(markets.CountCasualties(wy).Dead, Is.EqualTo(2));

                // Left to rot past revival.
                server.System<Content.Shared._RMC14.Medical.Unrevivable.RMCUnrevivableSystem>().MakeUnrevivable(bodies[0]);
                Assert.That(markets.CountCasualties(wy).Permanent, Is.EqualTo(1));

                var before = markets.GetPrice(wy)!.Value;
                markets.UpdateRoundEvents();
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before * (1 + CMUMarketsSystem.MassCasualtyShock)).Within(0.05f),
                    "One of three unclaimed bodies unrevivable isn't a mass-casualty event.");
            }
            finally
            {
                foreach (var body in bodies)
                    entities.DeleteEntity(body);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("AU14JobTARSPartyRifleman", "CMUStockProdigy")]
    [TestCase("AU14VAISOEngi", "CMUStockVanguardsArrow")]
    public async Task OtherCompaniesThirdPartiesCount(string body, string stock)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var markets = server.System<CMUMarketsSystem>();
            var bodies = new List<EntityUid>();
            try
            {
                markets.GetPrice(stock);
                for (var i = 0; i < 3; i++)
                    bodies.Add(entities.SpawnEntity(body, map.GridCoords));

                Assert.That(markets.CountCasualties(stock).Staff, Is.EqualTo(3), $"{body} doesn't count as {stock} staff.");
                Assert.That(markets.IsInRound(stock), Is.True);
            }
            finally
            {
                foreach (var uid in bodies)
                    entities.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GibbedUnclaimedBodiesStayCasualties()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var markets = server.System<CMUMarketsSystem>();
            var mobState = server.System<Content.Shared.Mobs.Systems.MobStateSystem>();
            const string wy = "CMUStockWeylandYutani";
            var bodies = new List<EntityUid>();
            try
            {
                markets.GetPrice(wy);
                for (var i = 0; i < 4; i++)
                    bodies.Add(entities.SpawnEntity("CMUJobPMCWhiteoutDogCatcher", map.GridCoords));

                // Three blown apart: killed, then the bodies are gone.
                for (var i = 0; i < 3; i++)
                {
                    mobState.ChangeMobState(bodies[i], Content.Shared.Mobs.MobState.Dead);
                    entities.DeleteEntity(bodies[i]);
                }

                // An unclaimed deployment that expires is deleted alive, and isn't a casualty.
                entities.DeleteEntity(bodies[3]);

                Assert.That(markets.CountCasualties(wy), Is.EqualTo((3, 3, 3)));
                var before = markets.GetPrice(wy)!.Value;
                markets.UpdateRoundEvents();
                Assert.That(markets.GetPrice(wy)!.Value, Is.EqualTo(before * (1 + CMUMarketsSystem.MassCasualtyShock)).Within(0.05f),
                    "Gibbed bodies didn't make a mass-casualty event.");
            }
            finally
            {
                foreach (var body in bodies)
                {
                    if (entities.EntityExists(body))
                        entities.DeleteEntity(body);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Every team the corporate console can call works for a company with a stock listing, so
    ///     its casualties move that stock. Working Joes are equipment, not staff.
    /// </summary>
    [Test]
    public async Task CorporatePartiesAreLinkedToTheirStock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protos = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var listed = protos.EnumeratePrototypes<CMUMarketCommodityPrototype>()
                .Where(c => c.Kind == CMUMarketKind.Stock)
                .SelectMany(c => c.RoundForces)
                .ToHashSet();

            var checkedParties = 0;
            foreach (var console in protos.EnumeratePrototypes<Robust.Shared.Prototypes.EntityPrototype>())
            {
                if (console.Abstract || !console.TryGetComponent<Content.Shared.CMU14.ColonyEconomy.CorporateConsoleComponent>(out var comp, factory))
                    continue;

                foreach (var partyId in comp.CallableParties.Keys)
                {
                    Assert.That(protos.TryIndex<Content.Shared.CMU14.Threats.ThirdPartyPrototype>(partyId, out var party), $"{console.ID} calls unknown party {partyId}.");
                    Assert.That(protos.TryIndex(party!.PartySpawn, out var spawn), $"{partyId} has no party spawn.");
                    foreach (var member in spawn!.LeadersToSpawn.Keys.Concat(spawn.GruntsToSpawn.Keys))
                    {
                        Assert.That(protos.TryIndex<Robust.Shared.Prototypes.EntityPrototype>(member, out var body), $"{partyId} spawns unknown {member}.");
                        if (!body!.TryGetComponent<Content.Server.Ghost.Roles.Components.GhostRoleComponent>(out var role, factory) || role.JobProto is not { } jobId)
                            continue;

                        var job = protos.Index(jobId);
                        if (job.RoundForce == "WORKING_JOE")
                            continue;

                        Assert.That(job.RoundForce, Is.Not.Null.And.AnyOf(listed.Cast<object>().ToArray()),
                            $"{member} in {partyId} works for {job.RoundForce ?? "nobody"}, which has no stock listing.");
                        checkedParties++;
                    }
                }
            }

            Assert.That(checkedParties, Is.GreaterThan(0), "Found no corporate parties to check.");
        });

        await pair.CleanReturnAsync();
    }
}
