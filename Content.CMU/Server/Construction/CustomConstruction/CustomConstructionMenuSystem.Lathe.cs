// SPDX-License-Identifier: LicenseRef-AdvancedAtkinsonatorv2-Proprietary
// Copyright (c) 2026 wray-git. All rights reserved.
// Proprietary - reuse only with the Author's prior written authorization. See LICENSE-AdvancedAtkinsonatorv2.md.
using System.IO;
using System.Linq;
using System.Text;
using Content.Shared._AU14.Construction.CustomConstruction;
using Content.Shared.Database;
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
/// lathe machines reference the packs <c>AU14AutolatheRecipes</c> / <c>AU14ArmylatheRecipes</c> (added to their
/// staticPacks); those pack files are rewritten from the recipe files every time an entry is added. Like the
/// rest of this system, it is restart-applied (committed to the content tree, shipped to clients).
/// </summary>
public sealed partial class CustomConstructionMenuSystem
{
    private const string LatheRecipePrefix = "AU14LRecipe_";
    private const string LatheHeader = "# lathe:";

    // The pack ids referenced by lathe.yml / armylathe.yml staticPacks, and the files that define them.
    private const string AutolathePackId = "AU14AutolatheRecipes";
    private const string ArmylathePackId = "AU14ArmylatheRecipes";
    private const string AutolathePackFile = "AU14AutolathePack.yml";
    private const string ArmylathePackFile = "AU14ArmylathePack.yml";

    private string? LatheDir => _generatedDir == null ? null : Path.Combine(_generatedDir, "Lathe");

    private void OnRequestOpenLathe(RequestOpenCustomLatheEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanEditConstructionMenu(session))
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
        if (!CanEditConstructionMenu(session) || LatheDir == null)
            return;

        // Only allow deleting our own generated recipe files (prefix-guarded; no path traversal).
        if (string.IsNullOrWhiteSpace(msg.RecipeId) || !msg.RecipeId.StartsWith(LatheRecipePrefix, StringComparison.Ordinal))
            return;

        var path = Path.Combine(LatheDir, $"{Path.GetFileName(msg.RecipeId)}.yml");
        try
        {
            if (File.Exists(path))
                File.Delete(path);
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
        if (!CanEditConstructionMenu(session))
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

        var time = msg.CompleteTime <= 0 ? 4f : msg.CompleteTime;
        var key = $"{msg.Lathe}__{Sanitize(msg.ResultId)}";
        var recipeId = $"{LatheRecipePrefix}{key}";

        try
        {
            Directory.CreateDirectory(LatheDir);
            File.WriteAllText(
                Path.Combine(LatheDir, $"{recipeId}.yml"),
                BuildLatheRecipeYaml(recipeId, msg.Lathe, msg.ResultId, steel, glass, plastic, time),
                Encoding.UTF8);

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
    /// Rewrites the two pack files from every generated lathe-recipe file, grouping by the <c>#  lathe:</c>
    /// header so each recipe lands in its machine's pack. Always writes both packs (empty if none) so the
    /// staticPacks references on the lathes never dangle.
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

        File.WriteAllText(Path.Combine(LatheDir, AutolathePackFile), BuildPackYaml(AutolathePackId, "CMAutolathe", autolathe), Encoding.UTF8);
        File.WriteAllText(Path.Combine(LatheDir, ArmylathePackFile), BuildPackYaml(ArmylathePackId, "CMArmylathe", armylathe), Encoding.UTF8);
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
