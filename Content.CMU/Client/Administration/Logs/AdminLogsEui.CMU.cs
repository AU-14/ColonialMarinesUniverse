using System.Numerics;
using Robust.Client.UserInterface.Controls;

// ReSharper disable once CheckNamespace
namespace Content.Client.Administration.UI.Logs;

// CMU14 class: per-player log window titles and cascaded placement, so several "View Logs" windows
// opened one after another can be told apart instead of stacking exactly on top of each other.
public sealed partial class AdminLogsEui
{
    private const float CMUCascadeStep = 24f;
    private const int CMUCascadeSlots = 8;

    // Cascade slots held by log windows open on this client. A new window takes the lowest free one,
    // so closing a window frees its spot instead of making the next window overlap a still-open one.
    private static readonly HashSet<int> CMUUsedSlots = new();

    // Windows opened while every slot was held; they share slots and own none.
    private static int _cmuOverflowWindows;

    // This window's slot, null once released so a second close does not release twice.
    private int? _cmuSlot;
    private bool _cmuOwnsSlot;
    private Guid? _cmuTitlePlayer;
    private Dictionary<Guid, string>? _cmuPlayerNames;

    /// <summary>
    /// Called from <see cref="Opened"/> in place of centering the window.
    /// </summary>
    private void CMUOpenCascaded()
    {
        var slot = -1;
        for (var i = 0; i < CMUCascadeSlots; i++)
        {
            if (CMUUsedSlots.Contains(i))
                continue;

            slot = i;
            break;
        }

        _cmuOwnsSlot = slot >= 0;
        if (_cmuOwnsSlot)
        {
            CMUUsedSlots.Add(slot);
        }
        else
        {
            slot = (CMUUsedSlots.Count + _cmuOverflowWindows) % CMUCascadeSlots;
            _cmuOverflowWindows++;
        }

        _cmuSlot = slot;

        if (LogsWindow == null)
            return;

        LogsWindow.OpenCentered();

        if (slot == 0 || LogsWindow.Parent is not { } parent)
            return;

        // Same placement as BaseWindow.RecenterWindow, shifted down-right by the slot.
        var size = LogsWindow.DesiredSize;
        var corner = parent.Size * 0.5f - size / 2 + new Vector2(slot * CMUCascadeStep);
        var max = Vector2.Max(Vector2.Zero, parent.Size - size);
        LayoutContainer.SetPosition(LogsWindow, Vector2.Clamp(corner, Vector2.Zero, max));
    }

    /// <summary>
    /// Called from <see cref="Closed"/>.
    /// </summary>
    private void CMUOnClosed()
    {
        if (_cmuSlot is not { } slot)
            return;

        _cmuSlot = null;
        if (_cmuOwnsSlot)
            CMUUsedSlots.Remove(slot);
        else
            _cmuOverflowWindows = Math.Max(0, _cmuOverflowWindows - 1);
    }

    /// <summary>
    /// Called from <see cref="HandleState"/> once the round's player names are known.
    /// </summary>
    private void CMUOnPlayerNames(Dictionary<Guid, string> players)
    {
        _cmuPlayerNames = players;
        CMUApplyTitle();
    }

    /// <summary>
    /// Called from <see cref="HandleMessage"/> when the server preselects players, e.g. the View Logs verb.
    /// </summary>
    private void CMUOnPlayersSelected(List<Guid> players)
    {
        _cmuTitlePlayer = players.Count == 1 ? players[0] : null;
        CMUApplyTitle();
    }

    /// <summary>
    /// Applies the title to the docked window and to the popped-out OS window, whichever exists.
    /// </summary>
    private void CMUApplyTitle()
    {
        var title = Loc.GetString("admin-logs-title");

        // Until the names arrive the default title stays; a player missing from the round list shows its id.
        if (_cmuTitlePlayer is { } selected && _cmuPlayerNames != null)
        {
            var name = _cmuPlayerNames.GetValueOrDefault(selected, selected.ToString());
            title = Loc.GetString("cmu-admin-logs-title-player", ("player", name));
        }

        if (LogsWindow != null)
            LogsWindow.Title = title;

        if (ClydeWindow != null)
            ClydeWindow.Title = title;
    }
}
