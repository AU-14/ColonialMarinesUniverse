// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Assembly;

// An upgrade part for an assembly machine: changes its speed and energy use.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AssemblyUpgradeComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float EnergyMultiplier = 1f;
}
