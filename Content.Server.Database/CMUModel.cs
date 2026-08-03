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

[Table("cmu_balance_rating_polls")]
[Index(nameof(RoundId))]
[Index(nameof(Target), nameof(TargetId), nameof(Metric))]
[Index(nameof(OpenedAt))]
public sealed class CMUBalanceRatingPoll
{
    public const int MetricMaxLength = 16;
    public const int TargetMaxLength = 16;
    public const int TargetIdMaxLength = 96;

    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [ForeignKey(nameof(Round))]
    public int RoundId { get; set; }

    public Round Round { get; set; } = default!;

    [StringLength(TargetMaxLength)]
    public string Target { get; set; } = string.Empty;

    [StringLength(TargetIdMaxLength)]
    public string TargetId { get; set; } = string.Empty;

    [StringLength(MetricMaxLength)]
    public string Metric { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedBy))]
    public Guid? CreatedById { get; set; }

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
    [ForeignKey(nameof(Poll))]
    public long PollId { get; set; }

    public CMUBalanceRatingPoll Poll { get; set; } = default!;

    [ForeignKey(nameof(Player))]
    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = default!;

    public byte Rating { get; set; }

    public DateTime RecordedAt { get; set; }
}

public partial class Profile
{
    public string? RegulationHairName { get; set; }
    public string? RegulationHairColor { get; set; }
    public string? RegulationFacialHairName { get; set; }
    public string? RegulationFacialHairColor { get; set; }
    public string? Allegiance { get; set; }
    public string? Origin { get; set; }
    public string? Platoon { get; set; }
    public bool Synthetic { get; set; }
    public string? ThreatPreference { get; set; }
    public string? GamemodeJobPriorities { get; set; }
    public string? GamemodeAntagPreferences { get; set; }
    public string? GamemodeThreatPreferences { get; set; }
    public string ShortExamine { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public string MedicalRecord { get; set; } = string.Empty;
    public string CriminalRecord { get; set; } = string.Empty;
    public string GeneralRecord { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
    public int Weight { get; set; } = 160;
    public string Build { get; set; } = string.Empty;
    public bool HideMetaInformation { get; set; }
}

internal static class CMUModelConfiguration
{
    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>().Property(profile => profile.ShortExamine).HasDefaultValue(string.Empty);
        modelBuilder.Entity<Profile>().Property(profile => profile.FullDescription).HasDefaultValue(string.Empty);
        modelBuilder.Entity<Profile>().Property(profile => profile.MedicalRecord).HasDefaultValue(string.Empty);
        modelBuilder.Entity<Profile>().Property(profile => profile.CriminalRecord).HasDefaultValue(string.Empty);
        modelBuilder.Entity<Profile>().Property(profile => profile.GeneralRecord).HasDefaultValue(string.Empty);
        modelBuilder.Entity<Profile>().Property(profile => profile.Height).HasDefaultValue(string.Empty);
        modelBuilder.Entity<Profile>().Property(profile => profile.Weight).HasDefaultValue(160);
        modelBuilder.Entity<Profile>().Property(profile => profile.Build).HasDefaultValue(string.Empty);
        modelBuilder.Entity<Profile>().Property(profile => profile.HideMetaInformation).HasDefaultValue(false);
    }
}
