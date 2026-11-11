using System.Collections.Generic;
using Content.Server.CMU14.HelpRelay.ErrorRelay;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14;

public sealed class ErrorRelayFormatTest
{

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
