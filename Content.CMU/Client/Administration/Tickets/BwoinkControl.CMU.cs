using Content.Client.CMU14.Administration.Tickets;
using Content.Shared.Administration;
using Content.Shared.CMU14.Administration.Tickets;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Client.Administration.UI.Bwoink;

// CMU14 class: AHelp ticket status, acknowledge, claim and close controls for the admin AHelp window.
// Visibility here is only convenience; the server rechecks Adminhelp on every request.
public sealed partial class BwoinkControl
{
    [Dependency] private IEntitySystemManager _cmuEntitySystems = default!;
    [Dependency] private IPlayerManager _cmuPlayerManager = default!;

    private CMUTicketClientSystem? _cmuTickets;
    private CMUTicketCloseWindow? _cmuCloseWindow;
    // Built here rather than in BwoinkControl.xaml to keep the upstream layout untouched.
    private Button? _cmuAcknowledge;

    private void CMUInitTickets()
    {
        if (!_cmuEntitySystems.TryGetEntitySystem(out _cmuTickets))
            return;

        _cmuTickets.OnTicketsChanged += CMUOnTicketsChanged;

        if (CMUTicketClaim.Parent is { } row)
        {
            _cmuAcknowledge = new Button
            {
                Visible = false,
                Text = Loc.GetString("cmu-ahelp-ticket-acknowledge"),
                ToolTip = Loc.GetString("cmu-ahelp-ticket-acknowledge-tooltip"),
                StyleClasses = { "OpenRight" },
            };
            var claimPosition = CMUTicketClaim.GetPositionInParent();
            row.AddChild(_cmuAcknowledge);
            _cmuAcknowledge.SetPositionInParent(claimPosition);
            CMUTicketClaim.RemoveStyleClass("OpenRight");
            CMUTicketClaim.AddStyleClass("OpenBoth");

            _cmuAcknowledge.OnPressed += _ =>
            {
                if (_currentPlayer != null)
                    _cmuTickets?.Acknowledge(_currentPlayer.SessionId);
            };
        }

        CMUTicketClaim.OnPressed += _ =>
        {
            if (_currentPlayer == null || _cmuTickets == null)
                return;

            var player = _currentPlayer.SessionId;
            var ticket = _cmuTickets.GetTicket(player);
            if (ticket?.ClaimedBy != null && ticket.ClaimedBy == _cmuPlayerManager.LocalUser)
                _cmuTickets.Unclaim(player);
            else
                _cmuTickets.Claim(player);
        };

        CMUTicketClose.OnPressed += _ => CMUOpenCloseWindow();
    }

    /// <summary>
    /// Idempotent. Also called by the AHelp handler on dispose, because a replaced handler closes
    /// its window without disposing this control, which would otherwise stay subscribed.
    /// </summary>
    internal void CMUShutdownTickets()
    {
        if (_cmuTickets != null)
            _cmuTickets.OnTicketsChanged -= CMUOnTicketsChanged;
        _cmuTickets = null;

        _cmuCloseWindow?.Close();
        _cmuCloseWindow = null;
    }

    private void CMUOnTicketsChanged()
    {
        if (Disposed)
            return;

        ChannelSelector.PopulateList();
        CMUUpdateTicketControls();
    }

    private void CMUOpenCloseWindow()
    {
        if (_currentPlayer == null || _cmuTickets == null)
            return;

        var player = _currentPlayer.SessionId;
        _cmuCloseWindow?.Close();
        _cmuCloseWindow = new CMUTicketCloseWindow();
        _cmuCloseWindow.Title = $"{Loc.GetString("cmu-ahelp-ticket-close-window-title")}: {_currentPlayer.Username}";
        _cmuCloseWindow.OnConfirm += reason => _cmuTickets?.Close(player, reason);
        _cmuCloseWindow.OnClose += () => _cmuCloseWindow = null;
        _cmuCloseWindow.OpenCentered();
        _cmuCloseWindow.ReasonEdit.GrabKeyboardFocus();
    }

    /// <summary>
    /// Open tickets nobody has acknowledged, answered or claimed; these sort to the top.
    /// </summary>
    private bool CMUIsTicketWaiting(NetUserId player)
    {
        return _cmuTickets?.GetTicket(player) is { Status: CMUTicketStatus.Open, AcknowledgedBy: null, AnsweredBy: null };
    }

    private string CMUTicketGlyph(NetUserId player)
    {
        return _cmuTickets?.GetTicket(player) switch
        {
            { Status: CMUTicketStatus.Open, AcknowledgedBy: null, AnsweredBy: null } => Loc.GetString("cmu-ahelp-ticket-glyph-open") + " ",
            { Status: CMUTicketStatus.Open, AnsweredBy: not null } => Loc.GetString("cmu-ahelp-ticket-glyph-answered") + " ",
            { Status: CMUTicketStatus.Open } => Loc.GetString("cmu-ahelp-ticket-glyph-acknowledged") + " ",
            { Status: CMUTicketStatus.Claimed } => Loc.GetString("cmu-ahelp-ticket-glyph-claimed") + " ",
            _ => string.Empty,
        };
    }

    private void CMUUpdateTicketControls()
    {
        var visible = _cmuTickets != null && _adminManager.HasFlag(AdminFlags.Adminhelp);
        CMUTicketStatusLabel.Visible = visible;
        CMUTicketClaim.Visible = visible;
        CMUTicketClose.Visible = visible;
        if (_cmuAcknowledge != null)
            _cmuAcknowledge.Visible = visible;
        if (!visible)
            return;

        var ticket = _currentPlayer == null ? null : _cmuTickets!.GetTicket(_currentPlayer.SessionId);
        var active = ticket is { Status: not CMUTicketStatus.Closed };
        CMUTicketClaim.Disabled = !active;
        CMUTicketClose.Disabled = !active;
        if (_cmuAcknowledge != null)
            _cmuAcknowledge.Disabled = ticket is not { Status: CMUTicketStatus.Open, AcknowledgedBy: null, AnsweredBy: null };

        var mine = ticket?.ClaimedBy != null && ticket.ClaimedBy == _cmuPlayerManager.LocalUser;
        CMUTicketClaim.Text = Loc.GetString(mine
            ? "cmu-ahelp-ticket-unclaim"
            : ticket?.ClaimedBy != null
                ? "cmu-ahelp-ticket-take-over"
                : "cmu-ahelp-ticket-claim");

        var status = ticket switch
        {
            null => Loc.GetString("cmu-ahelp-ticket-status-none"),
            { Status: CMUTicketStatus.Open, AcknowledgedBy: null, AnsweredBy: null } t => Loc.GetString("cmu-ahelp-ticket-status-open", ("id", t.Id)),
            { Status: CMUTicketStatus.Open, AnsweredBy: not null } t => Loc.GetString("cmu-ahelp-ticket-status-answered",
                ("id", t.Id),
                ("admin", t.AnsweredByName ?? string.Empty)),
            { Status: CMUTicketStatus.Open } t => Loc.GetString("cmu-ahelp-ticket-status-acknowledged",
                ("id", t.Id),
                ("admin", t.AcknowledgedByName ?? string.Empty)),
            { Status: CMUTicketStatus.Claimed } t => Loc.GetString("cmu-ahelp-ticket-status-claimed",
                ("id", t.Id),
                ("admin", t.ClaimedByName ?? string.Empty)),
            { } t => Loc.GetString("cmu-ahelp-ticket-status-closed",
                ("id", t.Id),
                ("admin", t.ClosedByName ?? string.Empty),
                ("reason", t.CloseReason ?? string.Empty)),
        };
        CMUTicketStatusLabel.Text = status;
        CMUTicketStatusLabel.ToolTip = status;
    }
}
