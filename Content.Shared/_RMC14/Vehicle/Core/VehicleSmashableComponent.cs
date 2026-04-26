using System;
using Content.Shared.Tag;
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

    [DataField]
    public bool RequiresDoorUnpowered;

    /// <summary>
    /// Multiplier applied to the smashing vehicle's own wheel/hull damage when it plows
    /// through this entity. 1 = full damage, 0 = no self-damage. Lets soft targets like
    /// resin walls be cheap to ram while concrete/windows keep their normal cost.
    /// </summary>
    [DataField]
    public float SelfDamageMultiplier = 1f;

    /// <summary>
    /// If set, only vehicles tagged with this value are allowed to smash through; everyone
    /// else bumps into it like a hard wall. Use for gating resin walls to heavy vehicles.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>? RequiredVehicleTag;
}
