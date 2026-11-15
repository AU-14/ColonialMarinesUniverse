// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Electric;

// A welder that burns no fuel: it is an ordinary welding tool whose fuel is made out of the charge of the power cell in it. While
// the cell has charge the tool keeps a little welding fuel in its tank, and only what the arc burns is made again.
[RegisterComponent, NetworkedComponent]
public sealed partial class ElectricWelderComponent : Component
{
    // How much fuel the tool keeps ready.
    [DataField]
    public float FuelReady = 10f;

    // Joules of the cell that make one unit of fuel.
    [DataField]
    public float ChargePerFuel = 15f;

    [ViewVariables]
    public float NextCheck;
}
