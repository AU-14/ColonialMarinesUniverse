// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Forge;

// A tool that gives out after a fixed number of uses: the stone tongs and the stone hammer.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BreaksAfterUsesComponent : Component
{
    [DataField, AutoNetworkedField]
    public int UsesLeft = 5;
}
