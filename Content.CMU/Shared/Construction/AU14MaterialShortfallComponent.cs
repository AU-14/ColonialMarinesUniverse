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
    /// <summary>
    /// Units saved per stack type. A construction graph can consume several discounted materials (for example,
    /// both CM steel and CM plasteel), so tracking only the last material would allow the others to be duplicated
    /// by deconstruction.
    /// </summary>
    [DataField]
    public Dictionary<string, int> MissingByStack = new();
}
