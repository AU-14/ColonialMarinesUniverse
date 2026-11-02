// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.Mining;

// A wall (the clay, the stone) that the last blow to was dealt with a tool that gives out (BreaksAfterUses): when the wall falls, that tool has dug a block and pays a use.
// It is a note of the server, put on the wall by the blow and taken off by a blow of any tool that does not give out.
[RegisterComponent]
public sealed partial class WallHitByToolComponent : Component
{
    public EntityUid Tool;
    public EntityUid User;
}
