using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

/// <summary>
///     A stored INSFOR Default faction. The full FactionDefinition is kept as a serialized YAML
///     blob in <see cref="Data"/> so the schema can grow without a new migration every time: load
///     code fills defaults for older <see cref="SchemaVersion"/>s. The small indexed columns let
///     the editor list and look up factions without deserializing every row.
///
///     Only a known schema is ever deserialized from <see cref="Data"/>, never executable content,
///     so a stored blob cannot run code even if the row were tampered with directly.
/// </summary>
[Table("au14_faction_definitions")]
[Index(nameof(Title))]
public sealed class AU14FactionDefinition
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [StringLength(64)]
    public string Title { get; set; } = string.Empty;

    public int SchemaVersion { get; set; }

    /// <summary>
    ///     Whether this is a host-authored Default faction. Kept as a column so the same table can
    ///     hold other stored kinds later without another migration.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    ///     Serialized FactionDefinition (YAML). Parsed back into the known schema on load.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime LastEditedAt { get; set; }
}
