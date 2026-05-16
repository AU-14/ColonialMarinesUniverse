using System.Linq;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Sensor;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared.Ghost;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.TacticalMap;

public abstract partial class SharedTacticalMapSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SensorTowerSystem _sensorTowers = default!;

    public int LineLimit { get; private set; }

    public override void Initialize()
    {
        SubscribeLocalEvent<TacticalMapUserComponent, OpenTacticalMapActionEvent>(OnUserOpenAction);
        SubscribeLocalEvent<TacticalMapUserComponent, OpenTacMapAlertEvent>(OnUserOpenAlert);

        Subs.CVar(_config, RMCCVars.RMCTacticalMapLineLimit, v => LineLimit = v, true);
    }

    private void OnUserOpenAction(Entity<TacticalMapUserComponent> ent, ref OpenTacticalMapActionEvent args)
    {
        ent.Comp.Controller = args.Performer == default ? null : args.Performer;

        // Match upstream: trust the yml-configured flags (marines/xenos/opfor/govfor/clf)
        // rather than resetting and re-deriving them here. The CMU fork's previous reset
        // pattern clobbered ghost flags every open, leaving observers with an empty map.
        if (args.Performer != default && HasComp<GhostComponent>(args.Performer))
        {
            // Belt-and-suspenders: force-grant every faction + live updates to any ghost
            // that somehow lost its yml flags (e.g. from a prior buggy build).
            ent.Comp.Marines = true;
            ent.Comp.Xenos = true;
            ent.Comp.Opfor = true;
            ent.Comp.Govfor = true;
            ent.Comp.Clf = true;
            ent.Comp.LiveUpdate = true;
        }

        if (TryGetTacticalMap(out var map))
        {
            ent.Comp.Map = map.Owner;
            Dirty(ent);
            UpdateUserData(ent, map);
        }

        ToggleMapUI(ent);
    }

    private void OnUserOpenAlert(Entity<TacticalMapUserComponent> ent, ref OpenTacMapAlertEvent args)
    {
        if (TryGetTacticalMap(out var map))
            UpdateUserData(ent, map);

        ToggleMapUI(ent);
    }

    public bool TryGetTacticalMap(out Entity<TacticalMapComponent> map)
    {
        var query = EntityQueryEnumerator<TacticalMapComponent>();
        while (query.MoveNext(out var uid, out var mapComp))
        {
            map = (uid, mapComp);
            return true;
        }

        map = default;
        return false;
    }

    protected void UpdateMapData(Entity<TacticalMapComputerComponent> computer)
    {
        if (!TryGetTacticalMap(out var map))
            return;

        UpdateMapData(computer, map);
    }

    protected void UpdateMapData(Entity<TacticalMapComputerComponent> computer, TacticalMapComponent map)
    {
        // If the computer is tied to a faction, filter what we send accordingly.
        var faction = computer.Comp.Faction?.ToUpperInvariant();

        computer.Comp.Blips = new Dictionary<int, TacticalMapBlip>();

        void AddIf(Func<bool> cond, Dictionary<int, TacticalMapBlip> src)
        {
            if (!cond())
                return;
            foreach (var kv in src)
            {
                computer.Comp.Blips.TryAdd(kv.Key, kv.Value);
            }
        }

        // Helpers to check faction selection
        bool WantsMarines() => faction == null || faction == "MARINES" || faction == "UNMC" || faction == string.Empty;
        bool WantsXenos() => faction == null || faction == "XENONIDS" || faction == "XENONID" || faction == string.Empty;
        bool WantsOpfor() => faction == null || faction == "OPFOR" || faction == string.Empty;
        bool WantsGovfor() => faction == null || faction == "GOVFOR" || faction == string.Empty;
        bool WantsClf() => faction == null || faction == "CLF" || faction == string.Empty;

        // Add marine blips if desired
        AddIf(WantsMarines, map.MarineBlips);

        // Add xeno blips/structures if desired
        if (WantsXenos())
        {
            AddIf(() => true, map.XenoBlips);
            AddIf(() => true, map.XenoStructureBlips);
        }

        // Add other factions only if desired
        if (WantsOpfor())
            AddIf(() => true, map.OpforBlips);

        if (WantsGovfor())
            AddIf(() => true, map.GovforBlips);

        if (WantsClf())
            AddIf(() => true, map.ClfBlips);

            // Ensure infrastructure (comms, sensors, tunnels) is always visible on computers
        // Track their entity ids so we can exclude them from enemy-sprite replacement.
        var infraIds = new HashSet<int>();
        var commsAll = EntityQueryEnumerator<CommunicationsTowerComponent>();
        while (commsAll.MoveNext(out var commId, out var comm))
        {
            var id = commId.Id;
            infraIds.Add(id);
            var blip = FindBlipInMapStatic(id, map);
            if (blip != null)
            {
                // If the blip lacks an image, attempt to provide a comms-specific image so it doesn't render as a grey placeholder.
                var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "comms_tower");
                var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                computer.Comp.Blips.TryAdd(id, full);
            }
        }

        var sensorsAll = EntityQueryEnumerator<SensorTowerComponent>();
        while (sensorsAll.MoveNext(out var sensorId, out var sensor))
        {
            var id = sensorId.Id;
            infraIds.Add(id);
            var blip = FindBlipInMapStatic(id, map);
            if (blip != null)
            {
                var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "sensor_tower");
                var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                computer.Comp.Blips.TryAdd(id, full);
            }
        }

        var tunnelsAll = EntityQueryEnumerator<XenoTunnelComponent>();
        while (tunnelsAll.MoveNext(out var tunId, out var tun))
        {
            var id = tunId.Id;
            infraIds.Add(id);
            var blip = FindBlipInMapStatic(id, map);
            if (blip != null)
            {

                var factionHasSensors = _sensorTowers.HasOnlineSensorForFaction(faction);

                if (WantsXenos() || factionHasSensors)
                {
                    var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "tunnel");
                    var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                    computer.Comp.Blips.TryAdd(id, full);
                }
            }
        }

        void ApplyEnemySpritesToComputer(string computerFaction)
        {
            var enemyRsi = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "enemy_blip");
            var keys = computer.Comp.Blips.Keys.ToList();
            foreach (var id in keys)
            {
                // Never override infrastructure icons with the enemy sprite.
                if (infraIds.Contains(id))
                    continue;

                bool isFriendly = false;
                if (string.IsNullOrEmpty(computerFaction))
                {
                    isFriendly = true; // showing all, no need to mark
                }
                else
                {
                    var up = computerFaction.ToUpperInvariant();
                    if (up == "MARINES")
                        isFriendly = map.MarineBlips.ContainsKey(id);
                    else if (up == "OPFOR")
                        isFriendly = map.OpforBlips.ContainsKey(id);
                    else if (up == "GOVFOR")
                        isFriendly = map.GovforBlips.ContainsKey(id);
                    else if (up == "CLF")
                        isFriendly = map.ClfBlips.ContainsKey(id);
                }

                if (!isFriendly)
                {
                    var orig = computer.Comp.Blips[id];
                    var enemy = new TacticalMapBlip(orig.Indices, enemyRsi, orig.Color, orig.Status, orig.Background, false);
                    computer.Comp.Blips[id] = enemy;
                }
            }
        }

        // Only apply enemy sprites on computers if that faction actually controls active sensors.
        // Without sensors we should not mark non-friendly humans as enemy on the canvas.
        if (faction != null && _sensorTowers.HasOnlineSensorForFaction(faction))
            ApplyEnemySpritesToComputer(faction);

        Dirty(computer);

        var lines = EnsureComp<TacticalMapLinesComponent>(computer);
        // Clear and set only the lines we want
        lines.MarineLines = WantsMarines() ? map.MarineLines : new();
        lines.XenoLines = WantsXenos() ? map.XenoLines : new();
        lines.OpforLines = WantsOpfor() ? map.OpforLines : new();
        lines.GovforLines = WantsGovfor() ? map.GovforLines : new();
        lines.ClfLines = WantsClf() ? map.ClfLines : new();
        Dirty(computer, lines);

        var labels = EnsureComp<TacticalMapLabelsComponent>(computer);
        labels.MarineLabels = WantsMarines() ? map.MarineLabels : new();
        labels.XenoLabels = WantsXenos() ? map.XenoLabels : new();
        labels.OpforLabels = WantsOpfor() ? map.OpforLabels : new();
        labels.GovforLabels = WantsGovfor() ? map.GovforLabels : new();
        labels.ClfLabels = WantsClf() ? map.ClfLabels : new();
        Dirty(computer, labels);
    }

    public void OpenComputerMap(Entity<TacticalMapComputerComponent?> computer, EntityUid user)
    {
        if (!Resolve(computer, ref computer.Comp, false))
            return;

        _ui.TryOpenUi(computer.Owner, TacticalMapComputerUi.Key, user);
        UpdateMapData((computer, computer.Comp));
    }

    public virtual void UpdateUserData(Entity<TacticalMapUserComponent> user, TacticalMapComponent map)
    {
    }

    private void ToggleMapUI(Entity<TacticalMapUserComponent> user)
    {
        if (_ui.IsUiOpen(user.Owner, TacticalMapUserUi.Key, user))
        {
            _ui.CloseUi(user.Owner, TacticalMapUserUi.Key, user);
            return;
        }

        _ui.TryOpenUi(user.Owner, TacticalMapUserUi.Key, user);
    }

    // Helper for shared code: Find blip in a map component by entity id
    private static TacticalMapBlip? FindBlipInMapStatic(int entityId, TacticalMapComponent map)
    {
        if (map.MarineBlips.TryGetValue(entityId, out var marineBlip))
            return marineBlip;
        if (map.XenoStructureBlips.TryGetValue(entityId, out var structureBlip))
            return structureBlip;
        if (map.XenoBlips.TryGetValue(entityId, out var xenoBlip))
            return xenoBlip;

        if (map.OpforBlips.TryGetValue(entityId, out var opforBlip))
            return opforBlip;
        if (map.GovforBlips.TryGetValue(entityId, out var govforBlip))
            return govforBlip;
        if (map.ClfBlips.TryGetValue(entityId, out var clfBlip))
            return clfBlip;
        return null;
    }
}
