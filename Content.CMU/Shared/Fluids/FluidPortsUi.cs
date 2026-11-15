// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;
using Robust.Shared.Maths;

namespace Content.Shared.CMU14.Fluids;

// What a pipe looks like: the sides it is joined on (a bit for each: north 1, east 2, south 4, west 8), and whether it goes down.
[Serializable, NetSerializable]
public enum FluidPipeVisuals : byte
{
    Connections,
    Down,
    CMUFlowArrows,
}

/// <summary>A machine connection marker on one side of a pipe, in grid coordinates.</summary>
[Serializable, NetSerializable]
public readonly record struct CMUFluidPipeArrow(Vector2i Side, bool Output, bool OnMachineTile);

// Appearance payloads must both register their collection type and support client-side snapshots.
[Serializable, NetSerializable]
public sealed class CMUFluidPipeArrowState(List<CMUFluidPipeArrow> arrows) : ICloneable
{
    public readonly List<CMUFluidPipeArrow> Arrows = arrows;

    public object Clone()
        => new CMUFluidPipeArrowState(new List<CMUFluidPipeArrow>(Arrows));
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

// Connection state is shared by viewers; the selected pipe stays private to each user on the server.
[Serializable, NetSerializable]
public sealed class FluidPortEntry(string id, bool output, string kind, int maxLinks, List<CMUFluidPortLink> links)
{
    public string Id = id;
    public bool Output = output;
    public string Kind = kind;
    public int MaxLinks = maxLinks;
    public List<CMUFluidPortLink> Links = links;
}

[Serializable, NetSerializable]
public sealed record CMUFluidPortLink(NetEntity Pipe, string Name, string Position);

[Serializable, NetSerializable]
public sealed class FluidPortChosenMessage(string port) : BoundUserInterfaceMessage
{
    public string Port = port;
}

[Serializable, NetSerializable]
public sealed class CMUFluidPortDisconnectMessage(string port, NetEntity pipe) : BoundUserInterfaceMessage
{
    public string Port = port;
    public NetEntity Pipe = pipe;
}
