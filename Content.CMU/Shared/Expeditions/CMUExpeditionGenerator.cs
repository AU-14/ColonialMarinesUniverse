using System;
using System.Collections.Generic;

namespace Content.Shared.CMU14.Expeditions;

/// <summary>
/// Generates geography first, fits sites to it, then finds routes through it. All randomness is
/// coordinate-hashed, including continuous noise, so unrelated generation cannot change a replay.
/// </summary>
public static partial class CMUExpeditionGenerator
{
    private static readonly (int X, int Y)[] Neighbors =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)];

    public static CMUExpeditionPlan Generate(int seed, CMUExpeditionBiome biome,
        CMUExpeditionLandform landform, CMUExpeditionStory story, int size = CMUExpeditionPlan.DefaultSize)
    {
        var plan = new CMUExpeditionPlan(size, seed, biome, landform, story);
        Navigation? navigation = null;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            plan.TerrainAttempt = attempt;
            GenerateTerrain(plan);
            navigation = BuildNavigation(plan);
            plan.Sites.Clear();
            if (TryPlaceSites(plan, navigation))
                break;
        }
        if (plan.Sites.Count < 5 || navigation == null)
            throw new InvalidOperationException("This seed could not fit safe expedition sites on connected dry terrain.");
        Array.Copy(plan.Terrain, plan.BaseTerrain, plan.Terrain.Length);
        ClearSites(plan);
        ConnectSites(plan, navigation);
        // A crash may orient its recovery site toward the selected bank or rock face.
        for (var i = 0; i < plan.Sites.Count; i++)
            StampSite(plan, plan.Sites[i]);
        Decorate(plan);
        return plan;
    }

    private static void GenerateTerrain(CMUExpeditionPlan plan)
    {
        var seed = unchecked((int) Hash(plan.Seed, (int) plan.Landform, plan.TerrainAttempt, 700));
        var angle = Unit(seed, 0, 0, 1) * MathF.Tau;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var scale = plan.Size / 140f;
        var blobs = new List<(float X, float Y, float A, float B)>();
        var count = 3 + (int) (Hash(seed, 0, 0, 2) % 5);
        for (var i = 0; i < count; i++)
        {
            blobs.Add(((Unit(seed, i, 0, 3) - 0.5f) * 110,
                (Unit(seed, i, 0, 4) - 0.5f) * 110,
                13 + Unit(seed, i, 0, 5) * 24, 9 + Unit(seed, i, 0, 6) * 19));
        }
        // A mission archipelago has a large, irregular main island plus outlying islands. Distant
        // scenery must not force us to invent causeways across an ocean to reach the objective.
        if (plan.Landform == CMUExpeditionLandform.Archipelago)
        {
            blobs.Add((-21, 8, 42, 29));
            blobs.Add((23, -8, 39, 27));
        }

        var phase = Unit(seed, 0, 0, 7) * MathF.Tau;
        var riverWidth = 2 + Unit(seed, 0, 0, 8) * 5;
        var subtype = Hash(seed, 0, 0, 9) % 3;
        for (var y = 0; y < plan.Size; y++)
        for (var x = 0; x < plan.Size; x++)
        {
            var px = (x - plan.Size * 0.5f) / scale;
            var py = (y - plan.Size * 0.5f) / scale;
            var u = px * cos - py * sin;
            var v = px * sin + py * cos;
            // Bend the sampling space at two scales before drawing any geographic feature.
            var wu = u + (Noise(seed, u, v, 38, 10) - 0.5f) * 48
                       + (Noise(seed, u, v, 12, 11) - 0.5f) * 12;
            var wv = v + (Noise(seed, u, v, 41, 12) - 0.5f) * 48
                       + (Noise(seed, u, v, 13, 13) - 0.5f) * 12;
            var height = Fractal(seed, wu, wv, 39, 14);
            var moisture = Fractal(seed, wu + 83, wv - 57, 25, 20);
            var detail = (Noise(seed, u, v, 4, 24) - 0.5f) * 3;
            var river = MathF.Sin(wv / (17 + subtype * 8) + phase) * (10 + subtype * 4);
            river += (Noise(seed, 0, wv, 21, 25) - 0.5f) * 23;
            var water = MathF.Abs(wu - river) - riverWidth;
            var stone = height > 0.66f;

            switch (plan.Landform)
            {
                case CMUExpeditionLandform.RiverValley:
                    // Tributaries join at oblique angles; some seeds split around long islands.
                    if (wu < river)
                        water = MathF.Min(water, MathF.Abs(wv - wu * 0.65f - 12) - 1.7f);
                    if (subtype != 0 && wu > river)
                        water = MathF.Min(water, MathF.Abs(wv + wu * 0.85f + 25) - 2.2f);
                    if (subtype == 2)
                        water = MathF.Min(water, MathF.Abs(wu - river - 13) - riverWidth * 0.55f);
                    break;
                case CMUExpeditionLandform.LakeCountry:
                    water = BlobDistance(wu, wv, blobs);
                    break;
                case CMUExpeditionLandform.Ridgeline:
                    var ridge = MathF.Abs(MathF.Sin(wu / (12 + subtype * 5) + wv * 0.018f));
                    stone = ridge < 0.22f + height * 0.28f;
                    water = MathF.Min((height - 0.3f) * 70, MathF.Abs(wu - river - 23) - 1.8f);
                    break;
                case CMUExpeditionLandform.Wetlands:
                    // Broad marsh basins leave natural dry hummocks; small moisture noise still
                    // varies the mud and vegetation without perforating every landing footprint.
                    var basin = Noise(seed, wu + 83, wv - 57, 44, 20);
                    water = MathF.Min((basin - (0.40f + subtype * 0.025f)) * 90,
                        MathF.Abs(wu - river) - 2);
                    break;
                case CMUExpeditionLandform.Coast:
                    var coast = MathF.Sin(wv / 22 + phase) * 14 + (height - 0.5f) * 55;
                    water = wu - coast + 15;
                    break;
                case CMUExpeditionLandform.Archipelago:
                    water = -BlobDistance(wu, wv, blobs);
                    break;
                case CMUExpeditionLandform.Caldera:
                    var radius = MathF.Sqrt(wu * wu * 0.75f + wv * wv * 1.2f);
                    var rim = 33 + MathF.Sin(MathF.Atan2(wv, wu) * 3 + phase) * 7;
                    stone = MathF.Abs(radius - rim) < 3 + height * 6 && moisture > 0.35f;
                    water = radius - (15 + subtype * 5);
                    break;
                case CMUExpeditionLandform.Fjord:
                    var width = Math.Clamp((wv + 65) * 0.13f, 1, 18);
                    water = MathF.Abs(wu - river * 0.6f) - width;
                    if (wu < 0)
                        water = MathF.Min(water, MathF.Abs(wv + wu * 1.1f - 15) - 3);
                    stone = MathF.Abs(wu - river * 0.6f) < width + 6 + height * 7;
                    break;
                case CMUExpeditionLandform.Delta:
                    var spread = MathF.Max(0, wv + 20) * 0.32f;
                    water = MathF.Min(MathF.Abs(wu - river - spread), MathF.Abs(wu - river + spread)) - 2.5f;
                    water = MathF.Min(water, MathF.Abs(wu - river * 0.5f) - 2);
                    break;
                case CMUExpeditionLandform.Highlands:
                    stone = height > 0.49f;
                    water = MathF.Min((height - 0.26f) * 80, MathF.Abs(wu - river - 30) - 1.5f);
                    break;
            }

            water += detail;
            var terrain = moisture > 0.54f ? CMUExpeditionTerrain.Scrub : CMUExpeditionTerrain.Ground;
            if (moisture > (plan.Biome is CMUExpeditionBiome.Swamp or CMUExpeditionBiome.SwampJungle ? 0.48f : 0.68f) || water < 2.5f)
                terrain = CMUExpeditionTerrain.Mud;
            if (stone && water > 3)
                terrain = CMUExpeditionTerrain.Stone;
            // Regional relief groups cliffs into mountain masses with broad lowland valleys.
            // Fine height noise alone scatters impassable peaks across almost every dry clearing.
            if (stone && height > 0.59f && water > 5 && Noise(seed, wu, wv, 58, 79) > 0.52f &&
                (plan.Biome == CMUExpeditionBiome.Mountain ||
                 plan.Landform is CMUExpeditionLandform.Highlands or CMUExpeditionLandform.Ridgeline or CMUExpeditionLandform.Caldera))
                terrain = CMUExpeditionTerrain.Cliff;
            if (water >= 0 && water < (plan.Biome == CMUExpeditionBiome.Beach ? 7 : 3) &&
                (plan.Biome == CMUExpeditionBiome.Beach ||
                 plan.Landform is CMUExpeditionLandform.Coast or CMUExpeditionLandform.Archipelago or CMUExpeditionLandform.Delta))
                terrain = CMUExpeditionTerrain.Beach;
            if (water < 0)
                terrain = CMUExpeditionTerrain.Water;
            plan.Terrain[plan.Index(x, y)] = terrain;
        }
    }

    private static float BlobDistance(float x, float y, List<(float X, float Y, float A, float B)> blobs)
    {
        var result = float.MaxValue;
        foreach (var blob in blobs)
        {
            var dx = (x - blob.X) / blob.A;
            var dy = (y - blob.Y) / blob.B;
            result = MathF.Min(result, (MathF.Sqrt(dx * dx + dy * dy) - 1) * MathF.Min(blob.A, blob.B));
        }
        return result;
    }

    private static bool Fits(CMUExpeditionPlan plan, CMUExpeditionPoint p)
    {
        foreach (var site in plan.Sites)
        {
            var clearance = site.Kind == CMUExpeditionSiteKind.LandingZone
                ? CMUExpeditionPlan.LandingRadius + 14 : 23;
            if (Math.Abs(p.X - site.Center.X) < clearance && Math.Abs(p.Y - site.Center.Y) < clearance)
                return false;
        }
        return true;
    }

    private static void AddSite(CMUExpeditionPlan plan, CMUExpeditionSiteKind kind, CMUExpeditionPoint p)
    {
        var variant = (int) (Hash(plan.Seed, p.X, p.Y, 42) % 4);
        var rotation = (int) (Hash(plan.Seed, p.X, p.Y, 43) % 4);
        plan.Sites.Add(new(kind, p, variant, rotation));
    }

    private static void ClearSites(CMUExpeditionPlan plan)
    {
        foreach (var site in plan.Sites)
        {
            var c = site.Center;
            var lz = site.Kind == CMUExpeditionSiteKind.LandingZone;
            var extent = lz ? CMUExpeditionPlan.LandingRadius + 3 : 11;
            for (var y = Math.Max(2, c.Y - extent); y <= Math.Min(plan.Size - 3, c.Y + extent); y++)
            for (var x = Math.Max(2, c.X - extent); x <= Math.Min(plan.Size - 3, c.X + extent); x++)
            {
                var dx = x - c.X;
                var dy = y - c.Y;
                var radius = (lz ? CMUExpeditionPlan.LandingRadius : 2) + Noise(plan.Seed, x, y, 9, 45) * 2;
                var inside = dx * dx + dy * dy < radius * radius;
                if (site.Kind == CMUExpeditionSiteKind.Recovery)
                {
                    var along = site.Rotation % 2 == 0 ? dx : dy;
                    var across = site.Rotation % 2 == 0 ? dy : dx;
                    inside |= along * along / 121f + across * across / 25f < 1;
                }
                if (lz && Math.Abs(dx) <= CMUExpeditionPlan.LandingRadius && Math.Abs(dy) <= CMUExpeditionPlan.LandingRadius)
                    inside = true;
                if (!inside)
                    continue;
                var index = plan.Index(x, y);
                if (plan.Terrain[index] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff)
                    continue;
                plan.Reserved[index] = true;
            }
        }
    }

    private static void ConnectSites(CMUExpeditionPlan plan, Navigation nav)
    {
        var joined = new bool[plan.Sites.Count];
        joined[0] = true;
        // A weighted spanning tree produces branches and cul-de-sacs. Optional edges add loops;
        // the recovery site always has at least two graph approaches.
        for (var n = 1; n < joined.Length; n++)
        {
            var best = float.MaxValue;
            var from = 0;
            var to = 0;
            for (var a = 0; a < joined.Length; a++)
            for (var b = 0; b < joined.Length; b++)
            {
                if (!joined[a] || joined[b])
                    continue;
                var cost = Distance(plan.Sites[a].Center, plan.Sites[b].Center)
                           * (0.65f + Unit(plan.Seed, a, b, 48) * 0.9f);
                if (cost >= best)
                    continue;
                best = cost;
                from = a;
                to = b;
            }
            joined[to] = true;
            plan.Routes.Add(new(from, to));
        }

        var degree = 0;
        foreach (var route in plan.Routes)
            if (route.From == 1 || route.To == 1)
                degree++;
        var extras = (int) (Hash(plan.Seed, 0, 0, 49) % 3);
        if (degree < 2)
            AddShortcut(plan, true);
        for (var i = 0; i < extras; i++)
            AddShortcut(plan, false);

        var costs = new float[plan.Terrain.Length];
        for (var i = 0; i < costs.Length; i++)
        {
            costs[i] = plan.Terrain[i] switch
            {
                CMUExpeditionTerrain.Water => 13,
                CMUExpeditionTerrain.Stone => 5,
                CMUExpeditionTerrain.Mud => 2.5f,
                _ => 1.2f,
            };
            costs[i] += Noise(plan.Seed, i % plan.Size, i / plan.Size, 12, 50) * 4;
        }
        foreach (var route in plan.Routes)
            CarveRoute(plan, route, costs, nav);
    }

    private static void AddShortcut(CMUExpeditionPlan plan, bool objective)
    {
        var best = float.MaxValue;
        CMUExpeditionRoute? chosen = null;
        for (var a = 0; a < plan.Sites.Count; a++)
        for (var b = a + 1; b < plan.Sites.Count; b++)
        {
            if (objective && a != 1 && b != 1 || Connected(plan, a, b))
                continue;
            var score = Distance(plan.Sites[a].Center, plan.Sites[b].Center)
                        * (0.8f + Unit(plan.Seed, a, b, 51) * 0.9f);
            if (score >= best)
                continue;
            best = score;
            chosen = new(a, b);
        }
        if (chosen is { } route)
            plan.Routes.Add(route);
    }

    private static bool Connected(CMUExpeditionPlan plan, int a, int b)
    {
        foreach (var route in plan.Routes)
            if (route.From == a && route.To == b || route.From == b && route.To == a)
                return true;
        return false;
    }

    private static void CarveRoute(CMUExpeditionPlan plan, CMUExpeditionRoute route, float[] costs, Navigation nav)
    {
        var start = plan.Sites[route.From].Center;
        var goal = plan.Sites[route.To].Center;
        var startIndex = plan.Index(start.X, start.Y);
        var goalIndex = plan.Index(goal.X, goal.Y);
        var scores = new float[costs.Length];
        Array.Fill(scores, float.MaxValue);
        var parents = new int[costs.Length];
        Array.Fill(parents, -1);
        var open = new SortedSet<(float Score, int Index)>();
        scores[startIndex] = 0;
        open.Add((0, startIndex));
        while (open.Count > 0)
        {
            var current = open.Min;
            open.Remove(current);
            var i = current.Index;
            if (i == goalIndex)
                break;
            var x = i % plan.Size;
            var y = i / plan.Size;
            foreach (var (dx, dy) in Neighbors)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 3 || ny < 3 || nx >= plan.Size - 3 || ny >= plan.Size - 3)
                    continue;
                var next = plan.Index(nx, ny);
                if (!nav.Walkable[next])
                    continue;
                var cost = scores[i] + costs[next] * (dx != 0 && dy != 0 ? 1.414214f : 1);
                if (plan.Paths[next])
                    cost += 2;
                if (cost >= scores[next])
                    continue;
                var heuristic = Distance(new(nx, ny), goal);
                open.Remove((scores[next] + heuristic, next));
                scores[next] = cost;
                parents[next] = i;
                open.Add((cost + heuristic, next));
            }
            if (!nav.Crossings.TryGetValue(i, out var crossings))
                continue;
            foreach (var next in crossings)
            {
                var end = new CMUExpeditionPoint(next % plan.Size, next / plan.Size);
                var distance = Distance(new(x, y), end);
                var cost = scores[i] + distance * (plan.Paths[next] ? 2 : 6);
                if (cost >= scores[next])
                    continue;
                var heuristic = Distance(end, goal);
                open.Remove((scores[next] + heuristic, next));
                scores[next] = cost;
                parents[next] = i;
                open.Add((cost + heuristic, next));
            }
        }

        if (parents[goalIndex] == -1)
            throw new InvalidOperationException("Expedition route has no supported crossing.");

        for (var i = goalIndex; i != -1; i = parents[i])
        {
            var x = i % plan.Size;
            var y = i / plan.Size;
            if (parents[i] is var parent && parent >= 0 &&
                (Math.Abs(x - parent % plan.Size) > 1 || Math.Abs(y - parent / plan.Size) > 1))
                StampBridge(plan, new(x, y), new(parent % plan.Size, parent / plan.Size));
            const int width = 1;
            for (var oy = -width; oy <= width; oy++)
            for (var ox = -width; ox <= width; ox++)
            {
                var tile = plan.Index(x + ox, y + oy);
                if (plan.BaseTerrain[tile] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff)
                    continue;
                plan.Paths[tile] = true;
                plan.Reserved[tile] = true;
                // Preserve the traversable corridor but only wear a broken, narrow game trail.
                if (ox == 0 && oy == 0 &&
                    plan.Terrain[tile] is not (CMUExpeditionTerrain.Deck or CMUExpeditionTerrain.Beach) &&
                    Noise(plan.Seed, x, y, 11, 53) > 0.52f)
                    plan.Terrain[tile] = CMUExpeditionTerrain.Trail;
            }
        }
    }

    private static void StampSite(CMUExpeditionPlan plan, CMUExpeditionSite site)
    {
        if (site.Kind == CMUExpeditionSiteKind.LandingZone)
            return;
        if (site.Kind >= CMUExpeditionSiteKind.Grove)
        {
            StampNaturalLandmark(plan, site);
            return;
        }
        var kind = site.Kind;
        if (kind == CMUExpeditionSiteKind.Recovery && plan.Story == CMUExpeditionStory.CrashRecovery)
        {
            StampCrash(plan, site);
            return;
        }
        if (kind == CMUExpeditionSiteKind.Recovery)
        {
            kind = plan.Story switch
            {
                CMUExpeditionStory.SurveyCamp => CMUExpeditionSiteKind.Camp,
                CMUExpeditionStory.LostRelay => CMUExpeditionSiteKind.Relay,
                _ => CMUExpeditionSiteKind.Wreck,
            };
        }

        for (var y = -11; y <= 11; y++)
        for (var x = -11; x <= 11; x++)
        {
            var floor = false;
            var wall = false;
            var length = 6 + site.Variant;
            switch (kind)
            {
                case CMUExpeditionSiteKind.Wreck:
                    var halfWidth = x > length - 3 ? 1 : 2 + site.Variant % 2;
                    floor = Math.Abs(x) <= length && Math.Abs(y) <= halfWidth &&
                            (Math.Abs(x) < length - 2 || Unit(plan.Seed, x + site.Center.X, y + site.Center.Y, 61) > 0.4f);
                    wall = floor && (Math.Abs(y) == halfWidth || x == -length) &&
                           Unit(plan.Seed, x + site.Center.X, y + site.Center.Y, 60) > 0.35f;
                    if (x < -length && Math.Abs(y - 1) < 3)
                        SetSiteTile(plan, site, x, y, CMUExpeditionTerrain.Mud, false);
                    break;
                case CMUExpeditionSiteKind.Camp:
                    floor = x is >= -3 and <= 3 && y is >= -2 and <= 2;
                    break;
                case CMUExpeditionSiteKind.Relay:
                    floor = Math.Abs(x) <= 2 && Math.Abs(y) <= 2;
                    break;
                case CMUExpeditionSiteKind.Cache:
                    floor = Math.Abs(x + 2) + Math.Abs(y - 1) < 3 + site.Variant;
                    break;
            }
            if (floor)
                SetSiteTile(plan, site, x, y, CMUExpeditionTerrain.Structure, wall);
        }
        PlaceSiteProp(plan, site, -4, -2, CMUExpeditionProp.Supply);
        PlaceSiteProp(plan, site, 3, 3, kind == CMUExpeditionSiteKind.Relay
            ? CMUExpeditionProp.Relay : CMUExpeditionProp.Supply);
        if (site.Kind == CMUExpeditionSiteKind.Recovery)
            plan.Props[plan.Index(site.Center.X, site.Center.Y)] = CMUExpeditionProp.Recovery;
    }

    private static CMUExpeditionPoint SitePoint(CMUExpeditionSite site, int x, int y) => site.Rotation switch
    {
        0 => new(site.Center.X + x, site.Center.Y + y),
        1 => new(site.Center.X - y, site.Center.Y + x),
        2 => new(site.Center.X - x, site.Center.Y - y),
        _ => new(site.Center.X + y, site.Center.Y - x),
    };

    private static void SetSiteTile(CMUExpeditionPlan plan, CMUExpeditionSite site, int x, int y,
        CMUExpeditionTerrain terrain, bool wall)
    {
        var p = SitePoint(site, x, y);
        var index = plan.Index(p.X, p.Y);
        if (plan.BaseTerrain[index] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff ||
            plan.Terrain[index] == CMUExpeditionTerrain.Deck)
            return;
        plan.Terrain[index] = terrain;
        plan.Reserved[index] = true;
        if (wall && !plan.Paths[index] && Math.Abs(x) > 1 && Math.Abs(y) > 1)
            plan.Props[index] = CMUExpeditionProp.Hull;
    }

    private static void PlaceSiteProp(CMUExpeditionPlan plan, CMUExpeditionSite site, int x, int y,
        CMUExpeditionProp prop)
    {
        var p = SitePoint(site, x, y);
        var index = plan.Index(p.X, p.Y);
        if (plan.Paths[index] || plan.BaseTerrain[index] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff)
            return;
        plan.Props[index] = prop;
        plan.Reserved[index] = true;
    }

    private static void Decorate(CMUExpeditionPlan plan)
    {
        // Solid obstacles remain separate from the walk-through forest floor.
        for (var y = 1; y < plan.Size - 1; y++)
        for (var x = 1; x < plan.Size - 1; x++)
        {
            var i = plan.Index(x, y);
            if (plan.Terrain[i] == CMUExpeditionTerrain.Cliff)
            {
                plan.Props[i] = CMUExpeditionProp.Rock;
                continue;
            }
            if (plan.Reserved[i] || plan.Terrain[i] == CMUExpeditionTerrain.Water)
                continue;
            var clear = false;
            for (var oy = -1; oy <= 1; oy++)
            for (var ox = -1; ox <= 1; ox++)
                clear |= plan.Props[plan.Index(x + ox, y + oy)] != CMUExpeditionProp.None;
            if (clear)
                continue;
            var grove = Fractal(plan.Seed, x, y, 24, 66);
            var density = plan.Biome switch
            {
                CMUExpeditionBiome.Woodland => Math.Clamp((grove - 0.2f) * 2.3f, 0.08f, 0.85f),
                CMUExpeditionBiome.Swamp => Math.Clamp((grove - 0.28f) * 1.8f, 0.04f, 0.75f),
                CMUExpeditionBiome.SwampJungle => Math.Clamp((grove - 0.22f) * 2.2f, 0.08f, 0.85f),
                CMUExpeditionBiome.BurnedWoodland => Math.Clamp((grove - 0.38f) * 0.9f, 0.01f, 0.35f),
                CMUExpeditionBiome.Beach => Math.Clamp((grove - 0.42f) * 0.8f, 0.01f, 0.2f),
                _ => Math.Clamp((grove - 0.52f) * 0.65f, 0.004f, 0.16f),
            };
            if (plan.Terrain[i] == CMUExpeditionTerrain.Beach)
                density *= 0.1f;
            var roll = Unit(plan.Seed, x, y, 72);
            if (plan.Terrain[i] == CMUExpeditionTerrain.Stone && roll < 0.5f)
                plan.Props[i] = CMUExpeditionProp.Boulder;
            else if (roll < density)
                plan.Props[i] = plan.Scorched[i] ? CMUExpeditionProp.CharredTree : CMUExpeditionProp.Tree;
            else if (roll > 0.99f)
                plan.Props[i] = CMUExpeditionProp.Boulder;
        }
        for (var i = 0; i < plan.Size; i++)
        {
            SetBoundary(plan, plan.Index(i, 0));
            SetBoundary(plan, plan.Index(i, plan.Size - 1));
            SetBoundary(plan, plan.Index(0, i));
            SetBoundary(plan, plan.Index(plan.Size - 1, i));
        }
        StampWildernessFeatures(plan);
        DressForestFloor(plan);
        MeasureWaterDepth(plan);
    }

    private static void SetBoundary(CMUExpeditionPlan plan, int index)
    {
        plan.Props[index] = plan.Terrain[index] == CMUExpeditionTerrain.Cliff
            ? CMUExpeditionProp.Rock : CMUExpeditionProp.Boundary;
    }

    private static float Distance(CMUExpeditionPoint a, CMUExpeditionPoint b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        return MathF.Sqrt(x * x + y * y);
    }

    private static float Fractal(int seed, float x, float y, float scale, int salt) =>
        Math.Clamp(0.5f + (Noise(seed, x, y, scale, salt) - 0.5f) * 1.15f +
                         (Noise(seed, x, y, scale * 0.43f, salt + 1) - 0.5f) * 0.55f +
                         (Noise(seed, x, y, scale * 0.19f, salt + 2) - 0.5f) * 0.25f, 0, 1);

    private static float Noise(int seed, float x, float y, float scale, int salt)
    {
        x = x / scale + salt * 3.17f;
        y = y / scale - salt * 2.73f;
        var gx = (int) MathF.Floor(x);
        var gy = (int) MathF.Floor(y);
        var tx = x - gx;
        var ty = y - gy;
        var sx = tx * tx * tx * (tx * (tx * 6 - 15) + 10);
        var sy = ty * ty * ty * (ty * (ty * 6 - 15) + 10);
        var a = Gradient(Hash(seed, gx, gy, salt), tx, ty);
        var b = Gradient(Hash(seed, gx + 1, gy, salt), tx - 1, ty);
        var c = Gradient(Hash(seed, gx, gy + 1, salt), tx, ty - 1);
        var d = Gradient(Hash(seed, gx + 1, gy + 1, salt), tx - 1, ty - 1);
        return Math.Clamp(0.5f + ((a + (b - a) * sx) * (1 - sy) + (c + (d - c) * sx) * sy) * 0.6f, 0, 1);
    }

    private static float Gradient(uint hash, float x, float y) => (hash & 7) switch
    {
        0 => x + y, 1 => x - y, 2 => -x + y, 3 => -x - y, 4 => x, 5 => -x, 6 => y, _ => -y,
    };

    public static float Unit(int seed, int x, int y, int salt) => (Hash(seed, x, y, salt) & 0xFFFFFF) / 16777216f;

    public static uint Hash(int seed, int x, int y, int salt)
    {
        unchecked
        {
            var value = (uint) seed ^ (uint) x * 0x9E3779B9u ^ (uint) y * 0x85EBCA6Bu ^ (uint) salt * 0xC2B2AE35u;
            value = (value ^ (value >> 16)) * 0x7FEB352Du;
            value = (value ^ (value >> 15)) * 0x846CA68Bu;
            return value ^ (value >> 16);
        }
    }
}
