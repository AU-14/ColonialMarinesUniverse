using System.IO;
using System.Runtime.ExceptionServices;

namespace Content.IntegrationTests.Tests.Helpers;

[TestFixture]
public sealed class PrototypeStartupRetryTest
{
    [Test]
    public async Task RetryDisposesTheFailedInstanceBeforeCreatingItsReplacement()
    {
        var instances = new List<StartupInstance>();
        using var log = new StringWriter();
        using var result = await PrototypeStartup.Start(
            () =>
            {
                if (instances.Count > 0)
                    Assert.That(instances[^1].DisposeCount, Is.EqualTo(1));
                var instance = new StartupInstance();
                instances.Add(instance);
                return instance;
            },
            _ => instances.Count == 1 ? Task.FromException(VariantFreezeFailure()) : Task.CompletedTask,
            log);

        Assert.That(instances, Has.Count.EqualTo(2));
        Assert.That(result, Is.SameAs(instances[1]));
        Assert.That(result.DisposeCount, Is.Zero, "The successful instance belongs to the caller.");
        Assert.That(log.ToString(), Does.Contain("(1/3)"));
    }

    [Test]
    public void RepeatedStartupFailureIsPropagatedAfterThreeFreshAttempts()
    {
        var instances = new List<StartupInstance>();
        var failure = new AggregateException(new AggregateException(VariantFreezeFailure()));
        var thrown = Assert.ThrowsAsync<AggregateException>(async () => await PrototypeStartup.Start(
            () =>
            {
                var instance = new StartupInstance();
                instances.Add(instance);
                return instance;
            },
            _ => Task.FromException(failure),
            TextWriter.Null));

        Assert.That(thrown, Is.SameAs(failure));
        Assert.That(instances, Has.Count.EqualTo(3));
        Assert.That(instances, Has.All.Property(nameof(StartupInstance.DisposeCount)).EqualTo(1));
    }

    [TestCase("unrelated-null-key")]
    [TestCase("other-parameter")]
    [TestCase("other-exception")]
    [TestCase("mixed-aggregate")]
    public void OtherFailuresAreDisposedAndPropagatedWithoutRetry(string kind)
    {
        Exception failure = kind switch
        {
            "unrelated-null-key" => new ArgumentNullException("key"),
            "other-parameter" => VariantFreezeFailure("value"),
            "other-exception" => new InvalidOperationException("Startup error"),
            "mixed-aggregate" => new AggregateException(VariantFreezeFailure(), new InvalidOperationException()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var creations = 0;
        var instance = new StartupInstance();
        var thrown = Assert.CatchAsync<Exception>(async () => await PrototypeStartup.Start(
            () =>
            {
                creations++;
                return instance;
            },
            _ => Task.FromException(failure),
            TextWriter.Null));

        Assert.That(thrown, Is.SameAs(failure));
        Assert.That(creations, Is.EqualTo(1));
        Assert.That(instance.DisposeCount, Is.EqualTo(1));
    }

    private static Exception VariantFreezeFailure(string parameter = "key")
    {
        return ExceptionDispatchInfo.SetRemoteStackTrace(new ArgumentNullException(parameter),
            "   at Robust.Shared.Prototypes.PrototypeManager.KindData.Freeze()\n");
    }

    private sealed class StartupInstance : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
