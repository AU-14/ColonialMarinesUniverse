// CMU14: discord account linking result records
using System;

namespace Content.Server.Database;

public enum DiscordLinkStatus
{
    /// <summary>Player linked; the code was consumed.</summary>
    Success,

    /// <summary>No code row matched; nothing changed.</summary>
    UnknownCode,

    /// <summary>Code existed but was older than the expiry window; the row was consumed.</summary>
    ExpiredCode,
}

/// <summary>Result of one linking attempt through <see cref="ServerDbBase.LinkDiscordAccountByCode"/>.</summary>
public sealed record DiscordLinkResult(DiscordLinkStatus Status, Guid PlayerId, string UserName);
