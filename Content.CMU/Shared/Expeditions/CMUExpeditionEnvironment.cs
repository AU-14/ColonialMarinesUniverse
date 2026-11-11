using System;

namespace Content.Shared.CMU14.Expeditions;

public static partial class CMUExpeditionGenerator
{
    private static void StampWildernessFeatures(CMUExpeditionPlan plan)
    {
        var cold = plan.Biome is CMUExpeditionBiome.Tundra or CMUExpeditionBiome.Mountain;
        var wet = plan.Biome is CMUExpeditionBiome.Swamp or CMUExpeditionBiome.SwampJungle;
        var targetCount = 7 + (int) (Hash(plan.Seed, 0, 0, 301) % 4);
        for (var attempt = 0; attempt < 500 && plan.Features.Count < targetCount; attempt++)
        {
            var x = 9 + (int) (Hash(plan.Seed, attempt, 0, 302) % (uint) (plan.Size - 18));
            var y = 9 + (int) (Hash(plan.Seed, attempt, 0, 303) % (uint) (plan.Size - 18));
            var center = new CMUExpeditionPoint(x, y);
            if (Distance(center, plan.LandingZone) < 30 || Distance(center, plan.Objective) < 12)
                continue;
            var clear = true;
            foreach (var feature in plan.Features)
                clear &= Distance(center, feature.Center) >= 18;
            for (var dy = -2; dy <= 2 && clear; dy++)
            for (var dx = -2; dx <= 2 && clear; dx++)
                clear &= CanDressFeature(plan, x + dx, y + dy);
            if (!clear)
                continue;

            var kind = (CMUExpeditionFeatureKind) (plan.Features.Count % 6);
            if (kind == CMUExpeditionFeatureKind.BurnScar && (wet || cold))
                kind = cold ? CMUExpeditionFeatureKind.Rockfall : CMUExpeditionFeatureKind.BogRemains;
            if (kind == CMUExpeditionFeatureKind.BogRemains && !wet)
                kind = CMUExpeditionFeatureKind.Windthrow;
            if (plan.Biome == CMUExpeditionBiome.BurnedWoodland && plan.Features.Count % 2 == 0)
                kind = CMUExpeditionFeatureKind.BurnScar;
            plan.Features.Add(new(kind, center));

            // Irregular, elongated patches. Existing access corridors, banks and set pieces are immutable.
            var turn = Hash(plan.Seed, x, y, 304) % 2 == 0;
            for (var dy = -7; dy <= 7; dy++)
            for (var dx = -7; dx <= 7; dx++)
            {
                var px = x + dx;
                var py = y + dy;
                var distance = turn ? dx * dx / 49f + dy * dy / 20f : dx * dx / 20f + dy * dy / 49f;
                if (distance > 0.8f + Noise(plan.Seed, px, py, 4, 305) * 0.6f || !CanDressFeature(plan, px, py))
                    continue;
                var i = plan.Index(px, py);
                var roll = Unit(plan.Seed, px, py, 306);
                switch (kind)
                {
                    case CMUExpeditionFeatureKind.BurnScar:
                        plan.Terrain[i] = CMUExpeditionTerrain.Mud;
                        if (plan.Props[i] == CMUExpeditionProp.Tree)
                            plan.Props[i] = CMUExpeditionProp.CharredTree;
                        plan.Details[i] = roll < 0.5f ? CMUExpeditionDetail.Ash : CMUExpeditionDetail.Deadwood;
                        break;
                    case CMUExpeditionFeatureKind.Windthrow:
                        if (plan.Props[i] == CMUExpeditionProp.Tree)
                            plan.Props[i] = CMUExpeditionProp.FallenLog;
                        plan.Details[i] = roll < 0.5f ? CMUExpeditionDetail.LeafLitter : CMUExpeditionDetail.Deadwood;
                        break;
                    case CMUExpeditionFeatureKind.Rockfall:
                        plan.Details[i] = CMUExpeditionDetail.Pebbles;
                        if (plan.Props[i] == CMUExpeditionProp.Tree)
                            plan.Props[i] = CMUExpeditionProp.Boulder;
                        break;
                    case CMUExpeditionFeatureKind.BogRemains:
                        plan.Details[i] = roll < 0.4f ? CMUExpeditionDetail.Fungus : CMUExpeditionDetail.Moss;
                        if (plan.Props[i] == CMUExpeditionProp.Tree && roll < 0.5f)
                            plan.Props[i] = CMUExpeditionProp.FallenLog;
                        break;
                    case CMUExpeditionFeatureKind.AbandonedCamp:
                        if (distance < 0.3f)
                        {
                            plan.Props[i] = CMUExpeditionProp.None;
                            plan.Details[i] = roll < 0.6f ? CMUExpeditionDetail.Ash : CMUExpeditionDetail.Litter;
                        }
                        break;
                    case CMUExpeditionFeatureKind.CargoSpill:
                        if (roll < 0.08f)
                            plan.Props[i] = CMUExpeditionProp.CargoDebris;
                        break;
                }
            }
            var centerIndex = plan.Index(x, y);
            plan.Props[centerIndex] = kind switch
            {
                CMUExpeditionFeatureKind.AbandonedCamp => CMUExpeditionProp.Campfire,
                CMUExpeditionFeatureKind.CargoSpill => CMUExpeditionProp.DiscardedPack,
                CMUExpeditionFeatureKind.BurnScar => CMUExpeditionProp.CharredTree,
                CMUExpeditionFeatureKind.Rockfall => CMUExpeditionProp.Boulder,
                _ => CMUExpeditionProp.FallenLog,
            };
            if (kind == CMUExpeditionFeatureKind.AbandonedCamp)
            {
                plan.Props[plan.Index(x + 2, y)] = CMUExpeditionProp.BurntFrame;
                plan.Props[plan.Index(x - 2, y + 1)] = CMUExpeditionProp.DiscardedPack;
            }
            if (kind == CMUExpeditionFeatureKind.BurnScar)
            {
                // A small extinguishable hot spot inside each scar, away from routes and live vegetation.
                for (var fy = -2; fy <= 2; fy++)
                for (var fx = -2; fx <= 2; fx++)
                {
                    var i = plan.Index(x + fx, y + fy);
                    plan.Props[i] = CMUExpeditionProp.None;
                    plan.Details[i] = CMUExpeditionDetail.Ash;
                }
                plan.FirePockets.Add(center);
                plan.FirePockets.Add(new(x + 1, y));
            }
        }
    }

    private static bool CanDressFeature(CMUExpeditionPlan plan, int x, int y)
    {
        var i = plan.Index(x, y);
        return !plan.Reserved[i] && plan.Terrain[i] is CMUExpeditionTerrain.Ground or
            CMUExpeditionTerrain.Scrub or CMUExpeditionTerrain.Mud or CMUExpeditionTerrain.Stone or CMUExpeditionTerrain.Beach;
    }

    private static void StampNaturalLandmark(CMUExpeditionPlan plan, CMUExpeditionSite site)
    {
        for (var y = -7; y <= 7; y++)
        for (var x = -7; x <= 7; x++)
        {
            var point = SitePoint(site, x, y);
            var i = plan.Index(point.X, point.Y);
            var distance = x * x + y * y;
            if (distance < 9 || distance > 42 || plan.Reserved[i] ||
                plan.Terrain[i] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff or CMUExpeditionTerrain.Deck)
                continue;
            var roll = Unit(plan.Seed, point.X, point.Y, 81);
            switch (site.Kind)
            {
                case CMUExpeditionSiteKind.Outcrop:
                    if (roll < 0.13f)
                        plan.Props[i] = CMUExpeditionProp.Boulder;
                    else if (roll < 0.7f)
                        plan.Details[i] = CMUExpeditionDetail.Pebbles;
                    break;
                case CMUExpeditionSiteKind.Deadfall:
                    if (Math.Abs(y + x / 3) < 2 && roll < 0.65f)
                        plan.Details[i] = CMUExpeditionDetail.Deadwood;
                    else if (roll < 0.3f)
                        plan.Details[i] = CMUExpeditionDetail.Litter;
                    break;
                case CMUExpeditionSiteKind.Grove:
                    if (roll < 0.55f)
                        plan.Details[i] = CMUExpeditionDetail.Fern;
                    break;
                case CMUExpeditionSiteKind.Hollow:
                    if (roll < 0.65f)
                        plan.Details[i] = CMUExpeditionDetail.Bush;
                    break;
            }
        }
    }

    private static void DressForestFloor(CMUExpeditionPlan plan)
    {
        var forest = plan.Biome is CMUExpeditionBiome.Woodland or CMUExpeditionBiome.Swamp or CMUExpeditionBiome.SwampJungle;
        var cold = plan.Biome is CMUExpeditionBiome.Tundra or CMUExpeditionBiome.Mountain;
        for (var y = 1; y < plan.Size - 1; y++)
        for (var x = 1; x < plan.Size - 1; x++)
        {
            var i = plan.Index(x, y);
            if (plan.Terrain[i] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff or
                CMUExpeditionTerrain.Deck or CMUExpeditionTerrain.Structure ||
                plan.Props[i] is not (CMUExpeditionProp.None or CMUExpeditionProp.Tree))
            {
                plan.Details[i] = CMUExpeditionDetail.None;
                continue;
            }
            if (plan.Details[i] != CMUExpeditionDetail.None)
                continue;

            if (plan.Scorched[i])
            {
                plan.Details[i] = plan.Paths[i] || Unit(plan.Seed, x, y, 310) < 0.62f
                    ? CMUExpeditionDetail.Ash : CMUExpeditionDetail.Deadwood;
                continue;
            }

            var patch = Fractal(plan.Seed, x, y, 17, 82);
            var roll = Unit(plan.Seed, x, y, 85);
            var density = forest ? 0.24f + patch * 0.52f : 0.12f + patch * 0.32f;
            if (plan.Paths[i])
                density *= 0.55f;
            if (roll > density)
                continue;

            // Keep the actual aircraft footprint and footpaths low; their edges can still be lush.
            var low = plan.Paths[i] || Math.Abs(x - plan.LandingZone.X) <= CMUExpeditionPlan.LandingRadius &&
                Math.Abs(y - plan.LandingZone.Y) <= CMUExpeditionPlan.LandingRadius;
            var nearWater = false;
            for (var dy = -2; dy <= 2 && !nearWater; dy++)
            for (var dx = -2; dx <= 2; dx++)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < plan.Size && ny < plan.Size &&
                    plan.Terrain[plan.Index(nx, ny)] == CMUExpeditionTerrain.Water)
                    nearWater = true;
            }

            var kind = Unit(plan.Seed, x, y, 86);
            var detail = CMUExpeditionDetail.Grass;
            if (plan.Terrain[i] == CMUExpeditionTerrain.Beach && nearWater && !low && kind < 0.22f)
                detail = CMUExpeditionDetail.Deadwood;
            else if (plan.Terrain[i] is CMUExpeditionTerrain.Stone or CMUExpeditionTerrain.Beach || cold)
                detail = kind < 0.7f ? CMUExpeditionDetail.Pebbles : CMUExpeditionDetail.Litter;
            else if (plan.Biome == CMUExpeditionBiome.BurnedWoodland)
                detail = kind < 0.45f ? CMUExpeditionDetail.Ash : kind < 0.7f
                    ? CMUExpeditionDetail.Deadwood : CMUExpeditionDetail.DryGrass;
            else if (!low)
            {
                if (!nearWater && patch > 0.54f && Noise(plan.Seed, x, y, 12, 87) > 0.53f && kind < 0.45f)
                    detail = CMUExpeditionDetail.Flowers;
                else if (nearWater && kind < 0.7f)
                    detail = CMUExpeditionDetail.Reeds;
                else if (kind < 0.06f)
                    detail = CMUExpeditionDetail.Deadwood;
                else if (kind < 0.2f)
                    detail = CMUExpeditionDetail.Litter;
                else if (patch > 0.5f && kind < 0.6f)
                    detail = CMUExpeditionDetail.Bush;
                else if (kind < 0.7f)
                    detail = CMUExpeditionDetail.Fern;
            }
            // Local floor communities break up the repeated grass/branch carpet without adding colliders.
            if (forest && !nearWater && !low && detail is CMUExpeditionDetail.Grass or CMUExpeditionDetail.Litter)
            {
                var community = Noise(plan.Seed, x, y, 21, 309);
                detail = community < 0.37f ? CMUExpeditionDetail.DryGrass : community < 0.5f
                    ? CMUExpeditionDetail.LeafLitter : community > 0.64f ? CMUExpeditionDetail.Moss : detail;
                if (!low && community > 0.6f && kind < 0.16f)
                    detail = CMUExpeditionDetail.Fungus;
            }
            plan.Details[i] = detail;
        }
    }

    private static void MeasureWaterDepth(CMUExpeditionPlan plan)
    {
        for (var y = 0; y < plan.Size; y++)
        for (var x = 0; x < plan.Size; x++)
        {
            var i = plan.Index(x, y);
            if (plan.Terrain[i] != CMUExpeditionTerrain.Water)
                continue;
            byte depth = 3;
            for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= plan.Size || ny >= plan.Size ||
                    plan.BaseTerrain[plan.Index(nx, ny)] == CMUExpeditionTerrain.Water)
                    continue;
                depth = Math.Min(depth, (byte) Math.Max(Math.Abs(dx), Math.Abs(dy)));
            }
            plan.WaterDepth[i] = depth;
        }
    }
}
