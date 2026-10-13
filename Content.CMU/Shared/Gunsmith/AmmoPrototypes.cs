// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Gunsmith;

/// <summary>
/// The parts of a cartridge a material can be put into: the core of the bullet, its jacket, its tip and the casing. (After the parts of the
/// bullets of the mod "CE Custom Ammo", which this bench is based on.)
/// </summary>
[Serializable, NetSerializable]
public enum AmmoPart : byte
{
    Core,
    Jacket,
    Tip,
    Casing,
}

/// <summary>What a material does to a cartridge when it is used for one of its parts.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class AmmoPartEffect
{
    /// <summary>The damage of the bullet is multiplied by this.</summary>
    [DataField]
    public double Damage = 1.0;

    /// <summary>Armor penetration added.</summary>
    [DataField]
    public double ArmorPierce = 0.0;

    /// <summary>
    /// How well the casing keeps the gases in: the energy the powder gives the bullet is multiplied by this (only for the casing; 1 is
    /// perfect).
    /// </summary>
    [DataField]
    public double Seal = 1.0;

    /// <summary>Extra damage of another type the bullet does, by type (flat, as a share of the damage of the bullet).</summary>
    [DataField]
    public Dictionary<string, double> Extra = new();

    /// <summary>The chance that a cartridge fails to cycle, added to that of the weapon.</summary>
    [DataField]
    public double Jam = 0.0;
}

/// <summary>
/// A material for the parts of a cartridge. The material itself (its density, its stack, its volume) is a gunsmith's material; this says which
/// parts it can be used for and what it does there.
/// </summary>
[Prototype("ammoMaterial")]
public sealed partial class AmmoMaterialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>The id of the gunsmith's material: its density and its stack.</summary>
    [DataField(required: true)]
    public string Material = string.Empty;

    [DataField(required: true)]
    public Dictionary<AmmoPart, AmmoPartEffect> Parts = new();

    /// <summary>Localization id of what is special about the material.</summary>
    [DataField]
    public string? Note;
}

/// <summary>What a liquid does in a bullet: how it is told apart by the bench and what it does to the target.</summary>
[Prototype("ammoPayload")]
public sealed partial class AmmoPayloadPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>The reagent.</summary>
    [DataField(required: true)]
    public string Reagent = string.Empty;

    /// <summary>The bullet sets the target on fire (a burning liquid).</summary>
    [DataField]
    public bool Ignite;

    /// <summary>The liquid is injected into the target.</summary>
    [DataField]
    public bool Inject = true;

    /// <summary>The type of the damage the liquid does by itself, and how much for each unit of it carried.</summary>
    [DataField]
    public string? DamageType;

    [DataField]
    public double DamagePerUnit;

    /// <summary>How much of the liquid is injected on a hit, as a share of what the bullet carries.</summary>
    [DataField]
    public double InjectShare = 1.0;

    /// <summary>Localization id of the note about this liquid.</summary>
    [DataField]
    public string? Note;
}
