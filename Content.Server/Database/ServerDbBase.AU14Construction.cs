// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

// AU14 building overhaul: admin-generated construction-menu entry CRUD. Kept in its own partial
// file so the shared DB base is not churned. All access goes through EFCore (parameterized by
// design); no raw SQL. Single-row writes so SaveChanges is atomic on its own.
public abstract partial class ServerDbBase
{
    public async Task<List<AU14CustomConstructionEntry>> GetCustomConstructionEntries()
    {
        await using var db = await GetDb();
        return await db.DbContext.AU14CustomConstructionEntries
            .OrderBy(e => e.Kind)
            .ThenBy(e => e.EntryKey)
            .ToListAsync();
    }

    /// <summary>
    ///     Inserts or overwrites the entry identified by (kind, entryKey). Upsert semantics match
    ///     the file store: writing the same key replaces the previous YAML.
    /// </summary>
    public async Task UpsertCustomConstructionEntry(string kind, string entryKey, string yaml)
    {
        await using var db = await GetDb();
        var existing = await db.DbContext.AU14CustomConstructionEntries
            .FirstOrDefaultAsync(e => e.Kind == kind && e.EntryKey == entryKey);

        var now = DateTime.UtcNow;
        if (existing == null)
        {
            db.DbContext.AU14CustomConstructionEntries.Add(new AU14CustomConstructionEntry
            {
                Kind = kind,
                EntryKey = entryKey,
                Yaml = yaml,
                CreatedAt = now,
                LastEditedAt = now,
            });
        }
        else
        {
            existing.Yaml = yaml;
            existing.LastEditedAt = now;
        }

        await db.DbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteCustomConstructionEntry(string kind, string entryKey)
    {
        await using var db = await GetDb();
        var existing = await db.DbContext.AU14CustomConstructionEntries
            .FirstOrDefaultAsync(e => e.Kind == kind && e.EntryKey == entryKey);
        if (existing == null)
            return false;

        db.DbContext.AU14CustomConstructionEntries.Remove(existing);
        await db.DbContext.SaveChangesAsync();
        return true;
    }
}
