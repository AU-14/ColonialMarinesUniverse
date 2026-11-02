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

    // What the machine takes in and what it gives out, when it is not to be all that it can (the ids of the stacks and of the things). Nothing in a list means everything,
    // so that one box can feed several machines, and a box does not get what it should not.
    [DataField, AutoNetworkedField]
    public List<string> InputFilter = new();

    [DataField, AutoNetworkedField]
    public List<string> OutputFilter = new();

    // The slots a machine that is not a recipe machine takes things into and gives them out of (the steam smelter, the presses, the grinder...). Empty for a recipe machine,
    // which has its own. A machine with these has no window with the switches, so it has them in its menu of the right mouse button instead.
    [DataField]
    public List<string> InputSlots = new();

    [DataField]
    public List<string> OutputSlots = new();
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

// A thing ticked or unticked in the filter of what the machine takes in (or of what it gives out).
[Serializable, NetSerializable]
public sealed class RecipeMachineFilterMessage(bool output, string id, bool value) : BoundUserInterfaceMessage
{
    public bool Output = output;
    public string Id = id;
    public bool Value = value;
}
