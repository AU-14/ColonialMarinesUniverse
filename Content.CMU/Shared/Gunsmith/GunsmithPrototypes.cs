// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Gunsmith;

/// <summary>
/// A material the gunsmith's bench can work. The numbers are the real ones for the metal of the name (a little rounded); the player has to take
/// them from the table of the bench and put them into the formulas. The bench uses them for the same formulas to see if the weapon holds.
/// </summary>
[Prototype("gunsmithMaterial")]
public sealed partial class GunsmithMaterialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Localization id of the name.</summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>The type of the stack (ingots, sheets, planks) that the bench takes in as this material.</summary>
    [DataField(required: true)]
    public string StackType = string.Empty;

    /// <summary>The volume of metal one item of the stack gives, in cubic centimetres.</summary>
    [DataField]
    public double VolumeCm3 = 120;

    /// <summary>Yield strength σт, MPa.</summary>
    [DataField(required: true)]
    public double YieldMpa;

    /// <summary>Ultimate tensile strength σв, MPa.</summary>
    [DataField(required: true)]
    public double UltimateMpa;

    /// <summary>Endurance limit σ-1 (fatigue, fully reversed), MPa.</summary>
    [DataField(required: true)]
    public double EnduranceMpa;

    /// <summary>Young's modulus E, GPa.</summary>
    [DataField(required: true)]
    public double YoungGpa;

    /// <summary>Shear modulus G, GPa.</summary>
    [DataField(required: true)]
    public double ShearGpa;

    /// <summary>Density ρ, g/cm³.</summary>
    [DataField(required: true)]
    public double DensityGcm3;

    /// <summary>Specific heat c, J/(g·K).</summary>
    [DataField(required: true)]
    public double SpecificHeat;

    /// <summary>The temperature (°C) at which the metal has lost all its strength for practical purposes.</summary>
    [DataField(required: true)]
    public double LimitTempC;

    /// <summary>How easily it is machined, 0.3 (a pain) to 1.3 (butter). The fine grades of work need a figure of at least 0.7.</summary>
    [DataField]
    public double Machinability = 1.0;

    /// <summary>Whether the material can be chosen for the parts of a weapon (lead is only the bullets).</summary>
    [DataField]
    public bool Structural = true;

    /// <summary>Colour of the part in the sprite.</summary>
    [DataField]
    public string Color = "#ffffff";
}

/// <summary>A powder for the cartridges: the numbers for the formulas of the interior ballistics.</summary>
[Prototype("gunsmithPowder")]
public sealed partial class GunsmithPowderPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>Specific force of the powder f, MPa·cm³/g.</summary>
    [DataField(required: true)]
    public double Force;

    /// <summary>Covolume α, cm³/g.</summary>
    [DataField(required: true)]
    public double Covolume;

    /// <summary>Heat of combustion Q, J/g.</summary>
    [DataField(required: true)]
    public double Energy;

    /// <summary>The share of that energy that pushes the bullet, in the best barrel: η.</summary>
    [DataField(required: true)]
    public double Efficiency;

    /// <summary>How many items of powder (saltpeter) a gram of this takes to make.</summary>
    [DataField]
    public double CostPerGram = 1;
}

/// <summary>An action of the weapon: how the cartridge is fed and the shot is locked.</summary>
[Prototype("gunsmithAction")]
public sealed partial class GunsmithActionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>Points of complexity of the mechanism.</summary>
    [DataField(required: true)]
    public double Complexity;

    /// <summary>Time of one cycle of the action at best, seconds (a heavy bolt makes it longer).</summary>
    [DataField(required: true)]
    public double CycleSeconds;

    /// <summary>The fire modes: 1 semi-auto, 2 burst, 4 full auto (added up).</summary>
    [DataField]
    public int Modes = 1;

    /// <summary>The mass of the working parts that are not designed by the player (trigger, springs, pins...), grams.</summary>
    [DataField]
    public double FixedMassG = 300;

    /// <summary>The length of the bolt, mm.</summary>
    [DataField]
    public double BoltLengthMm = 60;

    /// <summary>The state of the sprite of the middle part.</summary>
    [DataField(required: true)]
    public string MidSprite = string.Empty;

    /// <summary>The donor of the in-hand sprites.</summary>
    [DataField(required: true)]
    public string HandSprite = string.Empty;
}

/// <summary>A common cartridge, as a starting point for a caliber: the same numbers the player can set freely.</summary>
[Prototype("gunsmithCaliber")]
public sealed partial class GunsmithCaliberPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public double BoreMm;

    [DataField(required: true)]
    public double CaseDiameterMm;

    [DataField(required: true)]
    public double CaseLengthMm;

    [DataField(required: true)]
    public double SeatDepthMm;

    [DataField(required: true)]
    public double BulletMassG;

    [DataField(required: true)]
    public string Powder = string.Empty;

    [DataField(required: true)]
    public double ChargeG;

    /// <summary>The barrel (mm) the cartridge is rated in when it is fired from a weapon of the game; 0 for twenty calibers.</summary>
    [DataField]
    public double BarrelMm;

    /// <summary>A shotgun shell: the bullet is a load of pellets, as many as the cartridge is made with.</summary>
    [DataField]
    public bool Shell;

    /// <summary>
    /// The damage of one bullet (one pellet) of the plain cartridge of this caliber in the game, which the nominal load of the preset is fitted to; 0 for none
    /// (the damage then follows the energy alone).
    /// </summary>
    [DataField]
    public double GameDamage;

    /// <summary>The damage of the strongest bullet of this caliber in the game: nothing made at the bench goes past twice of it.</summary>
    [DataField]
    public double GameMaxDamage;

    /// <summary>The pellets the plain shell of the game of this caliber has (1 for a cartridge with one bullet).</summary>
    [DataField]
    public int NominalPellets = 1;

    /// <summary>The caliber of the heavy shotgun shells: only for it can heavy buckshot be made.</summary>
    [DataField]
    public bool Heavy;

    /// <summary>The tags that the cartridges of the game of this caliber have: a cartridge made for the caliber gets them, and fits the magazines and weapons that ask for them.</summary>
    [DataField]
    public List<string> CartridgeTags = new();

    /// <summary>The tags of the magazines of the game that the weapons of this caliber take: a magazine made for the caliber gets them.</summary>
    [DataField]
    public List<string> MagazineTags = new();
}

/// <summary>
/// A class of weapons (a pistol, a submachine gun, a rifle, a machine gun, a sniper rifle, a shotgun, a heavy weapon). What a weapon of the class can be:
/// a caliber too big for the class cannot be put into it, nor a barrel of the wrong length.
/// </summary>
[Prototype("gunsmithClass")]
public sealed partial class GunsmithClassPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public double MinBoreMm;

    [DataField]
    public double MaxBoreMm = 100;

    /// <summary>The most powder the cartridge of a weapon of this class may have, g.</summary>
    [DataField]
    public double MaxChargeG = 100;

    [DataField]
    public double MaxCaseLengthMm = 200;

    [DataField]
    public double MinBarrelMm = 40;

    [DataField]
    public double MaxBarrelMm = 1200;

    /// <summary>The stocks a weapon of this class may have; empty for all.</summary>
    [DataField]
    public List<StockKind> Stocks = new();
}

/// <summary>What a handmade weapon looks like: the picture of a weapon of the game. Chosen apart from the caliber.</summary>
[Prototype("gunsmithLook")]
public sealed partial class GunsmithLookPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>The entity that has the sprites.</summary>
    [DataField(required: true)]
    public string Entity = string.Empty;

    /// <summary>The class of the weapon the look is of (a pistol, a rifle ...): a weapon with the look is of that class.</summary>
    [DataField]
    public string Class = "GunsmithClassRifle";

    /// <summary>The picture of the weapon, for the preview in the window: the rsi (under Textures) and the state.</summary>
    [DataField(required: true)]
    public string Rsi = string.Empty;

    [DataField]
    public string State = "base";
}
