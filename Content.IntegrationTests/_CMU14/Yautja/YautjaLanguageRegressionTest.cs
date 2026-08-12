using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Robust.Server;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaLanguageRegressionTest
{
    private static readonly ProtoId<LanguagePrototype> YautjaLanguage = "Yautja";

    [Test]
    public async Task HellhoundUnderstandsYautja()
    {
        using var server = CreateServer();
        await server.WaitIdleAsync();
        EntityUid hellhound = default;

        await server.WaitPost(() =>
        {
            hellhound = server.EntMan.SpawnEntity("CMUMobYautjaHellhound", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            var languages = server.EntMan.GetComponent<LanguageComponent>(hellhound);
            Assert.That(languages.UnderstoodLanguages, Does.Contain(YautjaLanguage));
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(hellhound));
    }

    private static RobustIntegrationTest.ServerIntegrationInstance CreateServer()
    {
        return new RobustIntegrationTest.ServerIntegrationInstance(new RobustIntegrationTest.ServerIntegrationOptions
        {
            ContentStart = true,
            FailureLogLevel = LogLevel.Fatal,
            Options = new ServerOptions
            {
                LoadConfigAndUserData = false,
                LoadContentResources = true,
            },
            ContentAssemblies =
            [
                typeof(Shared.Entry.EntryPoint).Assembly,
                typeof(Server.Entry.EntryPoint).Assembly,
            ],
        });
    }
}
