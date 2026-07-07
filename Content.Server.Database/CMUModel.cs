using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

[Table("cmu_round_outcomes")]
[Index(nameof(PresetId))]
[Index(nameof(SelectedThreatId))]
[Index(nameof(RecordedAt))]
public sealed class CMURoundOutcome
{
    [Key, ForeignKey(nameof(Round))]
    public int RoundId { get; set; }

    public Round Round { get; set; } = default!;

    [StringLength(64)]
    public string PresetId { get; set; } = string.Empty;

    [StringLength(64)]
    public string Winner { get; set; } = string.Empty;

    [StringLength(96)]
    public string Outcome { get; set; } = string.Empty;

    [StringLength(96)]
    public string Source { get; set; } = string.Empty;

    [StringLength(96)]
    public string? SelectedThreatId { get; set; }

    [StringLength(96)]
    public string? PlanetId { get; set; }

    [StringLength(96)]
    public string? GovforPlatoonId { get; set; }

    [StringLength(96)]
    public string? OpforPlatoonId { get; set; }

    public int PlayerCount { get; set; }

    public int DurationSeconds { get; set; }

    public DateTime RecordedAt { get; set; }
}
