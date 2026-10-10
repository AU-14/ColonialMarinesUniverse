using Content.Server.CMU14.Ambassador;
using Content.Shared.CMU14.Ambassador;
using Content.Shared.Interaction.Components;
using Robust.Server.GameObjects;

namespace Content.IntegrationTests.CMU14.Ambassador;

[TestFixture]
public sealed class CMUAmbassadorUpdateTest
{
    [Test]
    public async Task ClosedWindowsSkipProjectionsAndReopeningPublishesCurrentFactionState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var first = entities.SpawnEntity("AU14AmbassadorConsoleUPP", map.GridCoords);
            var second = entities.SpawnEntity("AU14AmbassadorConsoleUPP", map.GridCoords);
            var other = entities.SpawnEntity("AU14AmbassadorConsoleUA", map.GridCoords);
            var actor = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<ComplexInteractionComponent>(actor);
            var a = entities.GetComponent<AmbassadorConsoleComponent>(first);
            var b = entities.GetComponent<AmbassadorConsoleComponent>(second);
            var c = entities.GetComponent<AmbassadorConsoleComponent>(other);
            var system = entities.System<AmbassadorConsoleSystem>();
            var ui = entities.System<UserInterfaceSystem>();
            try
            {
                foreach (var comp in new[] { a, b })
                {
                    comp.Budget = 100;
                    comp.ReplenishInterval = 1;
                    comp.ReplenishAmount = 10;
                    comp.CalledParties.Add("already-called");
                }
                c.Budget = 200;
                var calledA = a.CalledParties;
                var calledB = b.CalledParties;
                system.Update(0.5f);
                system.Update(0.5f);
                Assert.Multiple(() =>
                {
                    Assert.That(a.Budget, Is.EqualTo(110));
                    Assert.That(b.Budget, Is.EqualTo(110), "two consoles must replenish a faction only once");
                    Assert.That(c.Budget, Is.EqualTo(200), "different factions keep independent budgets");
                    Assert.That(a.CalledParties, Is.SameAs(calledA));
                    Assert.That(b.CalledParties, Is.SameAs(calledB));
                    Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(second, AmbassadorConsoleUi.Key, out _), Is.False);
                    Assert.That(ui.TryGetUiState<AmbassadorThirdPartyBuiState>(second, AmbassadorThirdPartyUi.Key, out _), Is.False);
                });

                foreach (var comp in new[] { a, b })
                {
                    comp.EmbargoActive = true;
                    comp.EmbargoTimer = 59.5f;
                    comp.EmbargoCostPerMinute = 30;
                }
                system.Update(0.5f);
                Assert.That(a.Budget, Is.EqualTo(80));
                Assert.That(b.Budget, Is.EqualTo(80), "an upkeep payment must also apply once per faction");
                Assert.That(b.EmbargoTimer, Is.EqualTo(a.EmbargoTimer));

                Assert.That(ui.TryOpenUi(second, AmbassadorConsoleUi.Key, actor), Is.True);
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(second, AmbassadorConsoleUi.Key, out var initial), Is.True);
                Assert.That(initial!.Budget, Is.EqualTo(80));
                Assert.That(ui.TryGetUiState<AmbassadorThirdPartyBuiState>(second, AmbassadorThirdPartyUi.Key, out _), Is.False);
                ui.CloseUi(second, AmbassadorConsoleUi.Key, actor);
                a.Budget = b.Budget = 105;
                system.Update(0f);
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(second, AmbassadorConsoleUi.Key, out var closed), Is.True);
                Assert.That(closed, Is.SameAs(initial), "closed windows keep their projection without rebuilding it");
                Assert.That(ui.TryOpenUi(second, AmbassadorConsoleUi.Key, actor), Is.True);
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(second, AmbassadorConsoleUi.Key, out var reopened), Is.True);
                Assert.That(reopened!.Budget, Is.EqualTo(105));
                ui.CloseUi(second, AmbassadorConsoleUi.Key, actor);

                a.Budget = b.Budget = 205;
                Assert.That(ui.TryOpenUi(second, AmbassadorThirdPartyUi.Key, actor), Is.True);
                system.Update(0f);
                Assert.That(ui.TryGetUiState<AmbassadorThirdPartyBuiState>(second, AmbassadorThirdPartyUi.Key, out var parties), Is.True);
                Assert.That(parties!.Budget, Is.EqualTo(205));
                Assert.That(parties.CalledParties, Does.Contain("already-called"));
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(second, AmbassadorConsoleUi.Key, out closed), Is.True);
                Assert.That(closed, Is.SameAs(reopened), "opening the party window must not rebuild the closed main window");
            }
            finally
            {
                entities.DeleteEntity(actor);
                entities.DeleteEntity(first);
                entities.DeleteEntity(second);
                entities.DeleteEntity(other);
            }
        });
        await pair.CleanReturnAsync();
    }
}
