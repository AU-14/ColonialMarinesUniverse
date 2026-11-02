// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Fluids;

// The liquids that go through the fluid pipes are reagents: any of them can be carried. These are the ones the machines
// here make or need. Steam is not among them: it is the gas that already exists (water vapor) and goes through the gas pipes.
public static class FluidKinds
{
    public const string Water = "Water";
    public const string Slag = "RUMCSlag";
}

// A tank inside a machine (or a barrel): up to Capacity of all the liquids together, each counted on its own by reagent.
// Networked so that a machine's window can show what is in it.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FluidTankComponent : Component
{
    [DataField]
    public float Capacity = 100f;

    [DataField, AutoNetworkedField]
    public Dictionary<string, float> Contents = new();

    public float Total
    {
        get
        {
            var total = 0f;
            foreach (var amount in Contents.Values)
                total += amount;

            return total;
        }
    }

    public float Get(string kind) => Contents.GetValueOrDefault(kind);
}

// A length of pipe. It holds a single liquid, whichever the first one to come in was, in a single number; the pipes around it
// level out with it a little every tick, so that a liquid spreads through a run of pipe slowly and evenly. An empty pipe
// takes any liquid. Only the server knows what is in a pipe.
[RegisterComponent]
public sealed partial class FluidPipeComponent : Component
{
    // The reagent in the pipe; null while it is empty.
    [DataField]
    public string? Kind;

    [DataField]
    public float Capacity = 50f;

    // The share of the difference between two pipes that moves each second.
    [DataField]
    public float Rate = 0.6f;

    // A pipe that goes down: it also passes its liquid to the pipe in the same place on the level below.
    [DataField]
    public bool Down;

    // A pipe cut off from everything (the "disconnect from everything" button): it keeps what is in it and levels out with no pipe, and no pipe with it.
    [DataField]
    public bool Isolated;

    [ViewVariables]
    public float Amount;

    // The pipes it was linked to with a screwdriver, besides the ones it touches.
    [ViewVariables]
    public List<EntityUid> Links = new();

    // The pipes next to it, found again whenever a pipe is built or taken away.
    [ViewVariables]
    public List<EntityUid> Neighbours = new();

    // The sides it is joined on, as it is shown to the clients (-1 until it has been worked out).
    [ViewVariables]
    public int Look = -1;
}

// A way for a machine to take a liquid in or give it out. A pipe is joined to a port with a screwdriver: first the pipe, then
// the machine. A port with no liquid named takes any and gives whatever the machine has most of.
[RegisterComponent]
public sealed partial class FluidPortsComponent : Component
{
    [DataField]
    public List<FluidPort> Ports = new();
}

[DataDefinition]
public sealed partial class FluidPort
{
    [DataField(required: true)]
    public string Id = string.Empty;

    // The reagent the port is for; null for any liquid.
    [DataField]
    public string? Kind;

    // An output port gives the machine's fluid to the pipe, an input port takes it from the pipe.
    [DataField]
    public bool Output;

    // Units a second.
    [DataField]
    public float Rate = 8f;

    // How much of this fluid the machine keeps at most; without it, as much as the tank holds.
    [DataField]
    public float? Limit;

    [DataField]
    public int MaxLinks = 1;

    // What the machine does with the rate of the port: a regulator turns it up and down.
    [ViewVariables]
    public float Scale = 1f;

    [ViewVariables]
    public List<EntityUid> Links = new();
}

// A tank that can be filled from, and drawn into, the containers people carry: pour a beaker into it, and draw whatever it holds
// into an empty one. This is how any liquid gets into and out of the fluid pipes.
[RegisterComponent]
public sealed partial class FluidFillableComponent : Component
{
}
