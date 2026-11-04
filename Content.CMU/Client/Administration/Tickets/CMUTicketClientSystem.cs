using Content.Shared.Administration;
using Content.Shared.CMU14.Administration.Tickets;
using Robust.Shared.Network;

namespace Content.Client.CMU14.Administration.Tickets;

/// <summary>
/// Admin-side mirror of the server's AHelp tickets and channel history. The server snapshot replaces
/// it wholesale; live bwoink lines are appended by the AHelp UI so a rebuilt admin window can be
/// refilled without a round trip.
/// </summary>
public sealed class CMUTicketClientSystem : EntitySystem
{
    private readonly Dictionary<NetUserId, CMUTicketInfo> _tickets = new();
    private readonly Dictionary<NetUserId, List<CMUTicketMessageEntry>> _history = new();

    /// <summary>
    /// Any ticket was added, changed or cleared.
    /// </summary>
    public event Action? OnTicketsChanged;

    /// <summary>
    /// A full snapshot replaced the history. Raised after <see cref="OnTicketsChanged"/>.
    /// </summary>
    public event Action? OnSnapshotReceived;

    public IReadOnlyDictionary<NetUserId, List<CMUTicketMessageEntry>> History => _history;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CMUTicketSnapshotEvent>(OnSnapshot);
        SubscribeNetworkEvent<CMUTicketUpdatedEvent>(OnUpdated);
    }

    public CMUTicketInfo? GetTicket(NetUserId player)
    {
        return _tickets.GetValueOrDefault(player);
    }

    /// <summary>
    /// Keeps a live bwoink line so the admin window can be rebuilt from the mirror.
    /// </summary>
    public void RecordLine(SharedBwoinkSystem.BwoinkTextMessage message)
    {
        if (!_history.TryGetValue(message.UserId, out var lines))
            _history[message.UserId] = lines = new List<CMUTicketMessageEntry>();

        lines.Add(new CMUTicketMessageEntry(message.SentAt, message.TrueSender, message.Text, message.AdminOnly, message.PlaySound));
        if (lines.Count > CMUTicketLimits.MaxHistoryPerChannel)
            lines.RemoveRange(0, lines.Count - CMUTicketLimits.MaxHistoryPerChannel);
    }

    public void RequestSnapshot()
    {
        RaiseNetworkEvent(new CMUTicketSnapshotRequestEvent());
    }

    public void Acknowledge(NetUserId player)
    {
        RaiseNetworkEvent(new CMUTicketAcknowledgeRequestEvent(player));
    }

    public void Claim(NetUserId player)
    {
        RaiseNetworkEvent(new CMUTicketClaimRequestEvent(player));
    }

    public void Unclaim(NetUserId player)
    {
        RaiseNetworkEvent(new CMUTicketUnclaimRequestEvent(player));
    }

    public void Close(NetUserId player, string reason)
    {
        RaiseNetworkEvent(new CMUTicketCloseRequestEvent(player, reason));
    }

    private void OnSnapshot(CMUTicketSnapshotEvent ev)
    {
        _tickets.Clear();
        foreach (var ticket in ev.Tickets)
        {
            _tickets[ticket.Player] = ticket;
        }

        _history.Clear();
        foreach (var (channel, lines) in ev.History)
        {
            _history[channel] = lines;
        }

        OnTicketsChanged?.Invoke();
        OnSnapshotReceived?.Invoke();
    }

    private void OnUpdated(CMUTicketUpdatedEvent ev)
    {
        _tickets[ev.Ticket.Player] = ev.Ticket;
        OnTicketsChanged?.Invoke();
    }
}
