// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Construction.CustomConstruction;

/// <summary>
/// Client → server: the admin picked a batch of entities in the "Mass Entity Editor" selector and wants the
/// recipe editor opened for the whole batch. The server re-validates permission (and the id list) and replies
/// with <see cref="OpenMassConstructionEditorEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestOpenMassConstructionEditorEvent : EntityEventArgs
{
    public List<string> ProtoIds = new();
}

/// <summary>
/// Server → client: open ONE recipe editor for a whole batch of entities. <see cref="Editor"/> carries the
/// standard editor payload (spawnlists/categories/default steps) keyed off the first entity;
/// <see cref="ProtoIds"/> is the validated batch the client must echo back in the mass submit.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenMassConstructionEditorEvent : EntityEventArgs
{
    public OpenCustomConstructionEditorEvent Editor = new();
    public List<string> ProtoIds = new();
}

/// <summary>
/// Client → server: the admin confirmed the mass editor. One recipe (spawnlist/category/steps) is applied to
/// EVERY entity in <see cref="ProtoIds"/> - each gets its own independent entry file, exactly as if it had been
/// added one-by-one, so any single one can still be changed or removed individually afterwards.
/// </summary>
[Serializable, NetSerializable]
public sealed class SubmitMassConstructionEditorEvent : EntityEventArgs
{
    public List<string> ProtoIds = new();
    public string Spawnlist = string.Empty;
    public string Category = string.Empty;
    public List<CustomConstructionStepData> Steps = new();
    public List<CustomConstructionStepData> DeconstructSteps = new();
    public int Health;

    /// <summary>True = dry run. The server validates and answers with <see cref="OpenDbSavePreviewEvent"/>
    /// listing exactly what WOULD be written (files + database rows) without writing anything. The client
    /// then re-sends the same submit with Preview = false once the admin confirms.</summary>
    public bool Preview;
}

/// <summary>
/// Client → server: the Mass Entity Editor's TILES mode was confirmed. One material cost / category is applied
/// to every tile in <see cref="TileIds"/> - each becomes its own independent generated tile recipe, exactly as
/// if added one-by-one through the Tiles Editor.
/// </summary>
[Serializable, NetSerializable]
public sealed class SubmitMassTileEditorEvent : EntityEventArgs
{
    public List<string> TileIds = new();
    public string Material = string.Empty;
    public int Amount = 1;
    public string Spawnlist = string.Empty;
    public string Category = string.Empty;

    /// <summary>True = file under the Z-Level (Experimental) top-bar page (the "Tiles" spawnlist).</summary>
    public bool ZLevelPage = true;

    /// <summary>True = dry run; see <see cref="SubmitMassConstructionEditorEvent.Preview"/>.</summary>
    public bool Preview;
}

/// <summary>
/// Server → client: the scrollable human-in-the-loop confirmation shown before a save is committed.
/// Lists, line by line, exactly which files and database rows the save would write (built by the SAME
/// validation pass that the real save uses, so what you confirm is what happens). The client re-sends
/// the original submit with Preview = false on confirm, or drops it on cancel.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenDbSavePreviewEvent : EntityEventArgs
{
    /// <summary>Loc id of the window title context ("mass entities" / "mass tiles" / "construction entry").</summary>
    public string Kind = string.Empty;

    /// <summary>One line per planned file write / DB upsert, plus one per rejected input with the reason.</summary>
    public List<string> Lines = new();

    public int Planned;
    public int Rejected;
}
