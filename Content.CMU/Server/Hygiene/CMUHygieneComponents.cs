using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using System.Collections.Generic;
using System;

namespace Content.Server.CMU14.Hygiene;

[RegisterComponent]
public sealed partial class CMUShowerComponent : Component
{
    [DataField]
    public bool Running;

    [DataField]
    public float WashSeconds = 8f;

    [DataField]
    public float Radius = 0.6f;

    [DataField]
    public float AutoShutoffSeconds = 60f;

    [DataField]
    public float SecondsRemaining = 60f;

    public float UntilTick;
    public readonly Dictionary<EntityUid, float> Progress = new();
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMULaundryComponent : Component
{
    [DataField]
    public float CycleSeconds = 12f;

    [DataField]
    public float CookoffSeconds = 7f;

    [DataField]
    public float AmmoCookoffSeconds = 3f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? AmmoCookoffAt;

    [DataField]
    public float AmmoFragmentInterval = 0.5f;

    [DataField]
    public float AmmoFragmentSpeed = 28f;

    [DataField]
    public float AmmoConeDegrees = 40f;

    [DataField]
    public EntProtoId AmmoPopEffect = "CMULaundryAmmoPop";

    [DataField]
    public EntProtoId AmmoFragment = "CMULaundryAmmoFragment";

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? CookoffAt;

    [DataField]
    public EntityUid? CycleUser;

    [DataField]
    public float BreakdownDamage = 100f;

    [DataField]
    public bool Broken;

    [DataField]
    public SoundSpecifier CompletionSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? FinishAt;
}

[RegisterComponent]
public sealed partial class CMULaundryHeatedGrenadeComponent : Component;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMULaundryPopcornComponent : Component
{
    [DataField] public int RoundsRemaining;
    [DataField] public float FragmentInterval = 0.5f;
    [DataField] public float FragmentSpeed = 28f;
    [DataField] public float ConeDegrees = 40f;
    [DataField] public float MinPopInterval = 0.07f;
    [DataField] public float MaxPopInterval = 0.14f;
    [DataField] public EntProtoId PopEffect = "CMULaundryAmmoPop";
    [DataField] public EntProtoId Fragment = "CMULaundryAmmoFragment";
    [DataField] public EntityUid? User;
    [DataField] public EntityUid? Washer;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPopAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextFragmentAt;
}

[RegisterComponent]
public sealed partial class CMUActiveShowerComponent : Component;

[RegisterComponent]
public sealed partial class CMUActiveLaundryComponent : Component;
