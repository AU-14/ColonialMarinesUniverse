// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Construction.CustomConstruction;

/// <summary>
/// AU14 construction-menu overrides, persisted as a generated prototype so the client can hide recipes the
/// admin removed from the menu - including VANILLA recipes that the right-click recipe editor never sees
/// (it only tracks recipes IT generated). The server writes a single instance of this into
/// <c>Resources/Prototypes/_AU14/CustomConstruction/Generated/Overrides/</c>; on the next restart the client
/// loads it and the construction-menu presenter skips any recipe whose construction id is listed here.
/// </summary>
[Prototype("au14MenuOverrides")]
public sealed partial class AU14MenuOverridesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Construction prototype ids to hide from the construction menu.</summary>
    [DataField]
    public HashSet<string> HiddenRecipes = new();
}
