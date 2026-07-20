#nullable enable
using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Pair;
using Content.IntegrationTests.Tests;
using Content.IntegrationTests.Tests.Destructible;
using Content.IntegrationTests.Tests.DeviceNetwork;
using Content.IntegrationTests.Tests.Interaction.Click;
using Content.Shared._RMC14.Prototypes;
using Robust.Client;
using Robust.Server;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.IntegrationTests;

// The static class exist to avoid breaking changes
public static partial class PoolManager
{
    public static readonly ContentPoolManager Instance = new();
    public const string TestMap = "Empty";

    /// <summary>
    /// Designated load bearing station. Sometimes you need a station for a test.
    /// </summary>
    public const string TestStation = "Saltern";

    /// <summary>
    /// Runs a server, or a client until a condition is true
    /// </summary>
    /// <param name="instance">The server or client</param>
    /// <param name="func">The condition to check</param>
    /// <param name="maxTicks">How many ticks to try before giving up</param>
    /// <param name="tickStep">How many ticks to wait between checks</param>
    public static async Task WaitUntil(RobustIntegrationTest.IntegrationInstance instance, Func<bool> func,
        int maxTicks = 600,
        int tickStep = 1)
    {
        await WaitUntil(instance, async () => await Task.FromResult(func()), maxTicks, tickStep);
    }

    /// <summary>
    /// Runs a server, or a client until a condition is true
    /// </summary>
    /// <param name="instance">The server or client</param>
    /// <param name="func">The async condition to check</param>
    /// <param name="maxTicks">How many ticks to try before giving up</param>
    /// <param name="tickStep">How many ticks to wait between checks</param>
    public static async Task WaitUntil(RobustIntegrationTest.IntegrationInstance instance, Func<Task<bool>> func,
        int maxTicks = 600,
        int tickStep = 1)
    {
        var ticksAwaited = 0;
        bool passed;

        await instance.WaitIdleAsync();

        while (!(passed = await func()) && ticksAwaited < maxTicks)
        {
            var ticksToRun = tickStep;

            if (ticksAwaited + tickStep > maxTicks)
            {
                ticksToRun = maxTicks - ticksAwaited;
            }

            await instance.WaitRunTicks(ticksToRun);

            ticksAwaited += ticksToRun;
        }

        if (!passed)
        {
            Assert.Fail($"Condition did not pass after {maxTicks} ticks.\n" +
                        $"Tests ran ({instance.TestsRan.Count}):\n" +
                        $"{string.Join('\n', instance.TestsRan)}");
        }

        Assert.That(passed);
    }

    public static async Task<TestPair> GetServerClient(
        PoolSettings? settings = null,
        ITestContextLike? testContext = null)
    {
        return await Instance.GetPair(settings, testContext);
    }

    public static void Startup(params Assembly[] extra)
        => Instance.Startup(extra);

        _testPrototypes.Clear();
        CMPrototypeExtensions.FilterCM = false;
        DiscoverTestPrototypes(typeof(PoolManager).Assembly);
        foreach (var assembly in extraAssemblies)
        {
            DiscoverTestPrototypes(assembly);
        }
    }
}
