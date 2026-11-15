// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Electric;

// A tool that runs on the power cell in it: every use takes a little of the charge, and a tool with a flat cell does nothing. The speed
// is the SpeedModifier of its Tool component. A digging tool (a pickaxe, a shovel) takes the charge when the digging begins.
[RegisterComponent, NetworkedComponent]
public sealed partial class ElectricToolComponent : Component
{
    // Joules of the cell for one use.
    [DataField]
    public float ChargePerUse = 20f;

    // Joules of the cell for one swing of the tool as a weapon (the jackhammer takes it for every blow on the rock); zero for a tool that does not.
    [DataField]
    public float ChargePerSwing;
}

// A device that looks for deposits of ore and oil around it, and tells the user what it found and where.
[RegisterComponent, NetworkedComponent]
public sealed partial class ProspectorComponent : Component
{
    // How far it sees, in tiles.
    [DataField]
    public float Range = 18f;

    // Joules of the cell for one scan.
    [DataField]
    public float ChargePerScan = 40f;
}
