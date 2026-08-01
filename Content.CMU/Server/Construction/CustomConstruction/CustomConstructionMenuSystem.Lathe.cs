// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Linq;
using System.Text;
using Content.Shared._AU14.Administration;
using Content.Shared._AU14.Construction.CustomConstruction;
using Content.Shared.Database;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Construction.CustomConstruction;

/// <summary>
/// Lathe sibling of the construction-items editor (see <see cref="CustomConstructionMenuSystem"/>): lets a
/// permitted admin add print recipes to the CM autolathe / armylathe with a material cost, straight from the
/// menu's Admin Tools.
///
/// Each added recipe is written as a self-contained <c>latheRecipe</c> file under Generated/Lathe/. The two
/// lathe machines reference the baseline packs <c>AU14AutolatheRecipes</c> / <c>AU14ArmylatheRecipes</c>;
/// the runtime pack contents are rebuilt from DB/generated recipes and hot-loaded into those pack ids.
/// </summary>
public sealed partial class CustomConstructionMenuSystem
{
    private const string LatheRecipePrefix = "AU14LRecipe_";
    private const string LatheHeader = "# lathe:";

    // The pack ids referenced by lathe.yml / armylathe.yml staticPacks.
    private const string AutolathePackId = "AU14AutolatheRecipes";
    private const string ArmylathePackId = "AU14ArmylatheRecipes";

    private string? LatheDir => _generatedDir == null ? null : Path.Combine(_generatedDir, "Lathe");

    private void EnsureLathePackFallbacks()
    {
        EnsureLathePackFallback(AutolathePackId, "CMAutolathe");
        EnsureLathePackFallback(ArmylathePackId, "CMArmylathe");
    }

    private void EnsureLathePackFallback(string packId, string lathe)
    {
        if (_prototype.HasIndex<LatheRecipePackPrototype>(packId))
            return;

        try
        {
            _prototype.LoadString(BuildPackYaml(packId, lathe, new List<string>()), overwrite: true);
            _prototype.ResolveResults();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to create fallback lathe recipe pack {packId}: {e}");
        }
    }

    private void OnRequestOpenLathe(RequestOpenCustomLatheEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.Lathe))
            return;

        RaiseNetworkEvent(new OpenCustomLatheEditorEvent { ExistingRecipes = EnumerateLatheRecipes() }, session);
    }

    /// <summary>Reads every generated lathe-recipe file into descriptors (id + lathe + result) for the editor list.</summary>
    private List<CustomLatheRecipeInfo> EnumerateLatheRecipes()
    {
        var result = new List<CustomLatheRecipeInfo>();
        if (LatheDir == null || !Directory.Exists(LatheDir))
            return result;

        foreach (var file in Directory.EnumerateFiles(LatheDir, $"{LatheRecipePrefix}*.yml"))
        {
            var recipeId = Path.GetFileNameWithoutExtension(file);
            var target = ReadLatheTarget(file);
            var resultId = string.Empty;
            try
            {
                foreach (var line in File.ReadLines(file))
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("result:"))
                    {
                        resultId = trimmed["result:".Length..].Trim();
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to read lathe recipe {file}: {e}");
            }

            result.Add(new CustomLatheRecipeInfo { Lathe = target, RecipeId = recipeId, Result = resultId });
        }

        result.Sort((a, b) => string.Compare(a.RecipeId, b.RecipeId, StringComparison.Ordinal));
        return result;
    }

    private void OnRemoveLatheRecipe(RemoveCustomLatheRecipeEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.Lathe) || LatheDir == null)
            return;

        // Only allow deleting our own generated recipe files (prefix-guarded; no path traversal).
        if (string.IsNullOrWhiteSpace(msg.RecipeId) || !msg.RecipeId.StartsWith(LatheRecipePrefix, StringComparison.Ordinal))
            return;

        var path = Path.Combine(LatheDir, $"{Path.GetFileName(msg.RecipeId)}.yml");
        try
        {
            if (File.Exists(path))
            {
                // Unload server-side so printing it stops working this round; the packs republished below
                // drop it from every client's lathe UI (a leftover recipe prototype on clients is harmless,
                // only a pack referencing a MISSING recipe ever errors).
                UnloadYaml(File.ReadAllText(path), msg.RecipeId);
                File.Delete(path);
            }
            DbDelete(DbKindLathe, Path.GetFileName(msg.RecipeId));
            RegenerateLathePacks();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to remove lathe recipe {msg.RecipeId}: {e}");
            return;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{session.Name} removed lathe recipe {msg.RecipeId}");
        PopupTo(session, Loc.GetString("construction-menu-lathe-removed", ("recipe", msg.RecipeId)), PopupType.Medium);
    }

    private void OnSubmitLathe(SubmitCustomLatheEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.Lathe))
            return;

        if (LatheDir == null)
        {
            PopupTo(session, Loc.GetString("construction-menu-verb-no-resources"), PopupType.MediumCaution);
            return;
        }

        if (string.IsNullOrWhiteSpace(msg.ResultId) || !_prototype.HasIndex<EntityPrototype>(msg.ResultId))
        {
            PopupTo(session, Loc.GetString("construction-menu-invalid-entity", ("entity", msg.ResultId)), PopupType.MediumCaution);
            return;
        }

        var steel = Math.Max(0, msg.SteelCost);
        var glass = Math.Max(0, msg.GlassCost);
        var plastic = Math.Max(0, msg.PlasticCost);
        if (steel + glass + plastic <= 0)
        {
            PopupTo(session, Loc.GetString("construction-menu-lathe-invalid-cost"), PopupType.MediumCaution);
            return;
        }

        // NaN passes a "<= 0" check and would emit a literal "NaN" the prototype parser rejects next restart.
        var time = float.IsFinite(msg.CompleteTime) && msg.CompleteTime > 0
            ? Math.Min(msg.CompleteTime, MaxStepSeconds)
            : 4f;
        var key = $"{msg.Lathe}__{Sanitize(msg.ResultId)}";
        var recipeId = $"{LatheRecipePrefix}{key}";

        try
        {
            Directory.CreateDirectory(LatheDir);
            var yaml = BuildLatheRecipeYaml(recipeId, msg.Lathe, msg.ResultId, steel, glass, plastic, time);
            if (IsOversizedYaml(yaml, out var sizeReason))
            {
                PopupTo(session, Loc.GetString("construction-menu-verb-invalid", ("reason", sizeReason)), PopupType.MediumCaution);
                return;
            }

            File.WriteAllText(Path.Combine(LatheDir, $"{recipeId}.yml"), yaml, Encoding.UTF8);
            DbUpsert(DbKindLathe, recipeId, yaml);

            // Publish the RECIPE before the packs that reference it: a client must never receive a pack
            // listing a recipe id it doesn't know (that is exactly the UnknownPrototypeException the lathe
            // UI throws). RegenerateLathePacks publishes the updated packs afterwards.
            PublishYaml(yaml, $"lathe recipe {recipeId}");
            RegenerateLathePacks();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to write custom lathe recipe for {msg.ResultId}: {e}");
            PopupTo(session, Loc.GetString("construction-menu-verb-add-failed"), PopupType.MediumCaution);
            return;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} added {msg.Lathe} recipe {msg.ResultId} (steel {steel}, glass {glass}, plastic {plastic}, {time}s)");

        PopupTo(session, Loc.GetString("construction-menu-lathe-added", ("item", msg.ResultId), ("lathe", msg.Lathe.ToString())), PopupType.Medium);
    }

    private static string BuildLatheRecipeYaml(string recipeId, CustomLatheTarget lathe, string result, int steel, int glass, int plastic, float time)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Auto-generated by the AU14 in-game Lathe Editor (Admin Tools > Lathe Editor).");
        sb.AppendLine($"{LatheHeader} {lathe}");
        sb.AppendLine("# Safe to edit, commit, or delete. Deleting and re-running the editor removes it from the lathe.");
        sb.AppendLine("- type: latheRecipe");
        sb.AppendLine($"  id: {recipeId}");
        sb.AppendLine($"  result: {result}");
        sb.AppendLine($"  completetime: {time.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        sb.AppendLine("  materials:");
        if (steel > 0)
            sb.AppendLine($"    CMSteel: {steel}");
        if (glass > 0)
            sb.AppendLine($"    CMGlass: {glass}");
        if (plastic > 0)
            sb.AppendLine($"    RMCPlastic: {plastic}");
        return sb.ToString();
    }

    /// <summary>
    /// Rebuilds the two runtime packs from every generated lathe-recipe file, grouping by the
    /// <c># lathe:</c> header so each recipe lands in its machine's pack. The baseline pack prototypes live
    /// in normal content YAML; generated files only contain actual admin-added recipes.
    /// </summary>
    private void RegenerateLathePacks()
    {
        if (LatheDir == null || !Directory.Exists(LatheDir))
            return;

        var autolathe = new List<string>();
        var armylathe = new List<string>();

        foreach (var file in Directory.EnumerateFiles(LatheDir, $"{LatheRecipePrefix}*.yml"))
        {
            var recipeId = Path.GetFileNameWithoutExtension(file);
            var target = ReadLatheTarget(file);
            if (target == CustomLatheTarget.Armylathe)
                armylathe.Add(recipeId);
            else
                autolathe.Add(recipeId);
        }

        autolathe.Sort(StringComparer.Ordinal);
        armylathe.Sort(StringComparer.Ordinal);

        var autolatheYaml = BuildPackYaml(AutolathePackId, "CMAutolathe", autolathe);
        var armylatheYaml = BuildPackYaml(ArmylathePackId, "CMArmylathe", armylathe);

        // Publish the packs live so lathe UIs (server and every client) reflect the recipe list right away.
        // Callers must have published any newly-added recipe BEFORE this runs (see OnSubmitLathe).
        PublishYaml(autolatheYaml, "autolathe pack");
        PublishYaml(armylatheYaml, "armylathe pack");
    }

    private CustomLatheTarget ReadLatheTarget(string path)
    {
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (!line.StartsWith(LatheHeader))
                    continue;

                return line[LatheHeader.Length..].Trim() == nameof(CustomLatheTarget.Armylathe)
                    ? CustomLatheTarget.Armylathe
                    : CustomLatheTarget.Autolathe;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to read lathe recipe header {path}: {e}");
        }

        return CustomLatheTarget.Autolathe;
    }

    private static string BuildPackYaml(string packId, string lathe, List<string> recipeIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Auto-managed by the AU14 in-game Lathe Editor (Admin Tools > Lathe Editor).");
        sb.AppendLine($"# Referenced by {lathe}'s staticPacks. Do not hand-edit the recipe list; use the in-game editor.");
        sb.AppendLine("- type: latheRecipePack");
        sb.AppendLine($"  id: {packId}");
        if (recipeIds.Count == 0)
        {
            sb.AppendLine("  recipes: []");
        }
        else
        {
            sb.AppendLine("  recipes:");
            foreach (var id in recipeIds)
                sb.AppendLine($"  - {id}");
        }

        return sb.ToString();
    }
}
