// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Electric;

// A generator that lives on something that lies beside it: the geothermal generator on the lava of the black stone layer. Every piece of the
// source within the reach gives its share of the power, up to the most the machine can take.
[RegisterComponent, NetworkedComponent]
public sealed partial class SourceGeneratorComponent : Component
{
    // The prototype of the thing it stands on (the lava).
    [DataField(required: true)]
    public string Source = string.Empty;

    // Tiles around it that it looks in.
    [DataField]
    public float Range = 2.6f;

    // Watts for each piece of the source.
    [DataField]
    public float SupplyPerSource = 1500f;

    // How many pieces it can use.
    [DataField]
    public int MaxSources = 4;

    [ViewVariables]
    public int Found;
}
