using System.Linq;
using Content.Server._RMC14.Sentry;
using Content.Shared.AU14.AllianceConsole;
using Content.Shared._RMC14.Sentry;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Sentry;

[TestFixture]
public sealed class DropshipSentryRegressionTest
{
    private static readonly EntProtoId DropshipSentry = "RMCSentryDropship";
    private static readonly EntProtoId GovforAllianceConsole = "AU14AllianceConsoleGovfor";

    [Test]
    public async Task DropshipSentryIsShootableAndInheritsStoredAllianceState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var targetingSystem = server.System<SentryTargetingSystem>();
            var sentry = entities.SpawnEntity(DropshipSentry, testMap.GridCoords);
            var console = entities.SpawnEntity(GovforAllianceConsole, testMap.GridCoords);

            try
            {
                var fixtures = entities.GetComponent<FixturesComponent>(sentry);
                Assert.That(fixtures.Fixtures.Values.Any(fixture =>
                    (fixture.CollisionLayer & (int) CollisionGroup.BulletImpassable) != 0), Is.True);

                entities.EventBus.RaiseLocalEvent(console,
                    new AllianceConsoleSetFactionStatusMsg("CLF", AllianceStatus.Friendly));

                Assert.That(targetingSystem.TryApplyDefaultFaction(sentry, "GOVFOR"), Is.True);
                var targeting = entities.GetComponent<SentryTargetingComponent>(sentry);
                Assert.That(targeting.AllianceFriendlyNpcFactions.Select(faction => faction.Id),
                    Does.Contain("CLF"));
            }
            finally
            {
                entities.DeleteEntity(sentry);
                entities.DeleteEntity(console);
            }
        });

        await pair.CleanReturnAsync();
    }
}
