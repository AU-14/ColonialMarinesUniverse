using System.Linq;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Sensor;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared._RMC14.Xenonids.Hive;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    private void AddSensorContacts(string faction, Dictionary<int, TacticalMapBlip> blips,
        TacticalMapComponent map)
    {
        if (!TeamHasActiveSensors(faction))
        {
            // Published sensor contacts must stop revealing threats when the array is
            // switched off or captured. Infrastructure remains public.
            foreach (var id in blips.Keys.ToArray())
            {
                if (!IsTacticalInfrastructure(id) &&
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

    private bool IsTacticalInfrastructure(int id)
    {
        var uid = new EntityUid(id);
        return (HasComp<CommunicationsTowerComponent>(uid) ||
                HasComp<SensorTowerComponent>(uid) || HasComp<XenoTunnelComponent>(uid))
            && !MetaData(uid).EntityPaused;
    }

    private bool IsTacticalHiveMember(int id, EntityUid? hive)
    {
        var uid = new EntityUid(id);
        // Paused hive members do not contribute published contacts.
        return hive != null && TryComp<HiveMemberComponent>(uid, out var member) &&
            member.Hive == hive && !MetaData(uid).EntityPaused;
    }
}
