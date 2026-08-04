using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Alchemist;

[Serializable, NetSerializable]
public enum AlchemistChemical : byte
{
    None,
    Sagunine,
    Cholinine,
    Noctine,
}

[Serializable, NetSerializable]
public enum AlchemistMixture : byte
{
    Sagunine,
    Cholinine,
    Noctine,
    Pyrinine,
    Vapinine,
    Crynine,
    Xenosterine,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoAlchemistSystem))]
public sealed partial class XenoAlchemistComponent : Component
{
    [DataField, AutoNetworkedField]
    public AlchemistChemical SelectedChemical = AlchemistChemical.Sagunine;

    [DataField, AutoNetworkedField]
    public AlchemistChemical ProducingChemical;

    [DataField, AutoNetworkedField]
    public bool Producing;

    [DataField, AutoNetworkedField]
    public int Sagunine;

    [DataField, AutoNetworkedField]
    public int Cholinine;

    [DataField, AutoNetworkedField]
    public int Noctine;

    [DataField, AutoNetworkedField]
    public int MaxStockpile = 20;

    [DataField, AutoNetworkedField]
    public int ProduceAmount = 4;

    [DataField, AutoNetworkedField]
    public int SlashGenerateAmount = 2;

    [DataField, AutoNetworkedField]
    public TimeSpan ProduceDelay = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan TailInjectionCooldown = TimeSpan.FromSeconds(8);

    [DataField, AutoNetworkedField]
    public DamageSpecifier TailInjectionDamage = new()
    {
        DamageDict = { ["Piercing"] = 20 },
    };

    [DataField, AutoNetworkedField]
    public TimeSpan NoctineDazeTime = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan PyrinineDazeTime = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan CrynineSlowTime = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public SoundSpecifier TailInjectionSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_tail_attack.ogg");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoAlchemistSystem))]
public sealed partial class XenoTemporaryStatModifierComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public int Armor;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier MeleeDamage = new();
}
