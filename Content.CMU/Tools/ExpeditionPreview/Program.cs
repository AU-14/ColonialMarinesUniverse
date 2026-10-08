using System.Diagnostics;
using System.Globalization;
using System.Text;
using Content.Shared.CMU14.Expeditions;

// Uses the production generator directly; no copy of its algorithm or game server required.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
// Machine-readable layout for map review and tooling, without starting a game server.
if (args.Length == 6 && args[0] == "--plan")
{
    var plan = CMUExpeditionGenerator.Generate(int.Parse(args[3]), Enum.Parse<CMUExpeditionBiome>(args[2], true),
        Enum.Parse<CMUExpeditionLandform>(args[4], true), Enum.Parse<CMUExpeditionStory>(args[5], true));
    File.WriteAllText(Path.GetFullPath(args[1]), System.Text.Json.JsonSerializer.Serialize(plan));
    Console.WriteLine($"{plan.WreckName}: {plan.CrashImpact}; recovery {plan.Objective}; {plan.WreckObjects.Count} wreck objects; {plan.Scorched.Count(s => s)} scorched tiles.");
    return;
}
var output = Path.GetFullPath(args.Length > 0 ? args[0] : "expedition-preview.svg");
var seed = args.Length > 1 ? int.Parse(args[1]) : 42;
CMUExpeditionLandform? fixedLandform = args.Length > 2
    ? Enum.Parse<CMUExpeditionLandform>(args[2], true) : null;
var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1440' height='1240' viewBox='0 0 1440 1240'><rect width='1440' height='1240' fill='#10181b'/><g font-family='sans-serif' fill='#eef2e7'>");
svg.Append($"<text x='24' y='36' font-size='24'>GOVFOR EXPEDITIONS · generator v{CMUExpeditionPlan.GeneratorVersion} · {(fixedLandform?.ToString() ?? "terrain variety")}</text>");
svg.Append("<text x='24' y='61' font-size='14'>Actual 140×140 layouts · L = landing clearing · R = recovery · numbered secondary sites · schematic, not in-game art</text>");
var watch = Stopwatch.StartNew();
var gallery = new (CMUExpeditionBiome Biome, CMUExpeditionLandform Landform)[]
{
    (CMUExpeditionBiome.Woodland, CMUExpeditionLandform.RiverValley),
    (CMUExpeditionBiome.Beach, CMUExpeditionLandform.Coast),
    (CMUExpeditionBiome.Mountain, CMUExpeditionLandform.Highlands),
    (CMUExpeditionBiome.SwampJungle, CMUExpeditionLandform.Wetlands),
    (CMUExpeditionBiome.Woodland, CMUExpeditionLandform.LakeCountry),
    (CMUExpeditionBiome.Beach, CMUExpeditionLandform.Archipelago),
    (CMUExpeditionBiome.Swamp, CMUExpeditionLandform.Delta),
    (CMUExpeditionBiome.BurnedWoodland, CMUExpeditionLandform.Ridgeline),
    (CMUExpeditionBiome.Tundra, CMUExpeditionLandform.Fjord),
    (CMUExpeditionBiome.Woodland, CMUExpeditionLandform.Caldera),
    (CMUExpeditionBiome.Mountain, CMUExpeditionLandform.Ridgeline),
    (CMUExpeditionBiome.SwampJungle, CMUExpeditionLandform.RiverValley),
};
for (var panel = 0; panel < 12; panel++)
{
    var biome = fixedLandform.HasValue ? CMUExpeditionBiome.Woodland : gallery[panel].Biome;
    var landform = fixedLandform ?? gallery[panel].Landform;
    var story = fixedLandform.HasValue ? CMUExpeditionStory.CrashRecovery : (CMUExpeditionStory) (panel % 4);
    var panelSeed = unchecked(seed + panel * 7919);
    var plan = CMUExpeditionGenerator.Generate(panelSeed, biome, landform, story);
    var ox = 24 + panel % 4 * 356;
    var oy = 100 + panel / 4 * 374;
    svg.Append($"<text x='{ox}' y='{oy}' font-size='17'>{biome} · {landform}</text><text x='{ox}' y='{oy + 20}' font-size='13' fill='#aab8b9'>Seed {panelSeed} · {plan.Sites.Count} sites · {story}</text>");
    svg.Append($"<g shape-rendering='crispEdges' transform='translate({ox},{oy + 32}) scale(2.3)'>");
    for (var y = 0; y < plan.Size; y++)
    for (var x = 0; x < plan.Size; x++)
    {
        var i = plan.Index(x, y);
        var color = plan.Terrain[i] switch
        {
            CMUExpeditionTerrain.Ground => biome switch
            {
                CMUExpeditionBiome.Tundra => "#cbd7da",
                CMUExpeditionBiome.Mountain => "#8b908a",
                CMUExpeditionBiome.SwampJungle => "#45674c",
                CMUExpeditionBiome.BurnedWoodland => "#77634e",
                _ => "#5d7750",
            },
            CMUExpeditionTerrain.Scrub => biome is CMUExpeditionBiome.Tundra or CMUExpeditionBiome.Mountain ? "#edf0e5" : "#405e3e",
            CMUExpeditionTerrain.Mud => biome == CMUExpeditionBiome.Tundra ? "#abbabe" : "#695f45",
            CMUExpeditionTerrain.Water => biome == CMUExpeditionBiome.Tundra ? "#88b7c4" : "#355967",
            CMUExpeditionTerrain.Stone => "#6e7771",
            CMUExpeditionTerrain.Beach => "#d9c58d",
            CMUExpeditionTerrain.Cliff => "#464d51",
            CMUExpeditionTerrain.Deck => "#97856a",
            CMUExpeditionTerrain.Structure => "#98a5a5",
            _ => biome == CMUExpeditionBiome.Tundra ? "#d8d1be" : "#a59168",
        };
        svg.Append($"<path d='M{x} {plan.Size - y - 1}h1v1h-1z' fill='{color}'/>");
        if (plan.Details[i] != CMUExpeditionDetail.None)
        {
            var detailColor = plan.Details[i] switch
            {
                CMUExpeditionDetail.Pebbles => "#9da393",
                CMUExpeditionDetail.Litter or CMUExpeditionDetail.Deadwood => "#514531",
                CMUExpeditionDetail.Reeds => "#92a077",
                CMUExpeditionDetail.Flowers => "#d5b77c",
                _ => "#6b874a",
            };
            svg.Append($"<rect x='{x + 0.25}' y='{plan.Size - y - 0.75}' width='.5' height='.5' fill='{detailColor}'/>");
        }
        if (plan.Props[i] is not (CMUExpeditionProp.None or CMUExpeditionProp.Boundary))
        {
            var propColor = plan.Props[i] switch
            {
                CMUExpeditionProp.Tree => biome == CMUExpeditionBiome.BurnedWoodland ? "#342c28" : "#1e3f2a",
                CMUExpeditionProp.Rock => "#384447",
                CMUExpeditionProp.Recovery => "#ffb852",
                _ => "#292d31",
            };
            svg.Append($"<rect x='{x + 0.1}' y='{plan.Size - y - 0.9}' width='.8' height='.8' fill='{propColor}'/>");
        }
    }
    for (var i = 0; i < plan.Sites.Count; i++)
    {
        var p = plan.Sites[i].Center;
        var label = i == 0 ? "L" : i == 1 ? "R" : (i - 1).ToString();
        svg.Append($"<circle cx='{p.X + 0.5}' cy='{plan.Size - p.Y - 0.5}' r='3.8' fill='#10181b' stroke='#e2c279' stroke-width='.4'/><text x='{p.X + 0.5}' y='{plan.Size - p.Y + 1}' text-anchor='middle' font-size='4.5'>{label}</text>");
    }
    svg.Append("</g>");
    Console.WriteLine($"{biome}/{landform}/{story}: {plan.Props.Count(p => p != CMUExpeditionProp.None)} solid props, {plan.Details.Count(d => d != CMUExpeditionDetail.None)} forest-floor details, {plan.WaterDepth.Count(d => d > 0)} water entities; {plan.Bridges.Count} bridges; terrain attempt {plan.TerrainAttempt}; LZ {plan.LandingZone}; recovery {plan.Objective}");
}
svg.Append("</g></svg>");
File.WriteAllText(output, svg.ToString());
Console.WriteLine($"Wrote {output}; generation and SVG export: {watch.ElapsedMilliseconds} ms.");
