// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Grinding;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrinderBladeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Durability = 100;

    [DataField]
    public int MaxDurability = 100;
}
