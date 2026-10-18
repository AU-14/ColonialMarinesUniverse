// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Power;

// The door of a power cabinet. Shut, it keeps the wiring away from the hands and lets the screen of the cabinet be used; open, the wiring can be
// reached (the panel can be unscrewed) and the live terminals can shock whoever touches them, but the screen is out of reach.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ServiceDoorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Open;
}

// What the appearance of a service door shows.
[Serializable, NetSerializable]
public enum ServiceDoorVisuals : byte
{
    Open,
}
