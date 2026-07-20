// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Linq;
using System.Text;
using Content.Shared._AU14.Administration;
using Content.Shared._AU14.Construction.CustomConstruction;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Construction.CustomConstruction;

/// <summary>
/// Tiles sibling of the construction-items editor (see <see cref="CustomConstructionMenuSystem"/>): lets a
/// permitted admin add a TILE to the construction menu. Tiles are not entities, so the generator converts the
/// chosen tile into a buildable "tile applier" entity automatically (an entity that lays the tile then deletes
/// itself, the same mechanism the hand-authored Tiles spawnlist uses), plus its construction graph and recipe.
///
/// The admin picks the MAIN CATEGORY (top-bar page): the "Z-Level (Experimental)" page (filed in the "Tiles"
/// spawnlist) or the normal Spawnlists page (filed in a chosen spawnlist). Persistence is restart-applied just
/// like the construction editor: one self-contained YAML file per tile under Generated/Tiles/.
/// </summary>
public sealed partial class CustomConstructionMenuSystem
{
    private const string TileFilePrefix = "AU14Tile_";
    private const string TileChildPrefix = "AU14CustomTile_";
    private const string TilesSpawnlist = "Tiles";
    private const string DefaultTileCategory = "Flooring";

    private string? TilesDir => _generatedDir == null ? null : Path.Combine(_generatedDir, "Tiles");

    private void OnRequestOpenTile(RequestOpenCustomTileEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.Tiles))
            return;

        var tiles = _prototype.EnumeratePrototypes<ContentTileDefinition>()
            .Where(t => !t.Abstract && t.ID != ContentTileDefinition.SpaceID)
            .Select(t => t.ID)
            .OrderBy(id => id, StringComparer.InvariantCulture)
            .ToList();

        RaiseNetworkEvent(new OpenCustomTileEditorEvent
        {
            AvailableTiles = tiles,
            AvailableSpawnlists = EnumerateSpawnlists(),
        }, session);
    }

    private void OnSubmitTile(SubmitCustomTileEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.Tiles))
            return;

        if (TilesDir == null)
        {
            PopupTo(session, Loc.GetString("construction-menu-verb-no-resources"), PopupType.MediumCaution);
            return;
        }

        // Validate the tile and material exist before writing a recipe that could never build.
        if (string.IsNullOrWhiteSpace(msg.TileId) || !_prototype.HasIndex<ContentTileDefinition>(msg.TileId))
        {
            PopupTo(session, Loc.GetString("construction-menu-tile-invalid-tile", ("tile", msg.TileId)), PopupType.MediumCaution);
            return;
        }

        var material = Fallback(msg.Material, "CMSteel");
        if (!_prototype.HasIndex<StackPrototype>(material))
        {
            PopupTo(session, Loc.GetString("construction-menu-invalid-material", ("material", material)), PopupType.MediumCaution);
            return;
        }

        var amount = Math.Clamp(msg.Amount, 1, MaxStepAmount);
        // Whitelisted (see SanitizeName): embedded verbatim in generated YAML - never trust raw client strings.
        var spawnlist = msg.ZLevelPage ? TilesSpawnlist : SanitizeName(msg.Spawnlist, DefaultSpawnlist);
        var category = SanitizeName(msg.Category, DefaultTileCategory);
        var key = $"{Sanitize(msg.TileId)}__{Sanitize(spawnlist)}__{Sanitize(category)}";

        try
        {
            Directory.CreateDirectory(TilesDir);
            var yaml = BuildTileYaml(key, msg.TileId, material, amount, spawnlist, category, msg.ZLevelPage);
            if (IsOversizedYaml(yaml, out var sizeReason))
            {
                PopupTo(session, Loc.GetString("construction-menu-verb-invalid", ("reason", sizeReason)), PopupType.MediumCaution);
                return;
            }

            File.WriteAllText(Path.Combine(TilesDir, $"{TileFilePrefix}{key}.yml"), yaml, Encoding.UTF8);
            DbUpsert(DbKindTiles, $"{TileFilePrefix}{key}", yaml);

            // Apply live (server + all clients) instead of waiting for a restart.
            PublishYaml(yaml, $"tile {key}");
            UnhideRecipeId($"{TileFilePrefix}{key}");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to write custom tile entry for {msg.TileId} (key {key}): {e}");
            PopupTo(session, Loc.GetString("construction-menu-verb-add-failed"), PopupType.MediumCaution);
            return;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} added construction menu TILE {msg.TileId} (spawnlist: {spawnlist}, category: {category}, cost: {amount} {material})");

        PopupTo(session, Loc.GetString("construction-menu-tile-added", ("tile", msg.TileId), ("category", category)), PopupType.Medium);
    }

    /// <summary>
    /// Builds the generated tile-applier entity + graph + recipe. Mirrors the hand-authored Tiles spawnlist
    /// (see Resources/Prototypes/_AU14/ZLevelBuilding/tiles.yml): an applier entity carries a TileApplier that
    /// lays the tile on map-init then deletes itself, and a Construction component so the build path accepts it.
    /// </summary>
    private string BuildTileYaml(string key, string tileId, string material, int amount, string spawnlist, string category, bool zLevelPage)
    {
        var entityId = $"{TileChildPrefix}{key}";
        var graphId = $"AU14CustomTileGraph_{key}";
        var recipeId = $"{TileFilePrefix}{key}";

        var sb = new StringBuilder();
        sb.AppendLine("# Auto-generated by the AU14 in-game TILE editor (Admin Tools > Tiles Editor).");
        sb.AppendLine($"# tile: {tileId}");
        sb.AppendLine($"# spawnlist: {spawnlist}");
        sb.AppendLine($"# category: {category}");
        sb.AppendLine("# Safe to edit, commit, or delete. Deleting removes it from the construction menu.");
        sb.AppendLine();

        // The applier entity (built target): lays the tile then despawns (TileApplierSystem).
        sb.AppendLine("- type: entity");
        sb.AppendLine($"  id: {entityId}");
        sb.AppendLine("  suffix: AU14 Custom Tile");
        sb.AppendLine("  components:");
        sb.AppendLine("  - type: Transform");
        sb.AppendLine("    anchored: true");
        // Sprite: the applier is what the menu shows as the recipe icon, so it wears the ACTUAL tile's
        // texture. Every tile used to render as the generic steel plating, which made a Tiles spawnlist
        // impossible to read at a glance. ContentTileDefinition.Sprite is a direct texture path, so it goes
        // in as a texture layer rather than an RSI state; tiles without one keep the old plating look.
        sb.AppendLine("  - type: Sprite");
        if (TryGetTileSprite(tileId, out var tileSprite))
        {
            sb.AppendLine("    layers:");
            sb.AppendLine($"    - texture: {tileSprite}");
        }
        else
        {
            sb.AppendLine("    sprite: Objects/Tiles/tile.rsi");
            sb.AppendLine("    state: steel");
        }
        sb.AppendLine("  - type: TileApplier");
        sb.AppendLine($"    tile: {tileId}");
        sb.AppendLine("  - type: Construction");
        sb.AppendLine($"    graph: {graphId}");
        sb.AppendLine("    node: tile");
        sb.AppendLine();

        // Construction graph: lay the tile by consuming the material.
        sb.AppendLine("- type: constructionGraph");
        sb.AppendLine($"  id: {graphId}");
        sb.AppendLine("  start: start");
        sb.AppendLine("  graph:");
        sb.AppendLine("  - node: start");
        sb.AppendLine("    edges:");
        sb.AppendLine("    - to: tile");
        sb.AppendLine("      steps:");
        sb.AppendLine($"      - material: {material}");
        sb.AppendLine($"        amount: {amount}");
        sb.AppendLine("        doAfter: 1");
        sb.AppendLine("  - node: tile");
        sb.AppendLine($"    entity: {entityId}");
        sb.AppendLine();

        // The construction recipe. canBuildInImpassable lets it floor open air (for z-level building).
        sb.AppendLine("- type: construction");
        sb.AppendLine($"  id: {recipeId}");
        sb.AppendLine($"  name: \"{tileId.Replace("\"", "'")}\"");
        sb.AppendLine("  description: A floor tile added through the in-game tile editor.");
        sb.AppendLine($"  graph: {graphId}");
        sb.AppendLine("  startNode: start");
        sb.AppendLine("  targetNode: tile");
        sb.AppendLine($"  category: {category}");
        sb.AppendLine("  objectType: Structure");
        sb.AppendLine("  placementMode: SnapgridCenter");
        sb.AppendLine("  canRotate: false");
        sb.AppendLine("  canBuildInImpassable: true");
        sb.AppendLine("  isCM: true");
        sb.AppendLine($"  spawnlist: {spawnlist}");
        // Z-level-page tiles obey the mapper opt-out (so you can't floor the air under the Almayer); normal
        // Spawnlists-page tiles are plain flooring with no such restriction.
        if (zLevelPage)
        {
            sb.AppendLine("  conditions:");
            sb.AppendLine("  - !type:ZBuildAllowed");
        }
        // NOTE: no "icon:" block - this fork's ConstructionPrototype has no icon field (the YAML Linter CI
        // rejects it as an unknown field). The menu derives the recipe icon from the applier entity's sprite.

        return sb.ToString();
    }

    /// <summary>Texture path of a tile definition's own sprite, for use as the recipe icon.</summary>
    private bool TryGetTileSprite(string tileId, out string texture)
    {
        texture = string.Empty;
        if (_tileDefManager[tileId] is not ContentTileDefinition def || def.Sprite is not { } sprite)
            return false;

        texture = sprite.ToString();
        return !string.IsNullOrWhiteSpace(texture);
    }
}
