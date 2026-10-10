using Content.Client.CMU14.Administration.Tickets;
using Content.Shared.Administration;
using Content.Shared.CMU14.Administration.Tickets;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Client.UserInterface.Systems.Bwoink;

// CMU14 class: AHelp ticket history. Refills the admin window after it is rebuilt (relog, Adminhelp
// flag flip) from CMUTicketClientSystem, which mirrors the server's round-scoped channel history.
public sealed partial class AHelpUIController : IOnSystemChanged<CMUTicketClientSystem>
{
    private CMUTicketClientSystem? _cmuTickets;

    public void OnSystemLoaded(CMUTicketClientSystem system)
    {
        _cmuTickets = system;
        system.OnSnapshotReceived += CMUOnSnapshotReceived;
    }

    public void OnSystemUnloaded(CMUTicketClientSystem system)
    {
        system.OnSnapshotReceived -= CMUOnSnapshotReceived;
        _cmuTickets = null;
    }

    /// <summary>
    /// Called from <see cref="EnsureUIHelper"/> right after a new handler replaced the old one.
    /// </summary>
    private void CMUOnUIHelperCreated()
    {
        if (UIHelper is not AdminAHelpUIHandler admin || _cmuTickets == null)
            return;

        admin.CMUReplay(_cmuTickets.History);
        // The mirror only holds what this client saw while it was an admin; ask for the server copy.
        _cmuTickets.RequestSnapshot();
    }

    /// <summary>
    /// Called from <see cref="ReceivedBwoink"/> after the line reached the handler.
    /// </summary>
    private void CMURecordBwoink(SharedBwoinkSystem.BwoinkTextMessage message)
    {
        if (UIHelper is not AdminAHelpUIHandler admin)
            return;

        admin.CMUMarkFilled(message.UserId);
        _cmuTickets?.RecordLine(message);
    }

    private void CMUOnSnapshotReceived()
    {
        if (_cmuTickets == null || _playerManager.LocalUser == null)
            return;

        if (UIHelper is AdminAHelpUIHandler admin)
        {
            admin.CMUReplay(_cmuTickets.History);
            return;
        }

        // A fresh admin handler replays the whole mirror while it is created.
        if (_adminManager.HasFlag(AdminFlags.Adminhelp))
            EnsureUIHelper();
    }
}

public sealed partial class AdminAHelpUIHandler
{
    // Channels that already show lines in this handler. Replays skip them so nothing is doubled.
    private readonly HashSet<NetUserId> _cmuFilledChannels = new();

    public void CMUMarkFilled(NetUserId channel)
    {
        _cmuFilledChannels.Add(channel);
    }

    /// <summary>
    /// Writes stored history into channels that have no lines yet, without counting them as unread.
    /// </summary>
    public void CMUReplay(IReadOnlyDictionary<NetUserId, List<CMUTicketMessageEntry>> history)
    {
        NetUserId? replayed = null;
        foreach (var (channel, lines) in history)
        {
            if (lines.Count == 0 || !_cmuFilledChannels.Add(channel))
                continue;

            var panel = EnsurePanel(channel);
            foreach (var line in lines)
            {
                var message = new SharedBwoinkSystem.BwoinkTextMessage(
                    channel,
                    line.TrueSender,
                    line.Text,
                    line.SentAt,
                    playSound: false,
                    adminOnly: line.AdminOnly);

                try
                {
                    panel.ReceiveLine(message, countUnread: false);
                }
                catch (Exception e)
                {
                    Logger.GetSawmill("cmu.ahelp.tickets").Warning($"Skipped unreadable AHelp history line in {channel}: {e.Message}");
                }
            }

            replayed = channel;
        }

        if (replayed != null)
            Control?.OnBwoink(replayed.Value);
    }
}
