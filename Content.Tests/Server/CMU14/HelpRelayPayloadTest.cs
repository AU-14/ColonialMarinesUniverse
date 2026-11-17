using System;
using Content.Server.CMU14.HelpRelay.Systems;
using Content.Server.GameTicking;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14
{
    public sealed class HelpRelayPayloadTest
    {
        private static HelpContext Context(string server = "CMU Main") => new()
        {
            RoundId = 1234,
            Elapsed = TimeSpan.Parse("00:41:07"),
            Gamemode = "CMLowPop",
            Map = "MapAtlas",
            Server = server,
        };

        [Test]
        public void HelpPostsNeverPing()
        {
            var quiet = HelpRelayPayload.Build(HelpSource.MHelp,
                new HelpPost { Description = "x", Context = Context() },
                revision: null);
            Assert.That(quiet.Content, Is.Null);
            Assert.That(quiet.AllowedMentions, Is.Not.Null);
            Assert.That(quiet.AllowedMentions!.Parse, Is.Empty);
            Assert.That(quiet.AllowedMentions.Users, Is.Null);
        }

        [Test]
        public void ReporterPingAllowsOnlyThatUser()
        {
            var payload = HelpRelayPayload.Build(HelpSource.BugHelp,
                new HelpPost { Description = "x", Context = Context(), PingUserId = 123456789012345678 },
                revision: null);

            Assert.That(payload.Content, Is.EqualTo("<@123456789012345678>"));
            Assert.That(payload.AllowedMentions!.Parse, Is.Empty);
            Assert.That(payload.AllowedMentions.Users, Is.EqualTo(new[] { "123456789012345678" }));
        }

    }
}
