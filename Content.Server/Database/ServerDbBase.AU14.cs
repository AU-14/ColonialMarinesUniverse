using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

// AU14 INSFOR faction featureset CRUD. Kept in its own partial file so the shared DB base is not
// churned. All access goes through EFCore (parameterized by design); no raw SQL. Single-row writes
// so SaveChanges is atomic on its own.
public abstract partial class ServerDbBase
{
    public async Task<List<AU14FactionDefinition>> GetFactionDefinitions()
    {
        await using var db = await GetDb();
        return await db.DbContext.AU14FactionDefinitions
            .OrderBy(f => f.Title)
            .ToListAsync();
    }

    public async Task<AU14FactionDefinition?> GetFactionDefinition(int id)
    {
        await using var db = await GetDb();
        return await db.DbContext.AU14FactionDefinitions
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<int> AddFactionDefinition(AU14FactionDefinition definition)
    {
        await using var db = await GetDb();
        db.DbContext.AU14FactionDefinitions.Add(definition);
        await db.DbContext.SaveChangesAsync();
        return definition.Id;
    }

    /// <summary>
    ///     Overwrites the stored fields for an existing faction. Returns false if the id is gone.
    /// </summary>
    public async Task<bool> UpdateFactionDefinition(AU14FactionDefinition definition)
    {
        await using var db = await GetDb();
        var existing = await db.DbContext.AU14FactionDefinitions
            .FirstOrDefaultAsync(f => f.Id == definition.Id);
        if (existing == null)
            return false;

        existing.Title = definition.Title;
        existing.SchemaVersion = definition.SchemaVersion;
        existing.IsDefault = definition.IsDefault;
        existing.Data = definition.Data;
        existing.LastEditedAt = definition.LastEditedAt;
        await db.DbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteFactionDefinition(int id)
    {
        await using var db = await GetDb();
        var existing = await db.DbContext.AU14FactionDefinitions
            .FirstOrDefaultAsync(f => f.Id == id);
        if (existing == null)
            return false;

        db.DbContext.AU14FactionDefinitions.Remove(existing);
        await db.DbContext.SaveChangesAsync();
        return true;
    }
}
