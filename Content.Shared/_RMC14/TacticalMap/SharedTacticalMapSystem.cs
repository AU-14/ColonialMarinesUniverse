using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Sensor;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.TacticalMap;

public abstract partial class SharedTacticalMapSystem : EntitySystem // CMU14 Class: Custom Factions
{
    public const string MarinesFaction = "MARINES";
    public const string XenosFaction = "XENONIDS";
    public const string OpforFaction = "OPFOR";
    public const string GovforFaction = "GOVFOR";
    public const string ClfFaction = "CLF";
    public const string WeYuFaction = "WEYU";

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SensorTowerSystem _sensorTowers = default!;

    private readonly Stack<Dictionary<int, TacticalMapBlip>> _computerBlipBuffers = new();
    private static readonly SpriteSpecifier.Rsi CommsIcon = new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "comms_tower");
    private static readonly SpriteSpecifier.Rsi SensorIcon = new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "sensor_tower");
    private static readonly SpriteSpecifier.Rsi TunnelIcon = new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "tunnel");
    private static readonly SpriteSpecifier.Rsi EnemyIcon = new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "enemy_blip");

    public int LineLimit { get; private set; }

    // CMU14: Ships loaded for either round faction configure their consoles through their owner.
    public void SetComputerFaction(Entity<TacticalMapComputerComponent> computer, string? faction)
    {
        computer.Comp.Faction = NormalizeMapFaction(faction);
        Dirty(computer);
    }

    public static bool TryNormalizeHumanFaction(string? faction, out string normalized)
    {
        normalized = MarinesFaction;

        if (string.IsNullOrWhiteSpace(faction))
            return true;

        var key = faction.Trim().ToUpperInvariant();
        if (key is "MARINE" or "MARINES" or "UNMC")
            return true;

        if (key.Contains(ClfFaction))
        {
            normalized = ClfFaction;
            return true;
        }

        if (key.Contains("OPF"))
        {
            normalized = OpforFaction;
            return true;
        }

        if (key.Contains("GOV"))
        {
            normalized = GovforFaction;
            return true;
        }

        if (key.Contains("WEYU") || key == "WY")
        {
            normalized = WeYuFaction;
            return true;
        }

        return false;
    }

    public static string NormalizeHumanFaction(string? faction)
    {
        return TryNormalizeHumanFaction(faction, out var normalized)
            ? normalized
            : MarinesFaction;
    }

    public static string? NormalizeMapFaction(string? faction)
    {
        if (string.IsNullOrWhiteSpace(faction))
            return null;

        var key = faction.Trim().ToUpperInvariant();
        if (key is "XENO" or "XENOS" or "XENONID" || key == XenosFaction)
            return XenosFaction;

        return TryNormalizeHumanFaction(key, out var normalized)
            ? normalized
            : key;
    }

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
            ent.Comp.WeYu = true; // CMU14
            ent.Comp.Abomination = true; // CMU14
            ent.Comp.Yautja = true; // CMU14
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

    // CMU14 method: draws a rectangle of line segments into the shared line list (FoF KoTH)
    public void DrawTacticalMapRectangle(Color color, Vector2 center, int halfWidth, int halfHeight, float thickness = 3f)
    {
        if (!TryGetTacticalMap(out var map))
            return;

        var corners = new List<Vector2>(4);
        corners.Add(new Vector2(center.X - halfWidth, center.Y - halfHeight));
        corners.Add(new Vector2(center.X + halfWidth, center.Y - halfHeight));
        corners.Add(new Vector2(center.X + halfWidth, center.Y + halfHeight));
        corners.Add(new Vector2(center.X - halfWidth, center.Y + halfHeight));

        var rect = new List<TacticalMapLine>(4);
        for (var i = 0; i < 4; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % 4];
            rect.Add(new TacticalMapLine(new Vector2i((int) a.X, (int) a.Y), new Vector2i((int) b.X, (int) b.Y), color, thickness));
        }

        // replace previous rect of same color
        map.Comp.SharedLines.RemoveAll(l => l.Color == color);
        map.Comp.SharedLines.AddRange(rect);

        Dirty(map);
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
        var faction = NormalizeMapFaction(computer.Comp.Faction);

        // CMU14: use the same faction selection for blips, lines, and labels.
        bool WantsMarines() => faction == null || faction == MarinesFaction;
        bool WantsXenos() => faction == null || faction == XenosFaction;
        bool WantsOpfor() => faction == null || faction == OpforFaction;
        bool WantsGovfor() => faction == null || faction == GovforFaction;
        bool WantsClf() => faction == null || faction == ClfFaction;
        bool WantsWeYu() => faction == null || faction == WeYuFaction;

        var blips = _computerBlipBuffers.TryPop(out var buffer) ? buffer : new Dictionary<int, TacticalMapBlip>();
        try
        {

            void AddIf(bool include, Dictionary<int, TacticalMapBlip> src)
            {
                if (!include)
                    return;
                foreach (var kv in src)
                {
                    blips.TryAdd(kv.Key, kv.Value);
                }
            }

            var sensorsOnline = faction != null && _sensorTowers.HasOnlineSensorForFaction(faction);

            // Add marine blips if desired
            AddIf(WantsMarines() || sensorsOnline, map.MarineBlips);

            // Add xeno blips/structures if desired
            if (WantsXenos())
            {
                AddIf(true, map.XenoBlips);
                AddIf(true, map.XenoStructureBlips);
            }

            // Add other factions only if desired
            AddIf(WantsOpfor() || sensorsOnline, map.OpforBlips);
            AddIf(WantsGovfor() || sensorsOnline, map.GovforBlips);
            AddIf(WantsClf() || sensorsOnline, map.ClfBlips);
            AddIf(WantsWeYu(), map.WeYuBlips);

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
                    var image = blip.Value.Image ?? CommsIcon;
                    var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                    blips.TryAdd(id, full);
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
                    var image = blip.Value.Image ?? SensorIcon;
                    var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                    blips.TryAdd(id, full);
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

                    var factionHasSensors = sensorsOnline;

                    if (WantsXenos() || factionHasSensors)
                    {
                        var image = blip.Value.Image ?? TunnelIcon;
                        var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                        blips.TryAdd(id, full);
                    }
                }
            }

            void ApplyEnemySpritesToComputer(string computerFaction)
            {
                var up = computerFaction.ToUpperInvariant();
                // Replacing an existing dictionary value does not invalidate its enumerator.
                foreach (var (id, orig) in blips)
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
                        if (up == "MARINES")
                            isFriendly = map.MarineBlips.ContainsKey(id);
                        else if (up == "OPFOR")
                            isFriendly = map.OpforBlips.ContainsKey(id);
                        else if (up == "GOVFOR")
                            isFriendly = map.GovforBlips.ContainsKey(id);
                        else if (up == "CLF")
                            isFriendly = map.ClfBlips.ContainsKey(id);
                        else if (up == "WEYU")
                            isFriendly = map.WeYuBlips.ContainsKey(id);
                    }

                    if (!isFriendly)
                    {
                        var enemy = new TacticalMapBlip(orig.Indices, EnemyIcon, orig.Color, orig.Status, orig.Background, false);
                        blips[id] = enemy;
                    }
                }
            }

            // Only apply enemy sprites on computers if that faction actually controls active sensors.
            // Without sensors we should not mark non-friendly humans as enemy on the canvas.
            if (faction != null && sensorsOnline)
                ApplyEnemySpritesToComputer(faction);

            if (!DictionaryEqual(computer.Comp.Blips, blips))
            {
                // Only publish a new feed revision when its contents change. Never recycle a
                // published dictionary: UI snapshots and other systems may still reference it.
                computer.Comp.Blips = new(blips);
                computer.Comp.BlipRevision++;
                Dirty(computer);
            }
        }
        finally
        {
            blips.Clear();
            _computerBlipBuffers.Push(blips);
        }

        var lines = EnsureComp<TacticalMapLinesComponent>(computer);
        var linesChanged = false;
        linesChanged |= PublishLines(ref lines.MarineLines, WantsMarines() ? map.MarineLines : null);
        linesChanged |= PublishLines(ref lines.XenoLines, WantsXenos() ? map.XenoLines : null);
        linesChanged |= PublishLines(ref lines.OpforLines, WantsOpfor() ? map.OpforLines : null);
        linesChanged |= PublishLines(ref lines.GovforLines, WantsGovfor() ? map.GovforLines : null);
        linesChanged |= PublishLines(ref lines.ClfLines, WantsClf() ? map.ClfLines : null);
        linesChanged |= PublishLines(ref lines.WeYuLines, WantsWeYu() ? map.WeYuLines : null);
        linesChanged |= PublishLines(ref lines.SharedLines, map.SharedLines);
        if (linesChanged) Dirty(computer, lines);

        var labels = EnsureComp<TacticalMapLabelsComponent>(computer);
        var labelsChanged = false;
        labelsChanged |= PublishLabels(ref labels.MarineLabels, WantsMarines() ? map.MarineLabels : null);
        labelsChanged |= PublishLabels(ref labels.XenoLabels, WantsXenos() ? map.XenoLabels : null);
        labelsChanged |= PublishLabels(ref labels.OpforLabels, WantsOpfor() ? map.OpforLabels : null);
        labelsChanged |= PublishLabels(ref labels.GovforLabels, WantsGovfor() ? map.GovforLabels : null);
        labelsChanged |= PublishLabels(ref labels.ClfLabels, WantsClf() ? map.ClfLabels : null);
        labelsChanged |= PublishLabels(ref labels.WeYuLabels, WantsWeYu() ? map.WeYuLabels : null);
        if (labelsChanged) Dirty(computer, labels);
    }

    private static bool DictionaryEqual<TKey, TValue>(Dictionary<TKey, TValue> left, Dictionary<TKey, TValue> right) where TKey : notnull
    {
        if (left.Count != right.Count) return false;
        foreach (var (key, value) in left)
            if (!right.TryGetValue(key, out var other) || !EqualityComparer<TValue>.Default.Equals(value, other)) return false;
        return true;
    }

    private static bool PublishLines(ref List<TacticalMapLine> target, List<TacticalMapLine>? source)
    {
        if (source == null)
        {
            if (target.Count == 0) return false;
            target = new();
        }
        else
        {
            if (!ReferenceEquals(target, source) && target.SequenceEqual(source)) return false;
            target = new(source);
        }
        return true;
    }

    private static bool PublishLabels(ref Dictionary<Vector2i, string> target, Dictionary<Vector2i, string>? source)
    {
        if (source == null)
        {
            if (target.Count == 0) return false;
            target = new();
        }
        else
        {
            if (!ReferenceEquals(target, source) && DictionaryEqual(target, source)) return false;
            target = new(source);
        }
        return true;
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
        if (map.WeYuBlips.TryGetValue(entityId, out var weyuBlip))
            return weyuBlip;
        if (map.YautjaBlips.TryGetValue(entityId, out var yautjaBlip)) // CMU14
            return yautjaBlip; // CMU14
        return null;
    }
}
