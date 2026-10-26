using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Robust.Shared.Utility;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    private async Task SaveAdminLogsIdempotently(List<AdminLog> logs)
    {
        DebugTools.Assert(logs.All(log => log.RoundId > 0), "Adding logs with invalid round ids.");
        var delay = TimeSpan.FromSeconds(5);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var db = await GetDb();
                var pending = new List<AdminLog>(logs.Count);
                foreach (var round in logs.GroupBy(log => log.RoundId))
                {
                    var ids = round.Select(log => log.Id).ToArray();
                    var committed = await db.DbContext.AdminLog.AsNoTracking()
                        .Where(log => log.RoundId == round.Key && ids.Contains(log.Id))
                        .Include(log => log.Players)
                        .ToDictionaryAsync(log => log.Id);
                    foreach (var log in round)
                    {
                        if (!committed.TryGetValue(log.Id, out var existing))
                        {
                            pending.Add(log);
                            continue;
                        }

                        // A timeout can occur after commit. Only an identical audit record is a replay;
                        // an ID collision must never silently discard or overwrite a different event.
                        if (!SameAdminLog(existing, log))
                            throw new InvalidOperationException($"Admin log {log.RoundId}:{log.Id} already exists with different data; retaining the batch.");
                    }
                }

                if (pending.Count > 0)
                {
                    db.DbContext.AdminLog.AddRange(pending);
                    await db.DbContext.SaveChangesAsync();
                }

                _opsLog.Debug($"Saved {pending.Count} admin logs; {logs.Count - pending.Count} were already committed.");
                return;
            }
            catch (Exception ex) when (attempt < 5 && RetryAdminLogWrite(ex))
            {
                _opsLog.Warning($"Admin log write attempt {attempt} failed; retrying in {delay.TotalSeconds} seconds: {ex.Message}");
                await Task.Delay(delay);
                delay *= 2;
            }
        }
    }

    private static bool SameAdminLog(AdminLog existing, AdminLog incoming)
    {
        // PostgreSQL timestamps retain microseconds rather than DateTime's 100 ns precision.
        return existing.Type == incoming.Type && existing.Impact == incoming.Impact &&
               existing.Date.Ticks / 10 == incoming.Date.Ticks / 10 && existing.Message == incoming.Message &&
               JsonElement.DeepEquals(existing.Json.RootElement, incoming.Json.RootElement) &&
               existing.Players.Select(player => player.PlayerUserId).ToHashSet()
                   .SetEquals(incoming.Players.Select(player => player.PlayerUserId));
    }

    private static bool RetryAdminLogWrite(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is TimeoutException || current is NpgsqlException { IsTransient: true } ||
                current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } ||
                current is SqliteException { SqliteErrorCode: 5 or 6 } ||
                current is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
                return true;
        }

        return false;
    }
}
