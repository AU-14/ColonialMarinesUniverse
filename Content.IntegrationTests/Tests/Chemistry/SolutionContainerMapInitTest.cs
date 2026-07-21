#nullable enable
using Content.IntegrationTests.Fixtures;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
public sealed class SolutionContainerMapInitTest : GameTest
{
    private const string ExistingSolutionWarning = "Attempted to port a solution id";

    private static readonly EntProtoId[] AffectedPrototypes =
    [
        "CMBeaker",
        "CMBeakerLarge",
        "CMDrinkCanBobdaClassic",
        "CMFireExtinguisher",
        "CMJumpsuitBO",
        "CMMarinePreparedMealChicken",
        "CMMarinePreparedMealCornbread",
        "CMMarinePreparedMealPasta",
        "CMMarinePreparedMealPizza",
        "CMMarinePreparedMealPork",
        "CMMarinePreparedMealTofu",
        "CMPillDexalin",
        "CMPillDylovene",
        "RMCBeakerHighCapacity",
        "RMCBucket",
        "RMCMobMouseDoc",
        "RMCTankReagentEmpty",
        "RMCTankReagentFuel",
        "RMCTankReagentWater",
    ];

    [Test]
    public async Task AffectedPrototypesDoNotPortExistingSolutionsOnMapInit()
    {
        var testMap = await Pair.CreateTestMap();
        var rootLog = Server.ResolveDependency<ILogManager>().RootSawmill;
        var logCatcher = new LogCatcher();

        rootLog.AddHandler(logCatcher);
        try
        {
            await Server.WaitAssertion(() =>
            {
                foreach (var prototype in AffectedPrototypes)
                {
                    var entity = SSpawnAtPosition(prototype, testMap.GridCoords);
                    Assert.That(Server.MetaData(entity).EntityLifeStage, Is.EqualTo(EntityLifeStage.MapInitialized));
                }
            });

            var warnings = logCatcher.CaughtLogs
                .Where(log => log.Level == LogEventLevel.Warning)
                .Select(log => log.RenderMessage())
                .Where(message => message.Contains(ExistingSolutionWarning, StringComparison.Ordinal))
                .ToArray();

            Assert.That(warnings, Is.Empty, string.Join(Environment.NewLine, warnings));
        }
        finally
        {
            rootLog.RemoveHandler(logCatcher);
        }
    }
}
