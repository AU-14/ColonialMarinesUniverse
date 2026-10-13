// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Gunsmith;

/// <summary>
/// The gunsmith's bench. Sheets, ingots and powder are fed into it; the weapons, magazines and cartridges are made from what it holds.
/// Only someone who has the sapper's skill can use it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunsmithBenchComponent : Component
{
    /// <summary>The slot the weapon being worked on lies in (a magazine and cartridges are made to fit it).</summary>
    [DataField]
    public string WeaponSlot = "gunsmith_weapon";

    /// <summary>What the bench holds, items of each type of stack.</summary>
    [DataField]
    public Dictionary<string, int> Stock = new();

    /// <summary>The stack type of the powder that cartridges are filled with.</summary>
    [DataField]
    public string PowderStack = "RUMCSaltpeterPowder";

    /// <summary>How many items of one stack the bench takes in at the most, so that it is not a warehouse.</summary>
    [DataField]
    public int StockLimit = 400;

    /// <summary>How fast the bench works: the time of the work is multiplied by this (the tests make it small).</summary>
    [DataField]
    public double TimeScale = 1.0;

    /// <summary>The ids of the things made by the bench, for the prototypes of the entities.</summary>
    [DataField]
    public string FirearmPrototype = "RUMCCraftedFirearm";

    [DataField]
    public string MagazinePrototype = "RUMCCraftedMagazine";

}

/// <summary>A weapon made at the bench. Carries its design and what the bench worked out of it.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CraftedFirearmComponent : Component
{
    // ---------------------------------------------------------------- the look (sent to the clients)

    [DataField, AutoNetworkedField]
    public string Rear = "pistol_rear";

    [DataField, AutoNetworkedField]
    public string Mid = "carbine_mid";

    [DataField, AutoNetworkedField]
    public string Front = "carbine_front";

    [DataField, AutoNetworkedField]
    public bool Broken;

    // ---------------------------------------------------------------- what it is (stays on the server)

    [DataField]
    public FirearmDesign Design = new();

    /// <summary>The signature of the caliber the weapon takes.</summary>
    [DataField]
    public string CaliberKey = string.Empty;

    [DataField]
    public double Damage = 10;

    [DataField]
    public double MuzzleVelocity = 300;

    [DataField]
    public double PressureMpa;

    [DataField]
    public double BarrelMisesMpa;

    [DataField]
    public double BarrelYieldMpa = 350;

    [DataField]
    public double BarrelLimitTempC = 700;

    /// <summary>Whether the weapon can stand its first shot at all (the least safety factor of the parts that hold the pressure).</summary>
    [DataField]
    public double WeakestSafety = 2;

    [DataField]
    public double StockSafety = 2;

    [DataField]
    public double HingeSafety = 2;

    /// <summary>The shots that fatigue allows before something gives (infinity is written as a large number).</summary>
    [DataField]
    public double FatigueLifeShots = 1e9;

    [DataField]
    public double HeatPerShotK = 1;

    [DataField]
    public double Reliability = 0.9;

    [DataField]
    public double Quality = 1;

    /// <summary>Shots a second in the semi-automatic mode.</summary>
    [DataField]
    public double FireRate = 1;

    /// <summary>The fire modes: 1 semi-automatic, 2 burst, 4 full automatic, added up.</summary>
    [DataField]
    public int Modes = 1;

    [DataField]
    public double ScatterDegrees = 8;

    [DataField]
    public double RecoilFactor = 2;

    // ---------------------------------------------------------------- the wear

    [DataField]
    public double Shots;

    /// <summary>The temperature of the barrel, °C, and when it was last worked out.</summary>
    [DataField]
    public double TemperatureC = 20;

    [DataField]
    public TimeSpan TemperatureAt;
}

/// <summary>A magazine made at the bench for one caliber.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CraftedMagazineComponent : Component
{
    [DataField]
    public string CaliberKey = string.Empty;

    /// <summary>The caliber it was made for (the ammunition bench reads it).</summary>
    [DataField]
    public CaliberSpec Caliber = new();

    [DataField]
    public double Reliability = 1;

    /// <summary>The share of the stack positions the spring pushes well; the others may fail to feed.</summary>
    [DataField]
    public double ReliableShare = 1;
}

/// <summary>A cartridge made at the ammunition bench, for one caliber: its own load, and what its materials and its liquid do.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CraftedCartridgeComponent : Component
{
    [DataField]
    public string CaliberKey = string.Empty;

    /// <summary>The load: the real mass of the bullet, the powder and the charge. A weapon works out its own pressure from it.</summary>
    [DataField]
    public CaliberSpec Load = new();

    [DataField]
    public double DamageMultiplier = 1.0;

    [DataField]
    public double ArmorPierce;

    /// <summary>How well the casing keeps the gases in.</summary>
    [DataField]
    public double Seal = 1.0;

    [DataField]
    public double Jam;

    [DataField]
    public Dictionary<string, double> Extra = new();

    /// <summary>The liquid in the bullet: the reagent, how much goes into the target, whether it burns, the damage it does by itself.</summary>
    [DataField]
    public string PayloadReagent = string.Empty;

    [DataField]
    public double PayloadUnits;

    [DataField]
    public double InjectUnits;

    [DataField]
    public bool Ignite;

    [DataField]
    public string? PayloadDamageType;

    [DataField]
    public double PayloadDamagePerUnit;

    /// <summary>How many pellets a shell holds (1 for a cartridge with one bullet).</summary>
    [DataField]
    public int Pellets = 1;

    /// <summary>The damage of a bullet (a pellet) when the cartridge is fired from a weapon of the game, in the barrel the caliber is rated in.</summary>
    [DataField]
    public double RatedDamage = 10;

    /// <summary>What the damage of the game is fitted to (see AmmoMechanics.ScaledDamage); zero for a caliber of the player's own, whose damage follows the energy alone.</summary>
    [DataField]
    public double GameDamage;

    [DataField]
    public double GameMaxDamage;

    [DataField]
    public double BaselineEnergyJ;

    [DataField]
    public int NominalPellets = 1;

    /// <summary>What the cartridge is made of, as one string: cartridges stack with the ones that have the same.</summary>
    [DataField]
    public string Signature = string.Empty;

    [DataField]
    public AmmoBulletKind Kind;
}

/// <summary>Put on a projectile by the cartridge it came from, for the weapon to work out its damage from.</summary>
[RegisterComponent]
public sealed partial class CraftedProjectileComponent : Component
{
    public Entity<CraftedCartridgeComponent>? From;
}

/// <summary>Building at the bench takes time.</summary>
[Serializable, NetSerializable]
public sealed partial class GunsmithBuildDoAfterEvent : DoAfterEvent
{
    public enum Kind : byte
    {
        Firearm,
        Magazine,
    }

    [DataField]
    public Kind What;

    [DataField]
    public FirearmDesign? Firearm;

    [DataField]
    public MagazineDesign? Magazine;

    private GunsmithBuildDoAfterEvent()
    {
    }

    public GunsmithBuildDoAfterEvent(Kind what, FirearmDesign? firearm, MagazineDesign? magazine)
    {
        What = what;
        Firearm = firearm;
        Magazine = magazine;
    }

    public override DoAfterEvent Clone() => this;
}
