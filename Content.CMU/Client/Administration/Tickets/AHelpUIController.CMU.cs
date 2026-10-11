using Content.Client.CMU14.Administration;
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

        // Shows the local mirror at once. It only holds what this client saw while it was an admin, so the
        // server copy is requested too and replaces it when it lands.
        admin.CMUReplay(_cmuTickets.History, authoritative: false);
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

        // The mirror now holds the server snapshot, which is authoritative over what the handler shows.
        if (UIHelper is AdminAHelpUIHandler admin)
        {
            admin.CMUReplay(_cmuTickets.History, authoritative: true);
            return;
        }

        // A fresh admin handler replays the whole mirror while it is created.
        if (CMUHasAhelp())
            EnsureUIHelper();
    }
}

// CMU14 class: in-game admin mode. The admin is inactive, so the client admin manager no longer reports
// Adminhelp; CMUIngameAdminClientSystem says they still handle AHelps.
public sealed partial class AHelpUIController : IOnSystemChanged<CMUIngameAdminClientSystem>
{
    private CMUIngameAdminClientSystem? _cmuIngameAdmin;

    /// <summary>
    /// Whether the local player gets the admin AHelp window: an Adminhelp admin or an in-game admin.
    /// </summary>
    public bool CMUHasAhelp()
    {
        return _adminManager.HasFlag(AdminFlags.Adminhelp) || (_cmuIngameAdmin?.IngameAdmin ?? false);
    }

    public void OnSystemLoaded(CMUIngameAdminClientSystem system)
    {
        _cmuIngameAdmin = system;
        system.OnStateChanged += CMUOnIngameAdminChanged;
    }

    public void OnSystemUnloaded(CMUIngameAdminClientSystem system)
    {
        system.OnStateChanged -= CMUOnIngameAdminChanged;
        _cmuIngameAdmin = null;
    }

    private void CMUOnIngameAdminChanged()
    {
        if (_playerManager.LocalUser == null)
            return;

        EnsureUIHelper();
        UpdateUnreadState();
    }
}

public sealed partial class AdminAHelpUIHandler
{
    // Channels that already show lines in this handler. Local-mirror replays skip them so nothing is doubled.
    private readonly HashSet<NetUserId> _cmuFilledChannels = new();

    public void CMUMarkFilled(NetUserId channel)
    {
        _cmuFilledChannels.Add(channel);
    }

    /// <summary>
    /// Writes stored history into channels without counting it as unread. An <paramref name="authoritative"/>
    /// replay is the server snapshot: every channel it carries is cleared and refilled, so lines this client
    /// missed (for example while deadminned) appear. Otherwise only channels with no lines yet are filled.
    /// </summary>
    public void CMUReplay(IReadOnlyDictionary<NetUserId, List<CMUTicketMessageEntry>> history, bool authoritative)
    {
        NetUserId? replayed = null;
        foreach (var (channel, lines) in history)
        {
            if (lines.Count == 0)
                continue;

            var filled = !_cmuFilledChannels.Add(channel);
            if (filled && !authoritative)
                continue;

            var panel = EnsurePanel(channel);
            if (filled)
                panel.CMUClearLines();

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
