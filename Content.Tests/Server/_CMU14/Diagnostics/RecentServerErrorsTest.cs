using System;
using System.Linq;
using Content.Server.CMU14.Diagnostics;
using NUnit.Framework;
using Serilog.Events;
using Serilog.Parsing;

namespace Content.Tests.Server._CMU14.Diagnostics;

[TestFixture]
public sealed class RecentServerErrorsTest
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public void KeepsOnlyBoundedRecentErrorsAndDoesNotReplayReportedEntries()
    {
        var errors = new CMURecentServerErrors();
        errors.Log("runtime", Message(LogEventLevel.Error, "old", Now - TimeSpan.FromMinutes(2)));
        Assert.That(errors.Snapshot(Now - TimeSpan.FromMinutes(1), 0), Is.Empty);

        for (var i = 0; i < 20; i++)
            errors.Log("runtime", Message(LogEventLevel.Error, $"failure {i}", Now));

        var recent = errors.Snapshot(Now - TimeSpan.FromMinutes(1), 0);
        Assert.Multiple(() =>
        {
            Assert.That(recent.Count, Is.EqualTo(CMURecentServerErrors.Capacity));
            Assert.That(CMURecentServerErrors.Format(recent[0]), Is.EqualTo("failure 16"));
            Assert.That(CMURecentServerErrors.Format(recent[^1]), Is.EqualTo("failure 19"));
            Assert.That(errors.Snapshot(Now - TimeSpan.FromMinutes(1), recent[^1].Id), Is.Empty);
        });
        errors.Clear();
        Assert.That(errors.Snapshot(DateTimeOffset.MinValue, 0), Is.Empty);
    }

    [Test]
    public void IgnoresRoutineMessagesAndItsOwnReports()
    {
        var errors = new CMURecentServerErrors();
        errors.Log("runtime", Message(LogEventLevel.Warning, "warning", Now));
        errors.Log(CMUClientStateDiagnosticsSystem.SawmillName, Message(LogEventLevel.Error, "do not recurse", Now));
        Assert.That(errors.Snapshot(DateTimeOffset.MinValue, 0), Is.Empty);
    }

    [Test]
    public void PreservesExceptionAndInnerStackButBoundsRenderedText()
    {
        var errors = new CMURecentServerErrors();
        Exception failure;
        try
        {
            throw new InvalidOperationException("outer failure", new ArgumentException("inner failure"));
        }
        catch (Exception e)
        {
            failure = e;
        }

        errors.Log("runtime", Message(LogEventLevel.Error, "state application failed", Now, failure));
        var formatted = CMURecentServerErrors.Format(errors.Snapshot(DateTimeOffset.MinValue, 0).Single());
        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("state application failed"));
            Assert.That(formatted, Does.Contain("InvalidOperationException: outer failure"));
            Assert.That(formatted, Does.Contain("ArgumentException: inner failure"));
            Assert.That(formatted, Does.Contain(nameof(PreservesExceptionAndInnerStackButBoundsRenderedText)));
        });

        errors.Log("runtime", Message(LogEventLevel.Error, new string('x', CMURecentServerErrors.MaxTextLength + 100), Now));
        var large = CMURecentServerErrors.Format(errors.Snapshot(DateTimeOffset.MinValue, 0)[^1]);
        Assert.That(large.Length, Is.LessThan(CMURecentServerErrors.MaxTextLength + 100));
        Assert.That(large, Does.EndWith("[truncated; see original server error]"));
    }

    private static LogEvent Message(LogEventLevel level, string text, DateTimeOffset time, Exception exception = null)
    {
        return new LogEvent(time, level, exception, new MessageTemplateParser().Parse(text), Array.Empty<LogEventProperty>());
    }
}
