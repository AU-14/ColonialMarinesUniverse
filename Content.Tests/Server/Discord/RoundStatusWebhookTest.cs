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
            TimeSpan.FromMinutes(91));

        var payload = RoundStatusWebhook.CreatePayload(
            RoundStatusWebhookKind.Ended,
            status,
            new[] { "111", "222" });

        Assert.That(payload.Content, Is.EqualTo("<@&111> <@&222>"));
        Assert.That(payload.AllowedMentions.Parse, Has.Member("roles"));
        Assert.That(payload.Embeds, Has.Count.EqualTo(1));

        var embed = payload.Embeds![0];
        Assert.That(embed.Title, Is.EqualTo("Round #42 ended"));

        var fields = embed.Fields.ToDictionary(field => field.Name, field => field.Value);
        Assert.That(fields["Current Players"], Is.EqualTo("17"));
        Assert.That(fields["Current Map"], Is.EqualTo("Kutjevo"));
        Assert.That(fields["Current GOVFOR"], Is.EqualTo("5th Platoon"));
        Assert.That(fields["Current Gamemode"], Is.EqualTo("Distress Signal"));
        Assert.That(fields["Round ID"], Is.EqualTo("#42"));
        Assert.That(fields["Duration"], Is.EqualTo("1h 31m 0s"));
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
            "Insurgency");

        var payload = RoundStatusWebhook.CreatePayload(
            RoundStatusWebhookKind.Running,
            status,
            Array.Empty<string>());

        Assert.That(payload.Content, Is.EqualTo(string.Empty));
        Assert.That(payload.AllowedMentions.Parse, Is.Empty);
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
            TimeSpan.FromSeconds(12));
        var colors = new RoundStatusWebhookColors(1, 2, 3, 4);

        AssertState(RoundStatusWebhookKind.Starting, "Server starting", 1);
        AssertState(RoundStatusWebhookKind.Running, "Round #44 running", 2);
        AssertState(RoundStatusWebhookKind.Ended, "Round #44 ended", 3);
        AssertState(RoundStatusWebhookKind.Shutdown, "Server shutting down", 4);

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
