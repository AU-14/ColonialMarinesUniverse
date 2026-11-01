using Content.Server.CMU14.Ambassador;
using Content.Shared.CMU14.Ambassador;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ThirdParty;

[TestFixture]
public sealed class AmbassadorConsoleUiRefreshTest
{
    // the console used to rebuild and resend its BUI state every tick. it should only
    // publish when someone's looking and something they can see actually changed
    [Test]
    public async Task AmbassadorConsolePublishesOnlyOnChangeWhileOpen()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var system = entities.System<AmbassadorConsoleSystem>();
            var ui = entities.System<UserInterfaceSystem>();
            var console = entities.SpawnEntity("AU14AmbassadorConsoleUA", map.GridCoords);
            var actor = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var comp = entities.GetComponent<AmbassadorConsoleComponent>(console);

            try
            {
                system.Update(0f);
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(console, AmbassadorConsoleUi.Key, out _), Is.False,
                    "a closed console shouldn't publish anything");

                Assert.That(ui.TryOpenUi(console, AmbassadorConsoleUi.Key, actor), Is.True);
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(console, AmbassadorConsoleUi.Key, out var initial), Is.True);

                system.Update(0f);
                system.Update(0f);
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(console, AmbassadorConsoleUi.Key, out var idle), Is.True);
                Assert.That(idle, Is.SameAs(initial), "nothing changed, so the state shouldn't be rebuilt");

                comp.ReplenishTimer = comp.ReplenishInterval;
                system.Update(0f);
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(console, AmbassadorConsoleUi.Key, out var replenished), Is.True);
                Assert.That(replenished!.Budget, Is.EqualTo(initial!.Budget + comp.ReplenishAmount));

                ui.CloseUi(console, AmbassadorConsoleUi.Key, actor);
                comp.ReplenishTimer = comp.ReplenishInterval;
                system.Update(0f);
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(console, AmbassadorConsoleUi.Key, out var closed), Is.True);
                Assert.That(closed, Is.SameAs(replenished), "budget still ticks but a closed UI isn't republished");

                Assert.That(ui.TryOpenUi(console, AmbassadorConsoleUi.Key, actor), Is.True);
                Assert.That(ui.TryGetUiState<AmbassadorConsoleBuiState>(console, AmbassadorConsoleUi.Key, out var reopened), Is.True);
                Assert.That(reopened!.Budget, Is.EqualTo(comp.Budget), "reopening catches up on what was missed");
            }
            finally
            {
                entities.DeleteEntity(console);
                entities.DeleteEntity(actor);
            }
        });

        await pair.CleanReturnAsync();
    }
}
