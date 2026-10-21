// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// A plate of the floor with the code of a mine in it, used once. It is built over a pit (the "build on" menu of the pit, for ten sheets of metal), and the pit becomes
// floor: nothing on the tile shows what it is. It remembers the factions of the one who built it, and lets those pass. When someone of another faction steps on it, it
// gives way: it turns back into a pit and whoever stepped on it falls in.
[RegisterComponent]
public sealed partial class MinePlateComponent : Component
{
    // The factions of the one who laid the plate: the plate is theirs, and it holds for everyone they are friends with.
    [DataField]
    public HashSet<string> Factions = new();

    // The one who laid it: with no faction to go by, they are still not the one it is for.
    [ViewVariables]
    public EntityUid? Placer;

    // What the plate turns into when it gives way.
    [DataField]
    public EntProtoId Collapse = "RUMCPit";
}
