// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Content.Server.Database
{
    // AU14 building overhaul: admin-generated construction-menu entries. Interface + manager
    // wrappers kept in a partial file so the shared DB manager is not churned. Mirrors the
    // existing RMC / AU14 faction wrapper pattern exactly.
    public partial interface IServerDbManager
    {
        Task<List<AU14CustomConstructionEntry>> GetCustomConstructionEntries();
        Task UpsertCustomConstructionEntry(string kind, string entryKey, string yaml);
        Task<bool> DeleteCustomConstructionEntry(string kind, string entryKey);
    }

    public sealed partial class ServerDbManager
    {
        public Task<List<AU14CustomConstructionEntry>> GetCustomConstructionEntries()
        {
            DbReadOpsMetric.Inc();
            return RunDbCommand(() => _db.GetCustomConstructionEntries());
        }

        public Task UpsertCustomConstructionEntry(string kind, string entryKey, string yaml)
        {
            DbWriteOpsMetric.Inc();
            return RunDbCommand(() => _db.UpsertCustomConstructionEntry(kind, entryKey, yaml));
        }

        public Task<bool> DeleteCustomConstructionEntry(string kind, string entryKey)
        {
            DbWriteOpsMetric.Inc();
            return RunDbCommand(() => _db.DeleteCustomConstructionEntry(kind, entryKey));
        }
    }
}
