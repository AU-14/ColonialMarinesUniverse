// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Cables;

// Which layer a cable lies on. A cable laid by hand lies on the top layer, in plain view over the floor tile; a screwdriver
// click on the tile tucks it under the tile, and the next click brings it back. A cable without this component keeps its
// old way: it is hidden whenever a floor tile covers it.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CableLayerComponent : Component
{
    // True: under the floor tile (hidden while a tile covers it). False: on the top layer.
    [DataField, AutoNetworkedField]
    public bool Buried = true;
}
