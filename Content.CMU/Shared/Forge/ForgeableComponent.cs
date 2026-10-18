// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Forge;

// A forgeable item: its temperature and how fast it heats, cools and glows.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ForgeableComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Temperature = 20f;

    [DataField]
    public float AmbientTemperature = 20f;

    [DataField]
    public float HeatGainPerSecond = 140f;

    [DataField]
    public float HeatLossPerSecond = 45f;

    [DataField]
    public float MaxTemperature = 1700f;

    [DataField]
    public float GlowTemperature = 500f;

    [DataField, AutoNetworkedField]
    public bool Active;

    [ViewVariables]
    public EntityUid? SeatedOn;

    [DataField]
    public float BurnTemperature = 100f;

    [DataField(required: true)]
    public DamageSpecifier BurnDamage = default!;

    [DataField]
    public Vector2 AnvilOffset = new(0.15625f, 0.21375f);
}

// What the appearance of a forgeable item shows.
[Serializable, NetSerializable]
public enum ForgeableVisuals : byte
{
    Active,

    Seat,
}

// Whether a forgeable item sits cold, hot or glowing in its seat.
[Serializable, NetSerializable]
public enum ForgeableSeatState : byte
{
    None,
    Furnace,
    Anvil,
}
