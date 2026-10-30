using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Administration.Tickets;

[Serializable, NetSerializable]
public enum CMUTicketStatus : byte
{
    Open,
    Claimed,
    Closed,
}

/// <summary>
/// One AHelp line as admins saw it. <see cref="Text"/> is already formatted markup, so the client
/// replays it the same way it shows a live bwoink line.
/// </summary>
[Serializable, NetSerializable]
public sealed record CMUTicketMessageEntry(
    DateTime SentAt,
    NetUserId TrueSender,
    string Text,
    bool AdminOnly,
    bool PlaySound);

/// <summary>
/// The latest ticket of one player's AHelp channel. Closed tickets stay until round end so admins
/// can still see why they were closed. <see cref="AcknowledgedBy"/> is set when an admin sent the
/// canned "seen" reply; it is not a claim and survives a later claim or unclaim.
/// <see cref="AnsweredBy"/> is set when an admin first wrote a non-admin-only reply into the open
/// ticket; like the acknowledgement it survives a later claim or unclaim.
/// </summary>
[Serializable, NetSerializable]
public sealed record CMUTicketInfo(
    int Id,
    NetUserId Player,
    string PlayerName,
    CMUTicketStatus Status,
    NetUserId? ClaimedBy,
    string? ClaimedByName,
    NetUserId? AcknowledgedBy,
    string? AcknowledgedByName,
    NetUserId? AnsweredBy,
    string? AnsweredByName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string? ClosedByName,
    string? CloseReason);

/// <summary>
/// Server to one admin: every ticket and the AHelp history of the most recently active ticket
/// channels, keyed by player channel. An empty snapshot means the round's tickets were cleared.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUTicketSnapshotEvent : EntityEventArgs
{
    public readonly List<CMUTicketInfo> Tickets;
    public readonly Dictionary<NetUserId, List<CMUTicketMessageEntry>> History;

    public CMUTicketSnapshotEvent(List<CMUTicketInfo> tickets, Dictionary<NetUserId, List<CMUTicketMessageEntry>> history)
    {
        Tickets = tickets;
        History = history;
    }
}

/// <summary>
/// Server to admins: one ticket was opened, acknowledged, answered, claimed, unclaimed or closed.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUTicketUpdatedEvent : EntityEventArgs
{
    public readonly CMUTicketInfo Ticket;

    public CMUTicketUpdatedEvent(CMUTicketInfo ticket)
    {
        Ticket = ticket;
    }
}

/// <summary>
/// Client to server: resend the ticket snapshot, e.g. after the admin AHelp UI was rebuilt.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUTicketSnapshotRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class CMUTicketClaimRequestEvent : EntityEventArgs
{
    public readonly NetUserId Player;

    public CMUTicketClaimRequestEvent(NetUserId player)
    {
        Player = player;
    }
}

/// <summary>
/// Client to server: send the player the canned "seen, will be handled" reply without claiming.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUTicketAcknowledgeRequestEvent : EntityEventArgs
{
    public readonly NetUserId Player;

    public CMUTicketAcknowledgeRequestEvent(NetUserId player)
    {
        Player = player;
    }
}

[Serializable, NetSerializable]
public sealed class CMUTicketUnclaimRequestEvent : EntityEventArgs
{
    public readonly NetUserId Player;

    public CMUTicketUnclaimRequestEvent(NetUserId player)
    {
        Player = player;
    }
}

[Serializable, NetSerializable]
public sealed class CMUTicketCloseRequestEvent : EntityEventArgs
{
    public readonly NetUserId Player;
    public readonly string Reason;

    public CMUTicketCloseRequestEvent(NetUserId player, string reason)
    {
        Player = player;
        Reason = reason;
    }
}

public static class CMUTicketLimits
{
    /// <summary>
    /// Longest close reason the server accepts, after trimming.
    /// </summary>
    public const int MaxCloseReasonLength = 500;

    /// <summary>
    /// Lines kept per player channel, on the server and in the admin client mirror. The oldest are dropped first.
    /// </summary>
    public const int MaxHistoryPerChannel = 200;

    /// <summary>
    /// Most channels whose history one snapshot carries. Active tickets go first, then the most recently active.
    /// </summary>
    public const int MaxSnapshotChannels = 100;
}
