using System;
using Content.Server.CMU14.HelpRelay;
using Content.Shared.GameTicking;
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
        public void FooterDropsEmptyParts()
        {
            var payload = HelpRelayPayload.Build(HelpSource.MHelp,
                new HelpPost { Description = "how do I reload?", Context = Context() },
                revision: null);

            Assert.That(payload.Embeds[0].Footer!.Text, Is.EqualTo("CMU Main · CMU help relay"));
        }

        [Test]
        public void FooterIncludesRevisionWhenSet()
        {
            var payload = HelpRelayPayload.Build(HelpSource.BugHelp,
                new HelpPost { Description = "fire no spread", Context = Context() },
                revision: "20a11caad6");

            Assert.That(payload.Embeds[0].Footer!.Text, Is.EqualTo("rev 20a11caad6 · CMU Main · CMU help relay"));
        }

        [Test]
        public void EmbedCarriesSourceFaceAndContext()
        {
            var payload = HelpRelayPayload.Build(HelpSource.BugHelp,
                new HelpPost { Description = "x", Author = "foobar (Jane · CMJobSquadRifleman)", Context = Context() },
                revision: "abc");

            var embed = payload.Embeds[0];
            Assert.That(embed.Author!.Name, Is.EqualTo("BugHelp · foobar (Jane · CMJobSquadRifleman)"));
            Assert.That(embed.Color, Is.EqualTo(0x5865F2));
            Assert.That(embed.Fields, Has.Count.EqualTo(4));
            Assert.That(embed.Fields[0].Name, Is.EqualTo("Round"));
            Assert.That(embed.Fields[0].Value, Is.EqualTo("1234"));
            Assert.That(embed.Fields[1].Value, Is.EqualTo("CMLowPop"));
            Assert.That(embed.Fields[2].Value, Is.EqualTo("MapAtlas"));
        }

        [Test]
        public void StaffAuthorFallback()
        {
            var payload = HelpRelayPayload.Build(HelpSource.AHelp,
                new HelpPost { Description = "x", Context = Context() },
                revision: null);

            Assert.That(payload.Embeds[0].Author!.Name, Is.EqualTo("AHelp · staff"));
        }

        [Test]
        public void LongDescriptionsAreCapped()
        {
            var payload = HelpRelayPayload.Build(HelpSource.MHelp,
                new HelpPost { Description = new string('a', 5000), Context = Context() },
                revision: null);

            var description = payload.Embeds[0].Description;
            Assert.That(description.Length, Is.EqualTo(3001)); // 3000 + ellipsis
            Assert.That(description, Does.EndWith("…"));
        }

        [Test]
        public void PingOnlyFromVerifiedLink()
        {
            var pinged = HelpRelayPayload.Build(HelpSource.BugHelp,
                new HelpPost { Description = "x", PingUserId = 42, Context = Context() },
                revision: null);
            Assert.That(pinged.Content, Is.EqualTo("<@42>"));
            Assert.That(pinged.AllowedMentions!.Users, Is.EqualTo(new[] { "42" }));

            var quiet = HelpRelayPayload.Build(HelpSource.MHelp,
                new HelpPost { Description = "x", Context = Context() },
                revision: null);
            Assert.That(quiet.Content, Is.Null);
            Assert.That(quiet.AllowedMentions, Is.Null);
        }

        [Test]
        public void ThreadNameAndTitlePassThrough()
        {
            var payload = HelpRelayPayload.Build(HelpSource.BugHelp,
                new HelpPost
                {
                    Description = "x",
                    Title = "BUG-R1234-03 · fire doesn't spread",
                    ThreadName = "BUG-R1234-03 · fire doesn't spread",
                    Context = Context(),
                },
                revision: null);

            Assert.That(payload.ThreadName, Does.StartWith("BUG-R1234-03"));
            Assert.That(payload.Embeds[0].Title, Is.EqualTo(payload.ThreadName));
        }

        [Test]
        public void ConversationLineMarksDirectionSpeakerAndRoundTime()
        {
            var player = HelpRelayPayload.BuildConversationLine("PFC Jane Doe", fromStaff: false, "00:12:34", "how do I reload?");
            Assert.That(player, Is.EqualTo(":inbox_tray: **[00:12:34]** **PFC Jane Doe:** how do I reload?"));

            var staff = HelpRelayPayload.BuildConversationLine("dev", fromStaff: true, roundTime: null, "alt+<F4>");
            Assert.That(staff, Is.EqualTo(":outbox_tray: **dev:** alt+<F4>"));
        }

        [Test]
        public void ConversationLineCapsLongText()
        {
            var line = HelpRelayPayload.BuildConversationLine("a", false, null, new string('x', 5000));

            Assert.That(line, Does.EndWith("... **(too long)**"));
        }

        [Test]
        public void ConversationPayloadCarriesTranscript()
        {
            var payload = HelpRelayPayload.BuildConversationPayload(
                HelpSource.MHelp,
                "dev",
                "\n:inbox_tray: **PFC Jane Doe:** hi",
                revision: "20a11caad6",
                server: "CMU Main");

            Assert.That(payload.Username, Is.EqualTo("MHelp · dev"));
            var embed = payload.Embeds[0];
            Assert.That(embed.Description, Does.Contain("PFC Jane Doe"));
            Assert.That(embed.Color, Is.EqualTo(0xE67E22));
            Assert.That(embed.Footer!.Text, Is.EqualTo("rev 20a11caad6 · CMU Main · CMU help relay"));
        }

        [Test]
        public void RunLevelSeparatorsAreMarkdown()
        {
            Assert.That(HelpRelayPayload.BuildRunLevelSeparator(GameRunLevel.InRound), Does.Contain("Round started"));
            Assert.That(HelpRelayPayload.BuildRunLevelSeparator(GameRunLevel.PostRound), Does.Contain("Post-round started"));
        }
    }
}
