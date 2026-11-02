// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Whitelist;

namespace Content.Shared.CMU14.ToolDamage;

[RegisterComponent]
// Takes full damage from the preferred tools and only a part of it from anything else.
public sealed partial class PreferredToolComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    // Share of the damage that still goes through when the tool is not a preferred one.
    [DataField]
    public float OtherMultiplier = 1f / 3f;
}
