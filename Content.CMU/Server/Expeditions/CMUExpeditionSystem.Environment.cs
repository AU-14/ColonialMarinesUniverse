using System.Numerics;
using Content.Server._RMC14.Marines;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._NC14.DayNightCycle;
using Content.Shared.Atmos;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Gravity;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionSystem
{
    [Dependency] private MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private bool BuildAirspace(BuildJob job, CMUExpeditionMapComponent expedition)
    {
        if (expedition.ZNetwork == null)
        {
            var network = _zLevels.CreateZNetwork();
            expedition.ZNetwork = network;
            var members = new Dictionary<EntityUid, int> { [job.Map] = 0 };
            for (var depth = 1; depth <= 2; depth++)
            {
                var upper = _map.CreateMap(runMapInit: false);
                expedition.UpperMaps.Add(upper);
                EnsureComp<MapGridComponent>(upper);
                var gravity = EnsureComp<GravityComponent>(upper);
                gravity.Enabled = gravity.Inherent = true;
                Dirty(upper, gravity);
                var air = new GasMixture(2500) { Temperature = Atmospherics.T20C };
                air.SetMoles(Gas.Oxygen, 21.824779f);
                air.SetMoles(Gas.Nitrogen, 82.10312f);
                _atmos.SetMapAtmosphere(upper, false, air);
                _metadata.SetEntityName(upper, Loc.GetString("cmu-expedition-airspace", ("seed", job.Plan.Seed), ("level", depth)));
                members[upper] = depth;
            }
            if (!_zLevels.TryAddMapsIntoZNetwork(network, members))
                throw new InvalidOperationException("Could not link expedition airspace.");
        }

        // Empty upper tiles are open air. Rock masses continue vertically, with a solid summit.
        // Batch collision entities just like surface scenery instead of blocking a server tick.
        var spawned = 0;
        var plan = job.Plan;
        while (job.AirspaceCursor < plan.Terrain.Length && spawned < ObjectBudget)
        {
            var i = job.AirspaceCursor++;
            if (plan.Terrain[i] != CMUExpeditionTerrain.Cliff)
                continue;
            var indices = new Vector2i(i % plan.Size, i / plan.Size);
            foreach (var upper in expedition.UpperMaps)
                _map.SetTile(upper, Comp<MapGridComponent>(upper), indices, new Tile(job.Tiles[CMUExpeditionTerrain.Stone].TileId));
            Spawn("CMUExpeditionRock", new EntityCoordinates(expedition.UpperMaps[0], new Vector2(indices.X + 0.5f, indices.Y + 0.5f)));
            spawned++;
        }
        if (job.AirspaceCursor < plan.Terrain.Length)
            return false;
        foreach (var upper in expedition.UpperMaps)
            _map.InitializeMap(upper);
        return true;
    }

    private void InitializeExpeditionLighting(EntityUid map, CMUExpeditionMapComponent expedition)
    {
        // One phase for every deck; a seed chooses morning, noon, dusk or night.
        var phase = new[] { 0.33f, 0.5f, 0.75f, 0.92f }[CMUExpeditionGenerator.Hash(expedition.Plan.Seed, 0, 0, 910) % 4];
        SetExpeditionTime(map, phase * 24);
    }

    public bool SetExpeditionTime(EntityUid map, float hour)
    {
        if (!float.IsFinite(hour) || hour < 0 || hour >= 24 || !TryComp<CMUExpeditionMapComponent>(map, out var expedition) || !expedition.Ready)
            return false;
        var maps = new List<EntityUid>(expedition.UpperMaps) { map };
        foreach (var member in maps)
        {
            EnsureComp<MapLightComponent>(member);
            var cycle = EnsureComp<DayNightCycleComponent>(member);
            cycle.CurrentCycleTime = hour / 24;
            Dirty(member, cycle);
        }
        return true;
    }

    private void AnnounceExpedition(EntityUid map, CMUExpeditionMapComponent expedition)
    {
        _marineAnnounce.AnnounceARES(map, Loc.GetString("cmu-expedition-announcement",
                ("sector", Name(map)), ("lz", Name(expedition.LandingBeacon!.Value))),
            new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg"), faction: "govfor");
    }

    private void OnExpeditionShutdown(Entity<CMUExpeditionMapComponent> ent, ref ComponentShutdown args)
    {
        foreach (var upper in ent.Comp.UpperMaps)
            QueueDel(upper);
        if (ent.Comp.ZNetwork is { } network)
            QueueDel(network);
    }
}
