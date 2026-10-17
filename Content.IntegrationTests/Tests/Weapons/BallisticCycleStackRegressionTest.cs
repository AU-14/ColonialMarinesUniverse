using Content.IntegrationTests.Fixtures;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Weapons;

[TestFixture]
[TestOf(typeof(SharedGunSystem))]
public sealed class BallisticCycleStackRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: BallisticCycleNonStackAmmoProvider
          components:
          - type: BallisticAmmoProvider
            capacity: 1
            proto: BulletFoam
        """;

    [Test]
    public async Task CyclingUnspawnedNonStackAmmoDoesNotLogMissingStackError()
    {
        var map = await Pair.CreateTestMap();
        var missingStackErrors = 0;

        bool JudgeMissingStackError(string sawmill, LogEvent message)
        {
            if (sawmill != "resolve" ||
                message.Level != LogEventLevel.Error ||
                !message.RenderMessage().Contains("Content.Shared.Stacks.StackComponent", StringComparison.Ordinal))
            {
                return false;
            }

            missingStackErrors++;
            return true;
        }

        Pair.ServerLogHandler.JudgeLog += JudgeMissingStackError;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                var provider = SEntMan.SpawnEntity("BallisticCycleNonStackAmmoProvider", map.GridCoords);
                var providerComponent = SEntMan.GetComponent<BallisticAmmoProviderComponent>(provider);

                var use = new UseInHandEvent(user);
                SEntMan.EventBus.RaiseLocalEvent(provider, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(providerComponent.UnspawnedCount, Is.Zero);
                    Assert.That(missingStackErrors, Is.Zero,
                        "cycling non-stack ammunition must not try to resolve StackComponent");
                });

                SEntMan.DeleteEntity(provider);
                SEntMan.DeleteEntity(user);
            });
        }
        finally
        {
            Pair.ServerLogHandler.JudgeLog -= JudgeMissingStackError;
        }
    }
}
