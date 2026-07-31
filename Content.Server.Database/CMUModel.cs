using System;
using System.Collections.Generic;
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
    [StringLength(64)] public string PresetId { get; set; } = string.Empty;
    [StringLength(64)] public string Winner { get; set; } = string.Empty;
    [StringLength(96)] public string Outcome { get; set; } = string.Empty;
    [StringLength(96)] public string Source { get; set; } = string.Empty;
    [StringLength(96)] public string? SelectedThreatId { get; set; }
    [StringLength(96)] public string? PlanetId { get; set; }
    [StringLength(96)] public string? GovforPlatoonId { get; set; }
    [StringLength(96)] public string? OpforPlatoonId { get; set; }
    public int PlayerCount { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime RecordedAt { get; set; }
}

[Table("cmu_balance_rating_polls")]
[Index(nameof(RoundId))]
[Index(nameof(Target), nameof(TargetId), nameof(Metric))]
[Index(nameof(OpenedAt))]
public sealed class CMUBalanceRatingPoll
{
    public const int MetricMaxLength = 16;
    public const int TargetMaxLength = 16;
    public const int TargetIdMaxLength = 96;
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; }
    [ForeignKey(nameof(Round))] public int RoundId { get; set; }
    public Round Round { get; set; } = default!;
    [StringLength(TargetMaxLength)] public string Target { get; set; } = string.Empty;
    [StringLength(TargetIdMaxLength)] public string TargetId { get; set; } = string.Empty;
    [StringLength(MetricMaxLength)] public string Metric { get; set; } = string.Empty;
    [ForeignKey(nameof(CreatedBy))] public Guid? CreatedById { get; set; }
    public Player? CreatedBy { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<CMUBalanceRatingResponse> Responses { get; set; } = default!;
}

[Table("cmu_balance_rating_responses")]
[PrimaryKey(nameof(PollId), nameof(PlayerId))]
[Index(nameof(PlayerId))]
[Index(nameof(RecordedAt))]
public sealed class CMUBalanceRatingResponse
{
    [ForeignKey(nameof(Poll))] public long PollId { get; set; }
    public CMUBalanceRatingPoll Poll { get; set; } = default!;
    [ForeignKey(nameof(Player))] public Guid PlayerId { get; set; }
    public Player Player { get; set; } = default!;
    public byte Rating { get; set; }
    public DateTime RecordedAt { get; set; }
}
