// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

/// <summary>
///     A stored admin-generated construction-menu entry (item recipe, tile, lathe recipe, or menu
///     overrides). The generated prototype YAML is self-contained and self-describing, so it is kept
///     verbatim in <see cref="Yaml"/>; the (Kind, EntryKey) pair mirrors the generated file layout
///     (subdirectory + file stem) and uniquely identifies each entry for upsert/delete.
///
///     This is the durable store: the Docker filesystem is wiped on every patch, so the generated
///     files under Resources are re-created from these rows on startup. Only prototype YAML of known
///     types is ever loaded from <see cref="Yaml"/>, never executable content.
/// </summary>
[Table("au14_custom_construction_entries")]
[Index(nameof(Kind), nameof(EntryKey), IsUnique = true)]
public sealed class AU14CustomConstructionEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    ///     Which generated subdirectory the YAML belongs to: "" (root construction entries),
    ///     "Tiles", "Lathe", or "Overrides".
    /// </summary>
    [StringLength(32)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    ///     The logical entry key: the generated file stem, e.g.
    ///     "AU14Custom_&lt;entity&gt;__&lt;spawnlist&gt;__&lt;category&gt;".
    /// </summary>
    [StringLength(256)]
    public string EntryKey { get; set; } = string.Empty;

    /// <summary>
    ///     The complete generated prototype YAML document (identical to the .yml file contents).
    /// </summary>
    public string Yaml { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime LastEditedAt { get; set; }
}
