using Content.Shared._RMC14.Marines.Announce;
using NUnit.Framework;

namespace Content.Tests.Shared._RMC14.Marines.Announce;

[TestFixture]
public sealed class MarineAnnouncementFactionTest
{
    [Test]
    public void ResolvesKnownOverwatchGroupsWhenCommunicationsFactionIsUnset()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedMarineAnnounceSystem.ResolveAnnouncementFaction(null, "GOVFOR"), Is.EqualTo("govfor"));
            Assert.That(SharedMarineAnnounceSystem.ResolveAnnouncementFaction(null, "OPFOR"), Is.EqualTo("opfor"));
        });
    }

    [Test]
    public void ConfiguredCommunicationsFactionTakesPriority()
    {
        Assert.That(SharedMarineAnnounceSystem.ResolveAnnouncementFaction("opfor", "GOVFOR"), Is.EqualTo("opfor"));
    }

    [Test]
    public void MarineRecipientMustMatchTargetFaction()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedMarineAnnounceSystem.IsMarineAnnouncementRecipient("GOVFOR", "govfor"), Is.True);
            Assert.That(SharedMarineAnnounceSystem.IsMarineAnnouncementRecipient("opfor", "govfor"), Is.False);
            Assert.That(SharedMarineAnnounceSystem.IsMarineAnnouncementRecipient("govfor", "opfor"), Is.False);
            Assert.That(SharedMarineAnnounceSystem.IsMarineAnnouncementRecipient("OPFOR", "opfor"), Is.True);
        });
    }
}
