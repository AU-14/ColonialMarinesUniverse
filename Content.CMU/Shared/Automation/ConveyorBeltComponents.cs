// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Automation;

// A belt of the factory floor: a tile that carries what lies on it the way it faces. It is the game's own conveyor (the Conveyor component moves things), made to
// work without power or levers: the belt is always on, and the faster kind has a larger speed. A machine with its ports on puts what it has made on a belt that
// leads away from it and takes what it needs from a belt that leads to it; a crate or a box at the end of a belt takes what comes to it.
[RegisterComponent, NetworkedComponent]
public sealed partial class ConveyorBeltComponent : Component
{
    // What the belt turns back into when it is taken up from the floor.
    [DataField(required: true)]
    public EntProtoId Item;

    // Seconds between two looks at what the belt has brought to the crate in front of it.
    [DataField]
    public float Interval = 0.5f;

    [ViewVariables]
    public float Next;
}

// A belt in the hand, a stack of them: a click on the floor puts one down, facing the way the stack is turned, and using the stack in the hand turns it a quarter.
[RegisterComponent]
public sealed partial class BeltPlacerComponent : Component
{
    // The belt that is put down.
    [DataField(required: true)]
    public EntProtoId Belt;

    // The way the belt will carry, in quarter turns clockwise from north.
    [ViewVariables]
    public int Turns;
}
