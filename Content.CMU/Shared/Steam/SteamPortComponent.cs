// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Steam;

// A machine that takes steam in, or gives it out, through a gas pipe. Steam is the gas that already exists, water vapor: the
// machine has a pipe node of the gas network named Node, and this is what its window and its description show of it.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SteamPortComponent : Component
{
    // The pipe node of the machine the steam is taken from or put into.
    [DataField]
    public string Node = "steam";

    // What the gauge of the window counts as full, in moles of water vapor.
    [DataField]
    public float GaugeMoles = 20f;

    // What is in the pipes joined to the machine, kept up to date by the server a few times a second.
    [DataField, AutoNetworkedField]
    public float Moles;

    [DataField, AutoNetworkedField]
    public float Pressure;
}
