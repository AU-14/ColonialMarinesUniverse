using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

// CMU14: round-start antag draw pity, persisted across server restarts.
public sealed record CmuAntagPityEntry(Guid PlayerUserId, int Missed, long LastSeen);

public abstract partial class ServerDbBase
{
    public async Task<List<CmuAntagPityEntry>> GetCmuAntagPityAsync(long pruneBefore)
    {
        await using var db = await GetDb();

        // Entries idle since before the cutoff are dead weight; drop them on load.
        await db.DbContext.Database.ExecuteSqlRawAsync(
            $"DELETE FROM cmu_antag_pity WHERE last_seen < {pruneBefore}");

        var entries = await db.DbContext.Database
            .SqlQueryRaw<CmuAntagPityRow>(
                // Column names must match the ad-hoc entity mapping: the snake-case
                // naming convention applies to SqlQueryRaw types too, so required
                // columns are snake_case — aliases here must be the raw names.
                "SELECT player_user_id, missed, last_seen FROM cmu_antag_pity")
            .ToListAsync();

        return entries.Select(row => new CmuAntagPityEntry(row.PlayerUserId, row.Missed, row.LastSeen)).ToList();
    }

    public async Task SaveCmuAntagPityAsync(CmuAntagPityEntry entry)
    {
        await using var db = await GetDb();

        await db.DbContext.Database.ExecuteSqlRawAsync(
            $"INSERT INTO cmu_antag_pity (player_user_id, missed, last_seen) " +
            $"VALUES ({entry.PlayerUserId}, {entry.Missed}, {entry.LastSeen}) " +
            "ON CONFLICT (player_user_id) DO UPDATE SET missed = EXCLUDED.missed, last_seen = EXCLUDED.last_seen");
    }

    private sealed class CmuAntagPityRow
    {
        public Guid PlayerUserId { get; set; }
        public int Missed { get; set; }
        public long LastSeen { get; set; }
    }
}
