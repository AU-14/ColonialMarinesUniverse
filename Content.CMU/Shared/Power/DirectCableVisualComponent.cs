// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Power;

// What the player sees of a cable run by hand from one device to another: the ends it has. Both devices carry the list, and the client draws the
// line (thick, in the colour of the network it carries) once for every pair. The network itself lives in the nodes of the devices.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DirectCableVisualComponent : Component
{
    // The other ends, and what each of the cables carries (the node group: HVPower, MVPower, Apc).
    [DataField, AutoNetworkedField]
    public List<NetEntity> Others = new();

    [DataField, AutoNetworkedField]
    public List<string> Groups = new();
}
