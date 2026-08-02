using System.Collections.Generic;
using System.Threading.Tasks;

namespace Content.Server.Database
{
    // AU14 INSFOR faction featureset. Interface + manager wrappers kept in a partial file so the
    // shared DB manager is not churned. Mirrors the existing RMC wrapper pattern exactly.
    public partial interface IServerDbManager
    {
        Task<List<AU14FactionDefinition>> GetFactionDefinitions();
        Task<AU14FactionDefinition?> GetFactionDefinition(int id);
        Task<int> AddFactionDefinition(AU14FactionDefinition definition);
        Task<bool> UpdateFactionDefinition(AU14FactionDefinition definition);
        Task<bool> DeleteFactionDefinition(int id);
    }

    public sealed partial class ServerDbManager
    {
        public Task<List<AU14FactionDefinition>> GetFactionDefinitions()
        {
            DbReadOpsMetric.Inc();
            return RunDbCommand(() => _db.GetFactionDefinitions());
        }

        public Task<AU14FactionDefinition?> GetFactionDefinition(int id)
        {
            DbReadOpsMetric.Inc();
            return RunDbCommand(() => _db.GetFactionDefinition(id));
        }

        public Task<int> AddFactionDefinition(AU14FactionDefinition definition)
        {
            DbWriteOpsMetric.Inc();
            return RunDbCommand(() => _db.AddFactionDefinition(definition));
        }

        public Task<bool> UpdateFactionDefinition(AU14FactionDefinition definition)
        {
            DbWriteOpsMetric.Inc();
            return RunDbCommand(() => _db.UpdateFactionDefinition(definition));
        }

        public Task<bool> DeleteFactionDefinition(int id)
        {
            DbWriteOpsMetric.Inc();
            return RunDbCommand(() => _db.DeleteFactionDefinition(id));
        }
    }
}
