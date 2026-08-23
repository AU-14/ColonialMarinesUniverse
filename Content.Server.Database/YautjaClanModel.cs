using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Content.Server.Database;

[Table("yautja_clan")]
public sealed class YautjaClan
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public int Honor { get; set; }

    [Required]
    public string Color { get; set; } = "#ffffff";

    public bool Active { get; set; } = true;

    public List<YautjaClanMember> Members { get; set; } = new();
}

[Table("yautja_clan_member")]
public sealed class YautjaClanMember
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public Guid PlayerUserId { get; set; }

    public Player Player { get; set; } = null!;

    public int? ClanId { get; set; }

    public YautjaClan? Clan { get; set; }

    public int Rank { get; set; }

    public int Permissions { get; set; }

    public int Honor { get; set; }

    public bool IsLegacy { get; set; }
}

public sealed record YautjaClanRecord(
    int Id,
    string Name,
    string Description,
    int Honor,
    string Color,
    bool Active);

public sealed record YautjaClanMemberRecord(
    Guid PlayerUserId,
    int? ClanId,
    int Rank,
    int Permissions,
    int Honor,
    bool IsLegacy);

public sealed record YautjaWhitelistHolderRecord(
    Guid PlayerUserId,
    string Name,
    int? Rank,
    int WhitelistFlags);

public sealed record YautjaClanDeleteResult(
    bool Succeeded,
    List<Guid> DetachedPlayers);
