using Content.Server.CMU14.Administration.Tickets;
using Robust.Shared.Network;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Server.Administration.Systems;

// CMU14 class: AHelp ticket hooks. The fork side only calls into these; the ticket rules live in CMUTicketSystem.
public sealed partial class BwoinkSystem
{
    [Dependency] private CMUTicketSystem _cmuTickets = default!;

    /// <summary>
    /// Queues a ticket status line (claim, close, ...) on the player's Discord relay embed.
    /// Does nothing when the relay is off.
    /// </summary>
    public void CMURelayTicketLine(NetUserId channel, string adminName, string plainText)
    {
        if (_webhookUrl == string.Empty)
            return;

        var messageParams = new AHelpMessageParams(
            adminName,
            plainText,
            true,
            _gameTicker.RoundDuration().ToString("hh\\:mm\\:ss"),
            _gameTicker.RunLevel,
            playedSound: true,
            icon: ":ticket:");
        _messageQueues.GetOrNew(channel).Enqueue(GenerateAHelpMessage(messageParams));
    }
}
