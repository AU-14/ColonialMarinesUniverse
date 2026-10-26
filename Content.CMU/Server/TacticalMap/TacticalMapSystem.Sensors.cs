using System.Linq;
using Content.Shared._RMC14.TacticalMap;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    private void AddSensorContacts(string faction, Dictionary<int, TacticalMapBlip> blips,
        TacticalMapComponent map, HashSet<int> infrastructure)
    {
        if (!TeamHasActiveSensors(faction))
        {
            // Published sensor contacts must stop revealing threats when the array is
            // switched off or captured. Infrastructure remains public.
            foreach (var id in blips.Keys.ToArray())
            {
                if (!infrastructure.Contains(id) &&
                    (map.XenoBlips.ContainsKey(id) || map.XenoStructureBlips.ContainsKey(id)))
                    blips.Remove(id);
            }
            return;
        }

        foreach (var source in new[] { map.MarineBlips, map.OpforBlips, map.GovforBlips,
                     map.ClfBlips, map.XenoBlips, map.XenoStructureBlips })
        {
            foreach (var (id, blip) in source)
                blips.TryAdd(id, blip);
        }
    }
}
