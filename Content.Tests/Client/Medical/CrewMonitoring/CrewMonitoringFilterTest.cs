using System.Collections.Generic;
using Content.Client.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using NUnit.Framework;

namespace Content.Tests.Client.Medical.CrewMonitoring;

[TestFixture]
public sealed class CrewMonitoringFilterTest
{
    [Test]
    public void MatchesNameJobAndAreaCaseInsensitively()
    {
        var status = new SuitSensorStatus(default, default, "Kukulkan", "Blooded", "JobIconNoId", new List<string>())
        {
            Area = "Main Ship"
        };

        Assert.Multiple(() =>
        {
            Assert.That(CrewMonitoringFilter.Matches(status, "kuk"), Is.True);
            Assert.That(CrewMonitoringFilter.Matches(status, "BLOODED"), Is.True);
            Assert.That(CrewMonitoringFilter.Matches(status, "ship"), Is.True);
            Assert.That(CrewMonitoringFilter.Matches(status, "marine"), Is.False);
        });
    }
}
