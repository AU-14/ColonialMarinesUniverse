// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Electric;

// The automatic input and output of a machine: with the output on, the machine puts what it has made into a box or a crate that stands
// next to it; with the input on, it takes from such a box what its recipes need. Both are off until they are turned on in the window.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutoPortsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool AutoOutput;

    [DataField, AutoNetworkedField]
    public bool AutoInput;

    // How far from the machine a box can stand, in tiles.
    [DataField]
    public float Range = 1.3f;

    // Seconds between two turns of moving.
    [DataField]
    public float Interval = 1f;

    [ViewVariables]
    public float Next;
}

// The two switches of the window.
[Serializable, NetSerializable]
public sealed class RecipeMachineAutoMessage(bool output, bool value) : BoundUserInterfaceMessage
{
    public bool Output = output;
    public bool Value = value;
}
