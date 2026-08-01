// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Construction.CustomConstruction;

/// <summary>Which lathe an admin-added recipe belongs to.</summary>
[Serializable, NetSerializable]
public enum CustomLatheTarget : byte
{
    Autolathe = 0,
    Armylathe = 1,
}

/// <summary>Client -> server: the admin pressed the in-menu "Lathe Editor" button. The server re-checks permission.</summary>
[Serializable, NetSerializable]
public sealed class RequestOpenCustomLatheEditorEvent : EntityEventArgs
{
}

/// <summary>One existing admin-added lathe recipe, for the editor's removal list.</summary>
[Serializable, NetSerializable]
public struct CustomLatheRecipeInfo
{
    public CustomLatheTarget Lathe;
    public string RecipeId;
    public string Result;
}

/// <summary>Server -> client: open the lathe editor, with the recipes already added (so they can be removed).</summary>
[Serializable, NetSerializable]
public sealed class OpenCustomLatheEditorEvent : EntityEventArgs
{
    public System.Collections.Generic.List<CustomLatheRecipeInfo> ExistingRecipes = new();
}

/// <summary>Client -> server: remove a previously-added lathe recipe (by its generated recipe id).</summary>
[Serializable, NetSerializable]
public sealed class RemoveCustomLatheRecipeEvent : EntityEventArgs
{
    public string RecipeId = string.Empty;
}

/// <summary>
/// Client -> server: add a print recipe to one of the CM lathes. The server validates the result entity,
/// writes the latheRecipe, and rebuilds the lathe's recipe pack (applies next restart).
/// </summary>
[Serializable, NetSerializable]
public sealed class SubmitCustomLatheEditorEvent : EntityEventArgs
{
    /// <summary>Which lathe gets the recipe (autolathe / armylathe).</summary>
    public CustomLatheTarget Lathe = CustomLatheTarget.Autolathe;

    /// <summary>Entity prototype the lathe prints.</summary>
    public string ResultId = string.Empty;

    /// <summary>Steel cost to print (material units; one sheet is 100). 0 = none.</summary>
    public int SteelCost;

    /// <summary>Glass cost to print. 0 = none.</summary>
    public int GlassCost;

    /// <summary>Plastic cost to print. 0 = none.</summary>
    public int PlasticCost;

    /// <summary>Seconds to print.</summary>
    public float CompleteTime = 4f;
}
