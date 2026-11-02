// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.Power;

// A cable that goes up and down: it takes the power of the network its node is in, and gives it to the nodes of the same network in the same place on the
// level above and on the level below. The joint is kept by the system for as long as the other side is there.
[RegisterComponent]
public sealed partial class CableShaftComponent : Component
{
    // The name of the node of the entity that carries the network.
    [DataField]
    public string Node = "shaft";

    [DataField]
    public bool Up = true;

    [DataField]
    public bool Down = true;
}
