using System.Collections.Generic;
using Content.Server.CMU14.HelpRelay.ErrorRelay;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14;

public sealed class ErrorRelayFormatTest
{
    private static ErrorLine Line(string text, string sawmill = "eng", string level = "Error") => new()
    {
        Sawmill = sawmill,
        Level = level,
        Text = text,
    };

    [Test]
    public void RendersLevelAndSawmill()
    {
        var line = Line("Caught exception in GameLoop Tick");

        Assert.That(line.Render(), Is.EqualTo("**Error `eng`** Caught exception in GameLoop Tick"));
    }

    [Test]
    public void RepeatedEntriesCollapseWithMarker()
    {
        var lines = new List<ErrorLine>
        {
            Line("boom"),
            Line("boom"),
            Line("boom"),
            Line("different"),
        };

        var description = ErrorRelayFormat.BuildDescription(lines, 2800, out var overflow);

        Assert.That(overflow, Is.EqualTo(0));
        Assert.That(description, Is.EqualTo("**Error `eng`** boom\n_×3_\n\n**Error `eng`** different"));
    }

    [Test]
    public void EntriesPastTheCapCountAsOverflow()
    {
        var lines = new List<ErrorLine>
        {
            Line(new string('a', 2000)),
            Line(new string('b', 2000)),
            Line("tail"),
        };

        var description = ErrorRelayFormat.BuildDescription(lines, 2800, out var overflow);

        Assert.That(overflow, Is.EqualTo(2));
        Assert.That(description, Does.StartWith("**Error `eng`** aaa"));
        Assert.That(description, Does.Not.Contain("tail"));
    }

    [Test]
    public void SanitizeReplacesAddressesHardwareIdsAndPlatformIds()
    {
        Assert.That(ErrorRelayFormat.Sanitize("connection from 192.168.1.23 failed"),
            Is.EqualTo("connection from [ip] failed"));
        Assert.That(ErrorRelayFormat.Sanitize("peer fe80::1f:2aa:3bb endpoint died"),
            Is.EqualTo("peer [ip] endpoint died"));
        Assert.That(ErrorRelayFormat.Sanitize("peer 2001:db8:0:0:0:0:0:2 dropped"),
            Is.EqualTo("peer [ip] dropped"));
        Assert.That(ErrorRelayFormat.Sanitize("adapter 0a:1b:2c:3d:4e:5f down"),
            Is.EqualTo("adapter [mac] down"));
        Assert.That(ErrorRelayFormat.Sanitize("adapter 0a-1b-2c-3d-4e-5f down"),
            Is.EqualTo("adapter [mac] down"));
        Assert.That(ErrorRelayFormat.Sanitize("ban (hwid: a1B2c3D4e5F6g7)"),
            Is.EqualTo("ban ([hwid])"));
        Assert.That(ErrorRelayFormat.Sanitize("discord id 1451234567890123456 refused"),
            Is.EqualTo("discord id [id] refused"));
    }

    [Test]
    public void SanitizeScrubsFullLogLine()
    {
        Assert.That(ErrorRelayFormat.Sanitize("banned Jane (192.168.1.23, hwid: a1B2c3D4e5F6g7) from the server"),
            Is.EqualTo("banned Jane ([ip], [hwid]) from the server"));
    }

    [Test]
    public void SanitizeScrubsHwidBeforeOtherScanners()
    {
        // The hwid scanner runs first: a mac-shaped hwid value is scrubbed whole.
        Assert.That(ErrorRelayFormat.Sanitize("hwid: 0a-1b-2c-3d-4e-5f"),
            Is.EqualTo("[hwid]"));
    }

    [Test]
    public void SanitizeKeepsNearMisses()
    {
        // Two colon groups: the IPv6 scanner fires at three or more.
        Assert.That(ErrorRelayFormat.Sanitize("14:03:22 Jane (fooBar) won the vote"),
            Is.EqualTo("14:03:22 Jane (fooBar) won the vote"));
        // Three dot groups: the IPv4 scanner fires at four.
        Assert.That(ErrorRelayFormat.Sanitize("engine version 0.4.12 ok"),
            Is.EqualTo("engine version 0.4.12 ok"));
        // The compressed IPv6 form needs a hex run after the double colon.
        Assert.That(ErrorRelayFormat.Sanitize("no ipv6 text like std::vector here"),
            Is.EqualTo("no ipv6 text like std::vector here"));
        // 18 digits: the platform id scanner fires at nineteen or more.
        Assert.That(ErrorRelayFormat.Sanitize("ticks 638700000000000000 elapsed"),
            Is.EqualTo("ticks 638700000000000000 elapsed"));
    }
}
