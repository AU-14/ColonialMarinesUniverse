using System;
using Content.Shared.Tag; // CMU14: restored from master (heavy-vehicle gating)
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vehicle;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleSmashableComponent : Component
{
    [DataField]
    public bool DeleteOnHit = true;

    [DataField]
    public double DamageOnHit = 1000;

    [DataField]
    public float SlowdownMultiplier = 0.5f;

    [DataField]
    public float SlowdownDuration = 0f;

    [DataField]
    public SoundSpecifier? SmashSound;

    [DataField] // CMU14: restored from master (ramming self-damage scaling)
    public float SelfDamageMultiplier = 1f;

    [DataField] // CMU14: restored from master (heavy-vehicle gating)
    public ProtoId<TagPrototype>? RequiredVehicleTag;

    [DataField]
    public bool RequiresDoorUnpowered;
}
