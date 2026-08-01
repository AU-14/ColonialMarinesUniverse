// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Construction.CustomConstruction;

/// <summary>What kind of requirement a recipe step is.</summary>
[Serializable, NetSerializable]
public enum CustomConstructionStepKind : byte
{
    /// <summary><see cref="CustomConstructionStepData.Value"/> = a material stack id (e.g. CMSteel); consumed.</summary>
    Material = 0,

    /// <summary><see cref="CustomConstructionStepData.Value"/> = a tool quality id (e.g. Welding). Currently rejected.</summary>
    Tool = 1,

    /// <summary><see cref="CustomConstructionStepData.Value"/> = any entity prototype id; consumed into the build.</summary>
    EntityMaterial = 2,

    /// <summary><see cref="CustomConstructionStepData.Value"/> = any entity prototype id; required present but NOT consumed.</summary>
    EntityTool = 3,
}

/// <summary>
/// One ordered step of a custom construction recipe. <see cref="Value"/> is interpreted according to
/// <see cref="Kind"/> (stack id / tool quality / entity prototype id). <see cref="Amount"/> applies to
/// material/entity-material steps.
/// </summary>
[Serializable, NetSerializable]
public struct CustomConstructionStepData
{
    public CustomConstructionStepKind Kind;
    public string Value;
    public int Amount;
    public float DoAfter;

    /// <summary>True for any step that requires a tool-like item (held but not consumed).</summary>
    public readonly bool IsTool => Kind is CustomConstructionStepKind.Tool or CustomConstructionStepKind.EntityTool;

    /// <summary>True when <see cref="Value"/> is an entity prototype id rather than a stack/tool-quality id.</summary>
    public readonly bool IsEntity => Kind is CustomConstructionStepKind.EntityMaterial or CustomConstructionStepKind.EntityTool;
}

/// <summary>
/// Server → client: open the construction-menu editor window for an entity. Sent when a permitted
/// admin uses the world "Add to / Change Recipe" verbs, or in response to the in-menu admin button.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenCustomConstructionEditorEvent : EntityEventArgs
{
    public string ProtoId = string.Empty;
    public string ItemName = string.Empty;
    public bool IsEdit;

    /// <summary>
    /// Identifies which existing entry is being edited (one entity can have several entries with different
    /// spawnlist/category/recipe). Empty when adding a brand-new entry. Round-trips back in the submit event
    /// so the server edits the right file (and renames it if the spawnlist/category changed).
    /// </summary>
    public string EntryKey = string.Empty;

    public string Spawnlist = string.Empty;
    public string Category = string.Empty;
    public List<CustomConstructionStepData> Steps = new();

    /// <summary>
    /// The tool steps required to take the built structure back down (e.g. crowbar, then welder). Empty means
    /// the default single crowbar (Prying) step. Only tool / entity-tool steps are meaningful here - the
    /// material refund is always derived from the build steps. Unused for in-hand item recipes.
    /// </summary>
    public List<CustomConstructionStepData> DeconstructSteps = new();

    /// <summary>Optional override for the built structure's health (destruction threshold). 0 = inherit from parent.</summary>
    public int Health;

    public List<string> AvailableSpawnlists = new();

    /// <summary>
    /// Categories that exist per spawnlist, read from the generated entry files on the server. Lets the
    /// editor show categories for a spawnlist that was just created in-game (its entries aren't loaded as
    /// prototypes until the next restart, so the client can't derive them from prototypes alone).
    /// </summary>
    public Dictionary<string, List<string>> AvailableCategoriesBySpawnlist = new();
}

/// <summary>
/// Client → server: open the editor for an entity chosen from the in-menu "Construction Items Editor"
/// utility. The server re-validates admin permission before opening (the client also pre-checks so
/// non-admins get an immediate popup instead). When <see cref="EntryKey"/> is set, the server opens the editor
/// directly for that existing entry (Change Recipe); empty opens the add/chooser flow.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestOpenCustomConstructionEditorEvent : EntityEventArgs
{
    public string ProtoId = string.Empty;

    /// <summary>The existing entry to edit, or empty to add a new one / show the chooser.</summary>
    public string EntryKey = string.Empty;

    /// <summary>True to skip the chooser and go straight to a blank add-editor even if entries exist.</summary>
    public bool ForceAddNew;

    public RequestOpenCustomConstructionEditorEvent(string protoId)
    {
        ProtoId = protoId;
    }
}

/// <summary>One existing recipe entry of an entity, for the chooser list.</summary>
[Serializable, NetSerializable]
public struct CustomConstructionEntryInfo
{
    public string EntryKey;
    public string Spawnlist;
    public string Category;
}

/// <summary>
/// Server → client: the picked entity already has recipe entries, so show a chooser to change/remove an
/// existing one or add a new recipe (instead of jumping straight into the add editor).
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenCustomConstructionChooserEvent : EntityEventArgs
{
    public string ProtoId = string.Empty;
    public string ItemName = string.Empty;
    public List<CustomConstructionEntryInfo> Entries = new();
}

/// <summary>Client → server: remove an existing recipe entry (from the chooser). Re-validated server-side.</summary>
[Serializable, NetSerializable]
public sealed class RemoveCustomConstructionEntryEvent : EntityEventArgs
{
    public string ProtoId = string.Empty;
    public string EntryKey = string.Empty;
}

/// <summary>
/// Client → server: hide a construction-menu recipe by its construction prototype id (the "Remove Item" button
/// in the menu detail panel). Works for ANY recipe, including vanilla ones the right-click editor can't track:
/// the server records the id in a generated overrides prototype so the menu skips it (applies next restart;
/// the acting admin's client also hides it immediately for this session). Re-validated server-side.
/// </summary>
[Serializable, NetSerializable]
public sealed class HideConstructionRecipeEvent : EntityEventArgs
{
    public string RecipeId = string.Empty;
}

/// <summary>
/// Client → server: bulk-remove EVERY generated recipe in a spawnlist (and category, if set). Destructive, so
/// the editor gates it behind an "include all entities" acknowledgement and a 3-second warning before sending.
/// </summary>
[Serializable, NetSerializable]
public sealed class RemoveCustomConstructionGroupEvent : EntityEventArgs
{
    public string Spawnlist = string.Empty;

    /// <summary>Empty = every category in the spawnlist; otherwise only this category.</summary>
    public string Category = string.Empty;
}

/// <summary>
/// Client → server: open the Spawnlist Delete tool (gated by the "spawnlistdelete" tool permission,
/// re-validated server-side). The server answers with <see cref="OpenSpawnlistDeleteEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestOpenSpawnlistDeleteEvent : EntityEventArgs
{
}

/// <summary>
/// Server → client: opens the Spawnlist Delete window with every spawnlist that currently has generated
/// recipes (construction entries AND tiles), plus how many recipes each holds so the admin sees the blast
/// radius before confirming.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenSpawnlistDeleteEvent : EntityEventArgs
{
    /// <summary>Spawnlist name → number of generated recipes filed under it.</summary>
    public Dictionary<string, int> SpawnlistCounts = new();

    /// <summary>Spawnlist name → (category name → number of generated recipes in that category), so the
    /// window can offer deleting a single category instead of the whole spawnlist.</summary>
    public Dictionary<string, Dictionary<string, int>> CategoryCounts = new();
}

/// <summary>
/// Client → server: delete a whole spawnlist - every generated recipe (construction entries and tiles)
/// filed under it is removed from disk, the database, the server's loaded prototypes, and hidden from
/// open menus. Destructive; the window arms a confirm delay before sending. Re-validated server-side.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeleteSpawnlistEvent : EntityEventArgs
{
    public string Spawnlist = string.Empty;

    /// <summary>When set, only recipes in this category of <see cref="Spawnlist"/> are deleted (the
    /// category's whole contents). Empty means the entire spawnlist, as before.</summary>
    public string Category = string.Empty;
}

/// <summary>
/// Client → server: the admin confirmed the editor. The server re-validates permission, writes the
/// generated prototype file, logs the action, and pops up the result.
/// </summary>
[Serializable, NetSerializable]
public sealed class SubmitCustomConstructionEditorEvent : EntityEventArgs
{
    public string ProtoId = string.Empty;

    /// <summary>The entry being edited, or empty to add a new one. See <see cref="OpenCustomConstructionEditorEvent.EntryKey"/>.</summary>
    public string EntryKey = string.Empty;

    public string Spawnlist = string.Empty;
    public string Category = string.Empty;
    public List<CustomConstructionStepData> Steps = new();

    /// <summary>Tool steps required to deconstruct the built structure (see <see cref="OpenCustomConstructionEditorEvent.DeconstructSteps"/>).</summary>
    public List<CustomConstructionStepData> DeconstructSteps = new();

    /// <summary>Optional override for the built structure's health (destruction threshold). 0 = inherit from parent.</summary>
    public int Health;

    /// <summary>True = dry run; the server answers with an OpenDbSavePreviewEvent instead of writing,
    /// and the client re-sends with Preview = false once the admin confirms.</summary>
    public bool Preview;
}
