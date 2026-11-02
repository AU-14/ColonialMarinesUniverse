// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Assembly;

// Key of the assembly machine window.
[Serializable, NetSerializable]
public enum AssemblyMachineUiKey : byte
{
    Key,
}

// Key of the blueprint window.
[Serializable, NetSerializable]
public enum AssemblyBlueprintUiKey : byte
{
    Key,
}
