// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Armorsmith;

/// <summary>What a piece of armor is worn on: the body, the head, or the body as a ghillie suit (light, woven of plant fiber, never heavy).</summary>
public enum ArmorSlotKind : byte
{
    Body,
    Head,
    Ghillie,
}

/// <summary>
/// What a handmade piece of armor looks like: an armor or a helmet of the game, in grey, so that any color tints it cleanly. The slot decides
/// whether it is worn on the body or on the head, and so the area of the plate and the limits of the design.
/// </summary>
[Prototype("armorsmithLook")]
public sealed partial class ArmorsmithLookPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Localization id of the name.</summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>The entity that has the sprites.</summary>
    [DataField(required: true)]
    public string Entity = string.Empty;

    [DataField(required: true)]
    public ArmorSlotKind Slot;

    /// <summary>The picture of the armor, for the preview in the window: the rsi, under Textures (the state is "icon").</summary>
    [DataField(required: true)]
    public string Rsi = string.Empty;
}

/// <summary>
/// A metal as the plate of an armor. The numbers of strength, density and stiffness are those of the gunsmith's material (the same table); what
/// only armor needs is here: how far the metal stretches before it breaks, which is what makes a plate take the energy of a bullet.
/// </summary>
[Prototype("armorsmithPlate")]
public sealed partial class ArmorsmithPlatePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>The id of the gunsmith's material.</summary>
    [DataField(required: true)]
    public string Material = string.Empty;

    /// <summary>The strain at which the metal breaks (as a share, 0.12 is 12%).</summary>
    [DataField(required: true)]
    public double FailureStrain;
}

/// <summary>
/// A soft layer behind the plate (cloth, durathread): it takes a part of the blow that gets through the plate and covers what the plate leaves open.
/// </summary>
[Prototype("armorsmithBacking")]
public sealed partial class ArmorsmithBackingPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Localization id of the name.</summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>The type of the stack the bench takes in as this material.</summary>
    [DataField(required: true)]
    public string StackType = string.Empty;

    /// <summary>Strength of the thread, MPa.</summary>
    [DataField(required: true)]
    public double UltimateMpa;

    /// <summary>The strain at which the thread breaks (as a share).</summary>
    [DataField(required: true)]
    public double FailureStrain;

    [DataField(required: true)]
    public double DensityGcm3;

    [DataField(required: true)]
    public double YoungGpa;

    /// <summary>The mass of one layer of the weave, kg/m².</summary>
    [DataField(required: true)]
    public double LayerKgM2;

    /// <summary>How many square metres of one layer one item of the stack makes.</summary>
    [DataField]
    public double AreaPerItemM2 = 0.5;
}
