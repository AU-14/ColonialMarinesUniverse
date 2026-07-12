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
}
