using System;
using System.Linq;
using Content.Server.Discord;
using NUnit.Framework;

namespace Content.Tests.Server.Discord;

[TestFixture]
public sealed class RoundStatusWebhookTest
{
    [Test]
    public void RoundEndPayloadIncludesStatusEmbedAndConfiguredRolePings()
    {
        var status = new RoundStatusWebhookData(
            42,
            17,
            "Kutjevo",
            "5th Platoon",
            "Distress Signal",
            new[]
            {
                new RoundStatusRecentGamemode(41, "Colony Fall", TimeSpan.FromMinutes(75)),
                new RoundStatusRecentGamemode(40, "Insurgency", TimeSpan.FromSeconds(2703)),
                new RoundStatusRecentGamemode(39, "Distress Signal", TimeSpan.FromSeconds(3550)),
            },
            TimeSpan.FromMinutes(91));

        var payload = RoundStatusWebhook.CreatePayload(
            RoundStatusWebhookKind.Ended,
            status,
            new[] { "111", "222" });

        Assert.That(payload.Content, Is.EqualTo("<@&111> <@&222>"));
        Assert.That(payload.AllowedMentions.Parse, Has.Member("roles"));
        Assert.That(payload.Embeds, Has.Count.EqualTo(1));

        var embed = payload.Embeds![0];
        Assert.That(embed.Title, Is.EqualTo("CMU Round #42 - Ended"));
        Assert.That(embed.Description, Is.EqualTo("Round ended."));

        var fields = embed.Fields.ToDictionary(field => field.Name, field => field.Value);
        Assert.That(fields["State"], Is.EqualTo("Ended"));
        Assert.That(fields["Players"], Is.EqualTo("17"));
        Assert.That(fields["Map"], Is.EqualTo("Kutjevo"));
        Assert.That(fields["GOVFOR"], Is.EqualTo("5th Platoon"));
        Assert.That(fields["Mode"], Is.EqualTo("Distress Signal"));
        Assert.That(fields["Round"], Is.EqualTo("#42"));
        Assert.That(fields["Last 3 Rounds"], Is.EqualTo("#41 Colony Fall 1h15m\n#40 Insurgency 45m03s\n#39 Distress Signal 59m10s"));
        Assert.That(fields["Runtime"], Is.EqualTo("1h 31m 0s"));
    }

    [Test]
    public void GamemodeRoleLookupOnlyReturnsConfiguredRolesForSpecificGamemodes()
    {
        Assert.That(
            RoundStatusWebhook.GetGamemodeRole("DistressSignal", "111", "222", "333"),
            Is.EqualTo("111"));
        Assert.That(
            RoundStatusWebhook.GetGamemodeRole("colonyfall", "111", "222", "333"),
            Is.EqualTo("222"));
        Assert.That(
            RoundStatusWebhook.GetGamemodeRole("Insurgency", "111", "222", "333"),
            Is.EqualTo("333"));
        Assert.That(
            RoundStatusWebhook.GetGamemodeRole("ForceOnForce", "111", "222", "333"),
            Is.Null);
    }

    [Test]
    public void PayloadWithoutPingsClearsPreviousMentionContent()
    {
        var status = new RoundStatusWebhookData(
            43,
            12,
            "Shiva's Snowball",
            "8th Platoon",
            "Insurgency",
            Array.Empty<RoundStatusRecentGamemode>());

        var payload = RoundStatusWebhook.CreatePayload(
            RoundStatusWebhookKind.Running,
            status,
            Array.Empty<string>());

        Assert.That(payload.Content, Is.EqualTo(string.Empty));
        Assert.That(payload.AllowedMentions.Parse, Is.Empty);
    }

    [Test]
    public void GamemodeVoteRolePingPayloadIncludesVotedMessage()
    {
        var payload = RoundStatusWebhook.CreateRolePingPayload(
            new[] { "333" },
            "Has been voted");

        Assert.That(payload.Content, Is.EqualTo("<@&333> Has been voted"));
        Assert.That(payload.AllowedMentions.Parse, Has.Member("roles"));
    }

    [Test]
    public void MessageIdStateRoundTripsThroughJson()
    {
        var ids = new RoundStatusWebhookMessageIds(11, 22, 33);

        var json = RoundStatusWebhook.SerializeMessageIds(ids);

        Assert.That(RoundStatusWebhook.TryDeserializeMessageIds(json, out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(ids));
    }

    [Test]
    public void MalformedMessageIdStateReturnsFalseAndDefaultIds()
    {
        Assert.That(
            RoundStatusWebhook.TryDeserializeMessageIds("{ nope", out var parsed),
            Is.False);
        Assert.That(parsed, Is.EqualTo(default(RoundStatusWebhookMessageIds)));
    }

    [Test]
    public void PayloadShortensLongVariableFieldsToAvoidAwkwardWrapping()
    {
        var status = new RoundStatusWebhookData(
            43,
            12,
            "Fiorina Orbital Penitentiary",
            "United States Colonial Marines",
            "Distress Signal With An Extremely Long Name",
            new[]
            {
                new RoundStatusRecentGamemode(42, "Distress Signal With An Extremely Long Name", TimeSpan.FromSeconds(3723)),
            });

        var payload = RoundStatusWebhook.CreatePayload(
            RoundStatusWebhookKind.Lobby,
            status,
            Array.Empty<string>());

        var fields = payload.Embeds![0].Fields.ToDictionary(field => field.Name, field => field.Value);
        Assert.That(fields["Map"], Is.EqualTo("Fiorina Orbital Penit..."));
        Assert.That(fields["GOVFOR"], Is.EqualTo("United States Colonia..."));
        Assert.That(fields["Mode"], Is.EqualTo("Distress Signal With..."));
        Assert.That(fields["Last 3 Rounds"], Is.EqualTo("#42 Distress Sig... 1h02m"));
    }

    [Test]
    public void OfflinePayloadDoesNotIncludeStaleRoundFields()
    {
        var status = new RoundStatusWebhookData(
            44,
            9,
            "Some Old Map",
            "Some Old GOVFOR",
            "Some Old Mode",
            new[]
            {
                new RoundStatusRecentGamemode(43, "Some Old Mode", TimeSpan.FromMinutes(12)),
            },
            TimeSpan.FromSeconds(12));

        var payload = RoundStatusWebhook.CreatePayload(
            RoundStatusWebhookKind.Shutdown,
            status,
            Array.Empty<string>());

        var embed = payload.Embeds![0];
        Assert.That(embed.Title, Is.EqualTo("CMU Round Status - Offline"));
        Assert.That(embed.Description, Is.EqualTo("Server offline."));
        Assert.That(embed.Fields, Has.Count.EqualTo(1));
        Assert.That(embed.Fields[0].Name, Is.EqualTo("State"));
        Assert.That(embed.Fields[0].Value, Is.EqualTo("Offline"));
    }

    [Test]
    public void PeriodicUpdateIsDueOnlyAfterIntervalAndExistingStatusMessage()
    {
        var interval = TimeSpan.FromSeconds(60);

        Assert.That(
            RoundStatusWebhook.ShouldUpdate(
                TimeSpan.FromSeconds(119),
                TimeSpan.FromSeconds(120),
                interval,
                true),
            Is.False);
        Assert.That(
            RoundStatusWebhook.ShouldUpdate(
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(120),
                interval,
                true),
            Is.True);
        Assert.That(
            RoundStatusWebhook.ShouldUpdate(
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(120),
                TimeSpan.Zero,
                true),
            Is.False);
        Assert.That(
            RoundStatusWebhook.ShouldUpdate(
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(120),
                interval,
                false),
            Is.False);
    }

    [Test]
    public void PayloadUsesStateSpecificTitlesAndColors()
    {
        var status = new RoundStatusWebhookData(
            44,
            9,
            "unknown",
            "unknown",
            "unknown",
            Array.Empty<RoundStatusRecentGamemode>(),
            TimeSpan.FromSeconds(12));
        var colors = new RoundStatusWebhookColors(1, 2, 3, 4);

        AssertState(RoundStatusWebhookKind.Starting, "CMU Round Status - Starting", 1);
        AssertState(RoundStatusWebhookKind.Lobby, "CMU Round Status - Lobby", 1);
        AssertState(RoundStatusWebhookKind.Running, "CMU Round #44 - Running", 2);
        AssertState(RoundStatusWebhookKind.Ended, "CMU Round #44 - Ended", 3);
        AssertState(RoundStatusWebhookKind.Shutdown, "CMU Round Status - Offline", 4);

        void AssertState(RoundStatusWebhookKind kind, string title, int color)
        {
            var payload = RoundStatusWebhook.CreatePayload(kind, status, Array.Empty<string>(), colors);

            Assert.That(payload.Embeds, Has.Count.EqualTo(1));
            Assert.That(payload.Embeds![0].Title, Is.EqualTo(title));
            Assert.That(payload.Embeds[0].Color, Is.EqualTo(color));
        }
    }

    [Test]
    public void ColorParserAcceptsHexAndFallsBackForInvalidValues()
    {
        Assert.That(RoundStatusWebhook.ParseColor("23EB49", 1), Is.EqualTo(0x23EB49));
        Assert.That(RoundStatusWebhook.ParseColor("#CD1010", 1), Is.EqualTo(0xCD1010));
        Assert.That(RoundStatusWebhook.ParseColor("not-hex", 1), Is.EqualTo(1));
        Assert.That(RoundStatusWebhook.ParseColor("12345", 1), Is.EqualTo(1));
    }
}
