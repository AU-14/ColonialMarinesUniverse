using System.Collections.Generic;
using System.Reflection;
using Content.Client.Construction;
using Content.Client.UserInterface;
using Content.Shared.Input;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests;

public sealed class LocalizationWarningTest
{
    [Test]
    public async Task ClientDoesNotLookUpLiteralTextAsMessageIds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var construction = client.System<ConstructionSystem>();

        await client.WaitPost(() =>
        {
            var configuration = client.ResolveDependency<IConfigurationManager>();
            var localizationLog = client.ResolveDependency<ILogManager>().GetSawmill("loc");
            var previousLogLevel = localizationLog.Level;

            try
            {
                localizationLog.Level = LogLevel.Warning;
                configuration.SetCVar(RTCVars.FailureLogLevel, LogLevel.Warning);

                // Pooled clients suppress localization warnings during startup. Re-run the real cache warmup with
                // warnings promoted so passing literal recipe text to Loc.GetString fails this regression test.
                var cacheField = typeof(ConstructionSystem).GetField("_recipesMetadataCache",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var warmupMethod = typeof(ConstructionSystem).GetMethod("WarmupRecipesCache",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(cacheField, Is.Not.Null);
                Assert.That(warmupMethod, Is.Not.Null);

                var cache = (Dictionary<string, string>) cacheField!.GetValue(construction)!;
                cache.Clear();
                warmupMethod!.Invoke(construction, null);

                BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat);
            }
            finally
            {
                configuration.SetCVar(RTCVars.FailureLogLevel, LogLevel.Error);
                localizationLog.Level = previousLogLevel;
            }
        });

        await pair.CleanReturnAsync();
    }
}
