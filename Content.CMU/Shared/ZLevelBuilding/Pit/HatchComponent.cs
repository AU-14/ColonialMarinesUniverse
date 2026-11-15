// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// A hatch over a pit: open or shut, and whether it is invisible when shut (camouflage).
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HatchComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Open;

    [DataField, AutoNetworkedField]
    public bool InvisibleWhenClosed;

    [DataField]
    public TimeSpan CamouflageTime = TimeSpan.FromSeconds(10);
}

// What the appearance of a hatch shows.
[Serializable, NetSerializable]
public enum HatchVisuals : byte
{
    Open,
    Invisible,
}
