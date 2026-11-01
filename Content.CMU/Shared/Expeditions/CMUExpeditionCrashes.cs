using System;

namespace Content.Shared.CMU14.Expeditions;

public static partial class CMUExpeditionGenerator
{
    private static CMUExpeditionPoint? FindImpactTerrain(CMUExpeditionPlan plan, CMUExpeditionPoint center,
        CMUExpeditionTerrain terrain)
    {
        for (var distance = 7; distance <= 12; distance++)
        foreach (var (dx, dy) in new[] { (0, 1), (-1, 0), (0, -1), (1, 0) })
        {
            var point = new CMUExpeditionPoint(center.X + dx * distance, center.Y + dy * distance);
            if (point.X > 1 && point.Y > 1 && point.X < plan.Size - 2 && point.Y < plan.Size - 2 &&
                plan.Terrain[plan.Index(point.X, point.Y)] == terrain)
                return point;
        }
        return null;
    }

    private static void StampCrash(CMUExpeditionPlan plan, CMUExpeditionSite site)
    {
        var gunship = Hash(plan.Seed, 0, 0, 320) % 2 == 0;
        var variant = (int) (Hash(plan.Seed, 0, 0, 321) % 3);
        plan.WreckName = (gunship, variant) switch
        {
            (false, _) => "cmu-expedition-wreck-mule",
            (true, 0) => "cmu-expedition-wreck-kestrel",
            (true, 1) => "cmu-expedition-wreck-escort",
            _ => "cmu-expedition-wreck-pathfinder",
        };
        var shore = plan.Biome == CMUExpeditionBiome.Beach
            ? FindImpactTerrain(plan, site.Center, CMUExpeditionTerrain.Water) : null;
        plan.CrashImpact = shore != null ? CMUExpeditionCrashImpact.ShoreBreak :
            plan.Biome == CMUExpeditionBiome.BurnedWoodland ? CMUExpeditionCrashImpact.Burnout :
            variant == 1 ? CMUExpeditionCrashImpact.Breakup : CMUExpeditionCrashImpact.ForestSkid;
        var impact = shore;
        if (FindImpactTerrain(plan, site.Center, CMUExpeditionTerrain.Cliff) is { } cliff &&
            (plan.Biome == CMUExpeditionBiome.Mountain || Hash(plan.Seed, 0, 0, 326) % 4 == 0))
        {
            plan.CrashImpact = CMUExpeditionCrashImpact.CliffStrike;
            impact = cliff;
        }
        if (impact is { } hit)
        {
            var turn = hit.X > site.Center.X ? 3 : hit.X < site.Center.X ? 1 : hit.Y > site.Center.Y ? 0 : 2;
            site = site with { Rotation = turn };
            plan.Sites[1] = site;
        }

        // A whole crash footprint, including the slide and blast zone, rather than a dirt outline at the hull.
        var width = plan.CrashImpact == CMUExpeditionCrashImpact.Burnout ? 330f :
            plan.CrashImpact == CMUExpeditionCrashImpact.ForestSkid ? 135f : 190f;
        var length = plan.CrashImpact == CMUExpeditionCrashImpact.Burnout ? 340f : 480f;
        for (var y = -28; y <= 21; y++)
        for (var x = -22; x <= 22; x++)
        {
            var p = SitePoint(site, x, y);
            if (!InsideCrashBounds(plan, p))
                continue;
            var i = plan.Index(p.X, p.Y);
            if (plan.BaseTerrain[i] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff ||
                plan.Terrain[i] == CMUExpeditionTerrain.Deck)
                continue;
            var ellipse = x * x / width + (y + 3) * (y + 3) / length;
            if (ellipse > 0.7f + Noise(plan.Seed, p.X, p.Y, 5, 322) * 0.85f)
                continue;
            plan.Scorched[i] = true;
            plan.Terrain[i] = CMUExpeditionTerrain.Mud;
            if (Math.Abs(x) < 3 && y < 2)
                plan.Reserved[i] = true; // Stripped trees along the skid; keep it walkable.
            if (!plan.Reserved[i] && Unit(plan.Seed, p.X, p.Y, 323) < 0.025f)
                plan.Props[i] = plan.CrashImpact == CMUExpeditionCrashImpact.CliffStrike && y > 3
                    ? CMUExpeditionProp.Boulder : CMUExpeditionProp.CargoDebris;
        }

        var floors = gunship ? CMUExpeditionWreckBlueprints.GunshipFloor : CMUExpeditionMuleBlueprint.Floors;
        var hull = gunship ? CMUExpeditionWreckBlueprints.GunshipHull : CMUExpeditionMuleBlueprint.Hulls;
        if (!gunship)
        {
            // Clear room to walk around the airframe, including connections to the existing approach trails.
            foreach (var piece in hull)
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var center = WreckPoint(piece.X, piece.Y);
                var p = new CMUExpeditionPoint(center.X + dx, center.Y + dy);
                if (!CanPlace(p))
                    continue;
                var i = plan.Index(p.X, p.Y);
                plan.Props[i] = CMUExpeditionProp.None;
                plan.Reserved[i] = true;
            }
        }
        foreach (var tile in floors)
        {
            var p = WreckPoint(tile.X, tile.Y);
            if (!CanPlace(p))
                continue;
            var i = plan.Index(p.X, p.Y);
            if (gunship && Unit(plan.Seed, p.X, p.Y, 324) < 0.08f && Math.Abs(tile.X) > 1)
                continue;
            plan.Terrain[i] = CMUExpeditionTerrain.Structure;
            plan.Reserved[i] = true;
            plan.Props[i] = CMUExpeditionProp.None;
            plan.Details[i] = CMUExpeditionDetail.None;
            var rotation = (byte) ((tile.Rotation & 4) | ((tile.Rotation + site.Rotation) & 3));
            var damaged = !gunship && (tile.X >= 2 && tile.Y is >= -6 and <= -3 ||
                plan.CrashImpact == CMUExpeditionCrashImpact.CliffStrike && tile.Y >= 7);
            plan.WreckFloors[i] = new(damaged ? "RMCFloorPlatingScorched" : tile.Prototype, rotation);
        }
        foreach (var piece in hull)
        {
            var p = WreckPoint(piece.X, piece.Y);
            if (!CanPlace(p))
                continue;
            var i = plan.Index(p.X, p.Y);
            if (Math.Abs(piece.X) <= 1 && Math.Abs(piece.Y) <= 1 ||
                gunship && (plan.Paths[i] || Unit(plan.Seed, p.X, p.Y, 325) < 0.16f) ||
                !gunship && piece.X == 4 && piece.Y is >= -5 and <= -3)
                continue;
            // An approach trail enters through the ramp or side exits; it cannot erase random wall panels.
            if (!gunship)
                plan.Paths[i] = false;
            plan.Props[i] = CMUExpeditionProp.Hull;
            plan.Reserved[i] = true;
            plan.WreckObjects[i] = new(piece.Prototype, (piece.Turn + site.Rotation) % 4);
        }
        if (gunship)
        {
            AddInterior(-1, 7, "CMUExpeditionWreckSeat");
            AddInterior(2, 6, "CMUExpeditionWreckConsole");
            AddInterior(-2, -4, "CMUExpeditionSupply");
            AddInterior(2, -6, "CMUExpeditionAbandonedPack");
        }
        else
        {
            foreach (var fitting in CMUExpeditionMuleBlueprint.Fittings)
                AddInterior(fitting.X, fitting.Y, fitting.Prototype, fitting.Turn);
            // Heavy, recognizable parts follow the skid, not a uniform carpet of tiny debris.
            AddInterior(-3, -16, "CMUExpeditionMuleToolCase", 1, debris: true);
            AddInterior(3, -19, "CMUExpeditionMuleCargo", 1, debris: true);
            AddInterior(-4, -23, "CMUExpeditionWreckSeat", 3, debris: true);
        }
        plan.Props[plan.Index(site.Center.X, site.Center.Y)] = CMUExpeditionProp.Recovery;
        // Reserve all four approaches to the machine independently of the damaged ship's orientation.
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var i = plan.Index(site.Center.X + dx, site.Center.Y + dy);
            plan.Props[i] = CMUExpeditionProp.None;
            plan.WreckObjects.Remove(i);
            plan.Reserved[i] = true;
        }
        foreach (var (fx, fy) in new[] { (-7, -10), (7, -6), (-6, 3) })
        {
            var fire = SitePoint(site, fx, fy);
            var clear = true;
            for (var dy = -2; dy <= 2 && clear; dy++)
            for (var dx = -2; dx <= 2 && clear; dx++)
            {
                var p = new CMUExpeditionPoint(fire.X + dx, fire.Y + dy);
                clear &= InsideCrashBounds(plan, p) && CanDressFeature(plan, p.X, p.Y);
            }
            if (!clear)
                continue;
            for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
            {
                var i = plan.Index(fire.X + dx, fire.Y + dy);
                plan.Reserved[i] = true;
                plan.Props[i] = CMUExpeditionProp.None;
                plan.Details[i] = CMUExpeditionDetail.Ash;
            }
            plan.FirePockets.Add(fire);
        }

        CMUExpeditionPoint WreckPoint(int x, int y)
        {
            if (!gunship)
            {
                // A single sheared nacelle leaves the cargo keel intact; damage moves whole modules.
                var side = variant == 1 ? 1 : -1;
                if (Math.Sign(x) == side && Math.Abs(x) >= 6)
                {
                    x += side * 3;
                    y -= plan.CrashImpact == CMUExpeditionCrashImpact.CliffStrike ? 4 : 2;
                }
                if (plan.CrashImpact == CMUExpeditionCrashImpact.Breakup && y >= 6)
                {
                    x++;
                    y += 2;
                }
                return SitePoint(site, x, y);
            }
            // Cargo extension, displaced forward section, or a torn-away engine side.
            if (variant == 0 && y < -2)
                y -= 2;
            if (variant == 1 && y > 3)
            {
                x += 2;
                y += 2;
            }
            if (variant == 2 && x < -2)
            {
                x -= 2;
                y--;
            }
            return SitePoint(site, x, y);
        }

        bool CanPlace(CMUExpeditionPoint p) => InsideCrashBounds(plan, p) &&
            plan.BaseTerrain[plan.Index(p.X, p.Y)] is not (CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff) &&
            plan.Terrain[plan.Index(p.X, p.Y)] != CMUExpeditionTerrain.Deck;

        void AddInterior(int x, int y, string prototype, int turn = 0, bool debris = false)
        {
            var p = WreckPoint(x, y);
            if (!CanPlace(p))
                return;
            var i = plan.Index(p.X, p.Y);
            if (gunship && plan.Paths[i] || plan.Props[i] != CMUExpeditionProp.None)
                return;
            if (!gunship)
                plan.Paths[i] = false;
            plan.Props[i] = debris ? CMUExpeditionProp.CargoDebris : CMUExpeditionProp.Supply;
            plan.Reserved[i] = true;
            plan.WreckObjects[i] = new(prototype, (turn + site.Rotation) % 4);
        }
    }

    private static bool InsideCrashBounds(CMUExpeditionPlan plan, CMUExpeditionPoint p) =>
        p.X > 1 && p.Y > 1 && p.X < plan.Size - 2 && p.Y < plan.Size - 2;
}
