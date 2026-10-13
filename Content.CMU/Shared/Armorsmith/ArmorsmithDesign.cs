// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Gunsmith;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Armorsmith;

/// <summary>Everything the player decides about a piece of armor.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class ArmorDesign
{
    /// <summary>The id of the look: an armor or a helmet of the game, in grey. It decides whether the piece is worn on the body or on the head.</summary>
    [DataField]
    public string Look = string.Empty;

    /// <summary>The id of the gunsmith's material the plate is made of.</summary>
    [DataField]
    public string PlateMaterial = "GunsmithSteel";

    [DataField]
    public double PlateThicknessMm = 4;

    /// <summary>The share of the piece the plate covers, percent (the rest is soft backing alone).</summary>
    [DataField]
    public double CoveragePercent = 100;

    /// <summary>The id of the soft layer behind the plate; empty for none.</summary>
    [DataField]
    public string BackingMaterial = string.Empty;

    [DataField]
    public int BackingLayers;

    [DataField]
    public ToleranceGrade Grade = ToleranceGrade.Standard;

    /// <summary>A helmet that covers the face too: it protects better, and its visor darkens the sight like a welding mask. Only for a helmet.</summary>
    [DataField]
    public bool ClosedHelmet;

    /// <summary>The color the grey armor is painted.</summary>
    [DataField]
    public Color Color = Color.White;

    /// <summary>The name the maker gives the piece; empty for the usual one.</summary>
    [DataField]
    public string ItemName = string.Empty;

    /// <summary>The description the maker writes; empty for the usual one.</summary>
    [DataField]
    public string ItemDescription = string.Empty;

    public const int MaxNameLength = 40;
    public const int MaxDescriptionLength = 300;

    public ArmorDesign Clone() => new()
    {
        Look = Look,
        PlateMaterial = PlateMaterial,
        PlateThicknessMm = PlateThicknessMm,
        CoveragePercent = CoveragePercent,
        BackingMaterial = BackingMaterial,
        BackingLayers = BackingLayers,
        Grade = Grade,
        ClosedHelmet = ClosedHelmet,
        Color = Color,
        ItemName = ItemName,
        ItemDescription = ItemDescription,
    };
}

/// <summary>What the bench worked out for a piece of armor. The window never shows the protection: the player is to work it out with the formulas.</summary>
public sealed class ArmorEvaluation
{
    public ArmorSlotKind Slot;
    public double AreaM2;
    public double Coverage;

    // the layers
    public double PlateKgM2;
    public double BackingKgM2;
    public double PlateMeritVelocity;
    public double BackingMeritVelocity;
    public double PlateLimitVelocity;
    public double BackingLimitVelocity;

    /// <summary>The speed of the reference bullet that the whole armor stops, m/s.</summary>
    public double LimitVelocity;

    // what the game gets
    public int Melee;
    public int Bullet;
    public int Bio;
    public int Explosion;
    public double MassKg;

    /// <summary>Whether it is a closed helmet.</summary>
    public bool Closed;

    /// <summary>The share of the speed the armor takes, and the share the wearer keeps (a helmet takes none).</summary>
    public double Slowdown;
    public double SpeedShare = 1.0;

    /// <summary>The tier of speed of the armor (a body armor only): light, medium, heavy.</summary>
    public string SpeedTier = string.Empty;

    public double Quality;
    public double Workmanship;

    // building
    public double Complexity;
    public double BuildSeconds;
    public Dictionary<string, double> MaterialUnits = new();
}

/// <summary>Where the evaluator finds the looks, the plates and the backings (the prototypes; in a test, whatever the test gives).</summary>
public interface IArmorsmithData
{
    ArmorsmithLookPrototype? Look(string id);
    GunsmithMaterialPrototype? Material(string id);
    ArmorsmithPlatePrototype? Plate(string materialId);
    ArmorsmithBackingPrototype? Backing(string id);
}
