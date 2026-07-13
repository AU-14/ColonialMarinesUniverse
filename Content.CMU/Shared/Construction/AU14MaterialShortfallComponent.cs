// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared._AU14.Construction;

/// <summary>
/// Anti-dupe bookkeeping for the construction-skill material discount: records how many units of a basic
/// material were NOT paid when this structure was built (skilled builders build cheaper). When the structure
/// is deconstructed, the refund the graph spawns is reduced by this shortfall, so deconstruction output can
/// never exceed what was actually invested (Input = Output).
/// </summary>
[RegisterComponent]
public sealed partial class AU14MaterialShortfallComponent : Component
{
    /// <summary>The stack type id of the discounted material (e.g. CMSteel).</summary>
    [DataField]
    public string StackTypeId = string.Empty;

    /// <summary>Units of that material saved by the discount = units to remove from any deconstruct refund.</summary>
    [DataField]
    public int Missing;
}
