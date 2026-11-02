// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Forge;

// Marks a smeltery and the highest temperature it can reach.
[RegisterComponent, NetworkedComponent]
public sealed partial class SmelteryComponent : Component
{
    // The hottest this furnace can make what sits in it: the clay one tops out at 1500 degrees, the stone one at 2000.
    [DataField]
    public float MaxTemperature = 1500f;
}
