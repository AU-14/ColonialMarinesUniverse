// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.Assembly;

[DataDefinition]
public sealed partial class AssemblySlotRequirement
{
    [DataField(required: true)]
    public ProtoId<StackPrototype> Material = default!;

    [DataField]
    public int Amount = 1;
}
