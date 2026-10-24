using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

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

    // CMU14: clan hall. sigil is a clan mask icon id, notice is the clan's pinned message,
    // elite notes are only shown to Elite and up
    [Required]
    public string Sigil { get; set; } = string.Empty;

    [Required]
    public string Notice { get; set; } = string.Empty;

    [Required]
    public string EliteNotes { get; set; } = string.Empty;

    public List<YautjaClanMember> Members { get; set; } = new();
}

// CMU14: a clanless hunter asking to join a clan. one open petition per player, no FKs on
// purpose, clans are deactivated rather than deleted and the disband path clears these
[Table("yautja_clan_petition")]
[Index(nameof(PlayerUserId), IsUnique = true)]
public sealed class YautjaClanPetition
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid PlayerUserId { get; set; }

    public int ClanId { get; set; }
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
    bool Active,
    string Sigil = "",
    string Notice = "",
    string EliteNotes = "");

public sealed record YautjaClanPetitionRecord(Guid PlayerUserId, int ClanId);

// a hunter row for the hall's boards, named with the last seen username so offline hunters read right
public sealed record YautjaHunterBoardRecord(
    Guid PlayerUserId,
    string Name,
    int? ClanId,
    int Rank,
    int Permissions,
    int Honor,
    bool IsLegacy,
    int WhitelistFlags);

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
