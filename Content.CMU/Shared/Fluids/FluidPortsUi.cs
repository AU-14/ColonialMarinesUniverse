// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fluids;

// What a pipe looks like: the sides it is joined on (a bit for each: north 1, east 2, south 4, west 8), and whether it goes down.
[Serializable, NetSerializable]
public enum FluidPipeVisuals : byte
{
    Connections,
    Down,
}

// The window that asks which port of a machine a pipe is to be joined to, when the machine has more than one that fits.
[Serializable, NetSerializable]
public enum FluidPortsUiKey : byte
{
    Key,
}

// What the fluid ports window shows: the list of ports.
[Serializable, NetSerializable]
public sealed class FluidPortsBoundUserInterfaceState(List<FluidPortEntry> ports) : BoundUserInterfaceState
{
    public List<FluidPortEntry> Ports = ports;
}

// One port of the machine in the window: its id, whether it is an inlet or an outlet, the liquid it is for (empty for any),
// whether the pipe in the user's hand is joined to it, and how many pipes are joined to it.
[Serializable, NetSerializable]
public sealed class FluidPortEntry(string id, bool output, string kind, bool joined, int links)
{
    public string Id = id;
    public bool Output = output;
    public string Kind = kind;
    public bool Joined = joined;
    public int Links = links;
}

// The port picked in the window.
[Serializable, NetSerializable]
public sealed class FluidPortChosenMessage(string port) : BoundUserInterfaceMessage
{
    public string Port = port;
}
