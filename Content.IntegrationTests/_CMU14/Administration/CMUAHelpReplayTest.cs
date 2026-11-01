using System.Linq;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Shared.CMU14.Administration.Tickets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;

namespace Content.IntegrationTests._CMU14.Administration;

/// <summary>
/// The admin AHelp window refills from ticket history. The server snapshot is authoritative: it must replace what
/// a channel shows, even when the channel already has lines, so lines missed while deadminned come back.
/// </summary>
[TestFixture]
public sealed class CMUAHelpReplayTest
{
    [Test]
    public async Task SnapshotReplacesFilledChannels()
    {
        // The admin window's controls resolve client entity systems, which only exist once connected.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var owner = new NetUserId(Guid.NewGuid());
            var channel = new NetUserId(Guid.NewGuid());
            var start = new DateTime(2026, 1, 1, 12, 0, 0);
            var handler = new AdminAHelpUIHandler(owner);

            // The admin saw one line live before deadminning.
            var seen = new CMUTicketMessageEntry(start, channel, "seen live", false, true);
            var history = new Dictionary<NetUserId, List<CMUTicketMessageEntry>> { [channel] = [seen] };
            handler.CMUReplay(history, authoritative: false);
            var panel = handler.EnsurePanel(channel);
            Assert.That(Lines(panel), Is.EqualTo(1));

            // The player kept writing while the admin was deadminned; the server copy has every line.
            var snapshot = new Dictionary<NetUserId, List<CMUTicketMessageEntry>>
            {
                [channel] =
                [
                    seen,
                    new CMUTicketMessageEntry(start.AddMinutes(1), channel, "missed one", false, true),
                    new CMUTicketMessageEntry(start.AddMinutes(2), channel, "missed two", false, true),
                ],
            };

            // A local-mirror replay never touches a channel that already shows lines.
            handler.CMUReplay(snapshot, authoritative: false);
            Assert.That(Lines(panel), Is.EqualTo(1));

            // The snapshot clears and refills it: every server line, none doubled.
            handler.CMUReplay(snapshot, authoritative: true);
            Assert.That(Lines(panel), Is.EqualTo(3));
            Assert.That(panel.LastMessage, Is.EqualTo(start.AddMinutes(2)));

            // A repeated snapshot is idempotent.
            handler.CMUReplay(snapshot, authoritative: true);
            Assert.That(Lines(panel), Is.EqualTo(3));

            handler.Dispose();
        });

        await pair.CleanReturnAsync();
    }

    private static int Lines(Control panel)
    {
        return Descendants(panel).OfType<OutputPanel>().Single(o => o.Name == "TextOutput").EntryCount;
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }
}
