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

    [DataField]
    public float SelfDamageMultiplier = 1f;

    [DataField]
    public ProtoId<TagPrototype>? RequiredVehicleTag;
}
