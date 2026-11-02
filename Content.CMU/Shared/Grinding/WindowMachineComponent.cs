// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.Grinding;

// The rules of the machines that are worked through a window (the grinder, the press, the smelters, the assembly machine,
// the boiler and the rest of the machines made here):
//  - a click on the machine neither puts anything into it nor takes anything out; the window does that with its slot buttons;
//  - a right click does not name what is inside it;
//  - a stack in the hand is added to a stack of the same kind in one of the StackSlots when none of them is free;
//  - the OutputSlots are the machine's own: the player does not put anything into them.
[RegisterComponent]
public sealed partial class WindowMachineComponent : Component
{
    [DataField]
    public List<string> StackSlots = new();

    [DataField]
    public List<string> OutputSlots = new();
}
