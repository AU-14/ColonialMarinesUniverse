using Content.Shared.Damage;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.ChemicalIrritants;

/// <summary>
/// Shared effect parameters for chemical irritants.
/// Held by both the injector (source) and the victim component,
/// so settings only need to be defined once per injector and are
/// copied to the victim on first contact.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class ChemicalIrritantProfile
{
    [DataField]
    public string IrritantType = "tear_gas";
    [DataField]
    public float DyloveneEfficiency = 1f;

    [DataField]
    public float EffectThreshold = 5f;

    [DataField]
    public float BlindThreshold = 20f;

    [DataField]
    public float SevereThreshold = 30f;

    [DataField]
    public TimeSpan BlurTime = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan BlindTime = TimeSpan.FromSeconds(1);

    [DataField]
    public float JitterChance = 0.30f;

    [DataField]
    public TimeSpan JitterTime = TimeSpan.FromSeconds(1);

    [DataField]
    public float DazeChance = 0.15f;

    [DataField]
    public TimeSpan DazeTime = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan StutterTime = TimeSpan.FromSeconds(2);

    [DataField]
    public float SlowChance = 0.10f;

    [DataField]
    public TimeSpan SlowTime = TimeSpan.FromSeconds(2);

    [DataField]
    public float SevereSlowChance = 0.25f;

    [DataField]
    public TimeSpan SevereSlowTime = TimeSpan.FromSeconds(4);

    [DataField]
    public DamageSpecifier IrritantDamage = new();
    
    [DataField]
    public float TripThreshold = 25f;

    [DataField]
    public float TripChance = 0.15f;

    [DataField]
    public TimeSpan TripStunTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan MinimumDelayBetweenTrips = TimeSpan.FromSeconds(5);
    
    [DataField]
    public List<string> ExposureMessages = new()
    {
        "Your eyes sting!",
        "Your lungs burn!",
        "You cough uncontrollably!",
        "Your skin burns!",
        "You gasp for air!"
    };
}