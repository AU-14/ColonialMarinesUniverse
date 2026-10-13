// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.Flora;

// A stack that shows one sprite for each of the first counts: one piece, two, and the third state for three and more.
[RegisterComponent]
public sealed partial class StackLevelsComponent : Component
{
    [DataField]
    public string Layer = "base";

    [DataField(required: true)]
    public List<string> States = new();
}
