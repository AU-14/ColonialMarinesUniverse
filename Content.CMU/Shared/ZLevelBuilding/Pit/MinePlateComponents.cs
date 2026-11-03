// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// A plate of the floor with the code of a mine in it, used once. It is built over a pit (the "build on" menu of the pit, for ten sheets of metal), and the pit becomes
// floor: nothing on the tile shows what it is. It knows no friend: when anyone steps on it, it gives way, turns back into a pit and whoever stepped on it falls in. The CLF see
// the plates (a mark over the tile), and a mine detector finds one a tile away.
// Networked, so that the client knows where the plates are for the mark.
[RegisterComponent, NetworkedComponent]
public sealed partial class MinePlateComponent : Component
{
    // The one who laid it.
    [ViewVariables]
    public EntityUid? Placer;

    // What the plate turns into when it gives way.
    [DataField]
    public EntProtoId Collapse = "RUMCPit";
}
