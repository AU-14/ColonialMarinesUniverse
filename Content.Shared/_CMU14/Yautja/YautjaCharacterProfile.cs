using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Enums;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[Serializable, NetSerializable]
public enum YautjaGearMaterial : byte
{
    Ebony,
    Bronze,
    Silver,
    Crimson,
    Bone,
}

[Serializable, NetSerializable]
public enum YautjaBracerMaterial : byte
{
    Retro,
    Ebony,
    Silver,
    Bronze,
    Crimson,
    Bone,
    Dragon,
    Swamp,
    Enforcer,
    Collector,
}

[Serializable, NetSerializable]
public enum YautjaTranslatorType : byte
{
    Modern,
    Retro,
    Combo,
}

[Serializable, NetSerializable]
public enum YautjaInvisibilitySound : byte
{
    Modern,
    Retro,
}

[Serializable, NetSerializable]
public enum YautjaLegacySet : byte
{
    None,
    Dragon,
    Swamp,
    Enforcer,
    Collector,
}

[Serializable, NetSerializable]
public enum YautjaUniqueSet : byte
{
    None,
    Anubys,
    Cleopatra,
    Plated,
    Ronin,
}

[Serializable, NetSerializable]
public enum YautjaSkinColor : byte
{
    Tan,
    Green,
    Purple,
    Blue,
    Red,
    Black,
}

[Serializable, NetSerializable]
public enum YautjaEyeColor : byte
{
    Gold,
    Amber,
    Copper,
    Red,
    Jade,
    Slate,
    Black,
}

[Serializable, NetSerializable]
public enum YautjaCapeStyle : byte
{
    Full,
    Ceremonial,
    Third,
    Half,
    Quarter,
    Poncho,
    Damaged,
}

[Serializable, NetSerializable]
public enum YautjaQuillStyle : byte
{
    Standard,
    ShortThick,
    StraightThin,
    LongTied,
    ShortThin,
    LongCurved,
    LongStraight,
    LongWide,
    ShortWide,
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class YautjaCharacterProfile
{
    public const int MaxFlavorTextLength = 512;

    private const int DefaultArmorStyle = 1;
    private const int DefaultMaskStyle = 1;
    private const int DefaultGreavesStyle = 1;
    private const string QuillMarkingPrefix = "CMUYautjaDreadlocks";
    private static readonly Color DefaultCapeColor = C(0x65, 0x43, 0x21);

    public static readonly YautjaGearMaterial[] MaterialOrder =
    [
        YautjaGearMaterial.Ebony,
        YautjaGearMaterial.Silver,
        YautjaGearMaterial.Bronze,
        YautjaGearMaterial.Crimson,
        YautjaGearMaterial.Bone,
    ];

    public static readonly YautjaBracerMaterial[] BracerMaterialOrder =
    [
        YautjaBracerMaterial.Retro,
        YautjaBracerMaterial.Ebony,
        YautjaBracerMaterial.Silver,
        YautjaBracerMaterial.Bronze,
        YautjaBracerMaterial.Crimson,
        YautjaBracerMaterial.Bone,
        YautjaBracerMaterial.Dragon,
        YautjaBracerMaterial.Swamp,
        YautjaBracerMaterial.Enforcer,
        YautjaBracerMaterial.Collector,
    ];

    public static readonly YautjaBracerMaterial[] CasterMaterialOrder =
    [
        YautjaBracerMaterial.Retro,
        YautjaBracerMaterial.Ebony,
        YautjaBracerMaterial.Silver,
        YautjaBracerMaterial.Bronze,
        YautjaBracerMaterial.Crimson,
        YautjaBracerMaterial.Bone,
    ];

    public static readonly YautjaTranslatorType[] TranslatorTypeOrder =
    [
        YautjaTranslatorType.Modern,
        YautjaTranslatorType.Retro,
        YautjaTranslatorType.Combo,
    ];

    public static readonly YautjaInvisibilitySound[] InvisibilitySoundOrder =
    [
        YautjaInvisibilitySound.Modern,
        YautjaInvisibilitySound.Retro,
    ];

    public static readonly YautjaLegacySet[] LegacyOrder =
    [
        YautjaLegacySet.None,
        YautjaLegacySet.Dragon,
        YautjaLegacySet.Swamp,
        YautjaLegacySet.Enforcer,
        YautjaLegacySet.Collector,
    ];

    public static readonly YautjaUniqueSet[] UniqueOrder =
    [
        YautjaUniqueSet.None,
        YautjaUniqueSet.Anubys,
        YautjaUniqueSet.Cleopatra,
        YautjaUniqueSet.Plated,
        YautjaUniqueSet.Ronin,
    ];

    public static readonly YautjaSkinColor[] SkinColorOrder =
    [
        YautjaSkinColor.Green,
        YautjaSkinColor.Tan,
        YautjaSkinColor.Purple,
        YautjaSkinColor.Blue,
        YautjaSkinColor.Red,
        YautjaSkinColor.Black,
    ];

    public static readonly YautjaQuillStyle[] QuillStyleOrder =
    [
        YautjaQuillStyle.Standard,
        YautjaQuillStyle.ShortThick,
        YautjaQuillStyle.StraightThin,
        YautjaQuillStyle.LongTied,
        YautjaQuillStyle.ShortThin,
        YautjaQuillStyle.LongCurved,
        YautjaQuillStyle.LongStraight,
        YautjaQuillStyle.LongWide,
        YautjaQuillStyle.ShortWide,
    ];

    public static readonly YautjaEyeColor[] EyeColorOrder =
    [
        YautjaEyeColor.Black,
        YautjaEyeColor.Gold,
        YautjaEyeColor.Amber,
        YautjaEyeColor.Copper,
        YautjaEyeColor.Red,
        YautjaEyeColor.Jade,
        YautjaEyeColor.Slate,
    ];

    public static readonly YautjaCapeStyle[] CapeStyleOrder =
    [
        YautjaCapeStyle.Full,
        YautjaCapeStyle.Ceremonial,
        YautjaCapeStyle.Third,
        YautjaCapeStyle.Half,
        YautjaCapeStyle.Quarter,
        YautjaCapeStyle.Poncho,
        YautjaCapeStyle.Damaged,
    ];

    public static readonly Color[] SkinToneColors =
    [
        C(166, 153, 100),
        C(120, 125, 101),
        C(136, 119, 144),
        C(125, 139, 150),
        C(105, 57, 59),
        C(72, 69, 77),
    ];

    public static readonly Color[] EyeColors =
    [
        C(196, 158, 65),
        C(181, 111, 48),
        C(155, 83, 52),
        C(124, 42, 40),
        C(76, 124, 92),
        C(111, 128, 140),
        C(20, 18, 16),
    ];

    public static YautjaCharacterProfile Default => new();

    [DataField]
    public string Name { get; private set; } = "Неизвестно";

    [DataField]
    public int Age { get; private set; } = 100;

    [DataField]
    public Sex Sex { get; private set; } = Sex.Male;

    [DataField]
    public Gender Gender { get; private set; } = Gender.Male;

    [DataField]
    public HumanoidCharacterAppearance Appearance { get; private set; } = BuildDefaultAppearance();

    [DataField]
    public YautjaGearMaterial ArmorMaterial { get; private set; } = YautjaGearMaterial.Ebony;

    [DataField]
    public int ArmorStyle { get; private set; } = DefaultArmorStyle;

    [DataField]
    public YautjaGearMaterial MaskMaterial { get; private set; } = YautjaGearMaterial.Ebony;

    [DataField]
    public int MaskStyle { get; private set; } = DefaultMaskStyle;

    [DataField]
    public int MaskAccessoryStyle { get; private set; }

    [DataField]
    public YautjaGearMaterial GreavesMaterial { get; private set; } = YautjaGearMaterial.Ebony;

    [DataField]
    public int GreavesStyle { get; private set; } = DefaultGreavesStyle;

    [DataField]
    public YautjaBracerMaterial BracerMaterial { get; private set; } = YautjaBracerMaterial.Ebony;

    [DataField]
    public YautjaBracerMaterial CasterMaterial { get; private set; } = YautjaBracerMaterial.Ebony;

    [DataField]
    public YautjaBracerOwnerRank OwnerRank { get; private set; } = YautjaBracerOwnerRank.Unblooded;

    [DataField]
    public YautjaCapeStyle CapeStyle { get; private set; } = YautjaCapeStyle.Full;

    [DataField]
    public Color CapeColor { get; private set; } = DefaultCapeColor;

    [DataField]
    public YautjaTranslatorType TranslatorType { get; private set; } = YautjaTranslatorType.Modern;

    [DataField]
    public YautjaInvisibilitySound InvisibilitySound { get; private set; } = YautjaInvisibilitySound.Modern;

    [DataField]
    public YautjaLegacySet Legacy { get; private set; } = YautjaLegacySet.None;

    [DataField]
    public YautjaUniqueSet Unique { get; private set; } = YautjaUniqueSet.None;

    [DataField]
    public string FlavorText { get; private set; } = string.Empty;

    public YautjaQuillStyle QuillStyle => GetQuillStyle(Appearance);
    public string QuillMarkingId => GetQuillMarkingId(QuillStyle);
    public YautjaSkinColor SkinColor => GetClosestSkinColor(Appearance.SkinColor);
    public YautjaEyeColor EyeColor => GetClosestEyeColor(Appearance.EyeColor);

    public string ArmorPrototype => Legacy != YautjaLegacySet.None
        ? $"CMUYautjaArmorLegacy{Legacy}"
        : Unique != YautjaUniqueSet.None
            ? $"CMUYautjaArmorUnique{Unique}"
            : ClanPrototype("CMUYautjaClanArmor", ArmorMaterial, Clamp(ArmorStyle, 1, 8));

    public string MaskPrototype => Legacy != YautjaLegacySet.None
        ? $"CMUYautjaMaskLegacy{Legacy}"
        : Unique != YautjaUniqueSet.None
            ? $"CMUYautjaMaskUnique{Unique}"
            : $"CMUYautjaMaskPred{Clamp(MaskStyle, 1, 20):00}{MaterialSuffix(MaskMaterial)}";

    public string? MaskAccessoryPrototype => MaskAccessoryStyle == 0
        ? null
        : $"CMUYautjaMaskAccessory{Clamp(MaskAccessoryStyle, 1, 3):00}{MaterialSuffix(MaskMaterial)}";

    public string GreavesPrototype => Legacy != YautjaLegacySet.None
        ? $"CMUYautjaGreavesLegacy{Legacy}"
        : Unique != YautjaUniqueSet.None
            ? $"CMUYautjaGreavesUnique{Unique}"
            : ClanPrototype("CMUYautjaClanGreaves", GreavesMaterial, Clamp(GreavesStyle, 1, 4));

    public string BracerPrototype => Legacy != YautjaLegacySet.None
        ? $"CMUYautjaBracerLegacy{Legacy}"
        : BracerMaterial switch
        {
            YautjaBracerMaterial.Retro => "CMUYautjaBracerRetro",
            YautjaBracerMaterial.Silver => "CMUYautjaBracerSilver",
            YautjaBracerMaterial.Bronze => "CMUYautjaBracerBronze",
            YautjaBracerMaterial.Crimson => "CMUYautjaBracerCrimson",
            YautjaBracerMaterial.Bone => "CMUYautjaBracerBone",
            YautjaBracerMaterial.Dragon => "CMUYautjaBracerLegacyDragon",
            YautjaBracerMaterial.Swamp => "CMUYautjaBracerLegacySwamp",
            YautjaBracerMaterial.Enforcer => "CMUYautjaBracerLegacyEnforcer",
            YautjaBracerMaterial.Collector => "CMUYautjaBracerLegacyCollector",
            _ => "CMUYautjaBracerEbony",
        };

    public string CasterPrototype => CasterMaterial switch
    {
        YautjaBracerMaterial.Retro => "CMUYautjaPlasmaCasterRetro",
        YautjaBracerMaterial.Silver => "CMUYautjaPlasmaCasterSilver",
        YautjaBracerMaterial.Bronze => "CMUYautjaPlasmaCasterBronze",
        YautjaBracerMaterial.Crimson => "CMUYautjaPlasmaCasterCrimson",
        YautjaBracerMaterial.Bone => "CMUYautjaPlasmaCasterBone",
        _ => "CMUYautjaPlasmaCasterEbony",
    };

    public string CapePrototype => CapeStyle switch
    {
        YautjaCapeStyle.Ceremonial => "CMUYautjaCapeCeremonial",
        YautjaCapeStyle.Third => "CMUYautjaCapeThird",
        YautjaCapeStyle.Half => "CMUYautjaCapeHalf",
        YautjaCapeStyle.Quarter => "CMUYautjaCapeQuarter",
        YautjaCapeStyle.Poncho => "CMUYautjaCapePoncho",
        YautjaCapeStyle.Damaged => "CMUYautjaCapeDamaged",
        _ => "CMUYautjaCapeFull",
    };

    public string ArmorDisplayName => Legacy != YautjaLegacySet.None
        ? $"{GetLegacyDisplayName(Legacy)} legacy armor"
        : Unique != YautjaUniqueSet.None
            ? $"{GetUniqueDisplayName(Unique)} unique armor"
            : GetArmorStyleDisplayName(ArmorMaterial, ArmorStyle);

    public string MaskDisplayName => Legacy != YautjaLegacySet.None
        ? $"{GetLegacyDisplayName(Legacy)} legacy bio-mask"
        : Unique != YautjaUniqueSet.None
            ? $"{GetUniqueDisplayName(Unique)} unique bio-mask"
            : GetMaskStyleDisplayName(MaskMaterial, MaskStyle);

    public string GreavesDisplayName => Legacy != YautjaLegacySet.None
        ? $"{GetLegacyDisplayName(Legacy)} legacy greaves"
        : Unique != YautjaUniqueSet.None
            ? $"{GetUniqueDisplayName(Unique)} unique greaves"
            : GetGreavesStyleDisplayName(GreavesMaterial, GreavesStyle);

    public string BracerDisplayName => Legacy != YautjaLegacySet.None
        ? $"{GetLegacyDisplayName(Legacy)} legacy bracers"
        : GetBracerDisplayName(BracerMaterial);

    public YautjaCharacterProfile()
    {
    }

    private YautjaCharacterProfile(YautjaCharacterProfile other)
    {
        Name = other.Name;
        Age = other.Age;
        Sex = Sex.Male;
        Gender = Gender.Male;
        Appearance = SanitizeAppearance(other.Appearance);
        ArmorMaterial = other.ArmorMaterial;
        ArmorStyle = other.ArmorStyle;
        MaskMaterial = other.MaskMaterial;
        MaskStyle = other.MaskStyle;
        MaskAccessoryStyle = other.MaskAccessoryStyle;
        GreavesMaterial = other.GreavesMaterial;
        GreavesStyle = other.GreavesStyle;
        BracerMaterial = other.BracerMaterial;
        CasterMaterial = other.CasterMaterial;
        OwnerRank = other.OwnerRank;
        CapeStyle = other.CapeStyle;
        CapeColor = other.CapeColor;
        TranslatorType = other.TranslatorType;
        InvisibilitySound = other.InvisibilitySound;
        Legacy = other.Legacy;
        Unique = other.Unique;
        FlavorText = other.FlavorText;
    }

    public YautjaCharacterProfile Clone()
    {
        return new YautjaCharacterProfile(this);
    }

    public YautjaCharacterProfile WithName(string name)
    {
        return new(this) { Name = string.IsNullOrWhiteSpace(name) ? Default.Name : name.Trim() };
    }

    public YautjaCharacterProfile WithAge(int age)
    {
        return new(this) { Age = Clamp(age, 100, 1200) };
    }

    public YautjaCharacterProfile WithSex(Sex sex)
    {
        return new(this) { Sex = Sex.Male };
    }

    public YautjaCharacterProfile WithGender(Gender gender)
    {
        return new(this) { Gender = Gender.Male };
    }

    public YautjaCharacterProfile WithAppearance(HumanoidCharacterAppearance appearance)
    {
        return new(this) { Appearance = SanitizeAppearance(appearance) };
    }

    public YautjaCharacterProfile WithSkinColor(YautjaSkinColor skinColor)
    {
        var color = GetSkinColorColor(skinColor);
        return WithAppearance(Appearance.WithSkinColor(color).WithHairColor(color));
    }

    public YautjaCharacterProfile WithEyeColor(YautjaEyeColor eyeColor)
    {
        return WithAppearance(Appearance.WithEyeColor(GetEyeColorColor(eyeColor)));
    }

    public YautjaCharacterProfile WithQuillStyle(YautjaQuillStyle style)
    {
        return WithAppearance(ApplyQuillStyle(Appearance, style));
    }

    public YautjaCharacterProfile WithArmor(YautjaGearMaterial material, int style)
    {
        return new(this)
        {
            ArmorMaterial = material,
            ArmorStyle = Clamp(style, 1, 8),
        };
    }

    public YautjaCharacterProfile WithMask(YautjaGearMaterial material, int style)
    {
        return new(this)
        {
            MaskMaterial = material,
            MaskStyle = Clamp(style, 1, 20),
        };
    }

    public YautjaCharacterProfile WithMaskAccessory(int style)
    {
        return new(this) { MaskAccessoryStyle = Clamp(style, 0, 3) };
    }

    public YautjaCharacterProfile WithGreaves(YautjaGearMaterial material, int style)
    {
        return new(this)
        {
            GreavesMaterial = material,
            GreavesStyle = Clamp(style, 1, 4),
        };
    }

    public YautjaCharacterProfile WithBracer(YautjaBracerMaterial material)
    {
        return new(this) { BracerMaterial = material };
    }

    public YautjaCharacterProfile WithCaster(YautjaBracerMaterial material)
    {
        return new(this) { CasterMaterial = material };
    }

    public YautjaCharacterProfile WithOwnerRank(YautjaBracerOwnerRank ownerRank)
    {
        return new(this) { OwnerRank = ownerRank };
    }

    public YautjaCharacterProfile WithCapeStyle(YautjaCapeStyle style)
    {
        return new(this) { CapeStyle = style };
    }

    public YautjaCharacterProfile WithCapeColor(Color color)
    {
        return new(this) { CapeColor = color.WithAlpha(1f) };
    }

    public YautjaCharacterProfile WithTranslatorType(YautjaTranslatorType translatorType)
    {
        return new(this) { TranslatorType = translatorType };
    }

    public YautjaCharacterProfile WithInvisibilitySound(YautjaInvisibilitySound invisibilitySound)
    {
        return new(this) { InvisibilitySound = invisibilitySound };
    }

    public YautjaCharacterProfile WithLegacy(YautjaLegacySet legacy)
    {
        return new(this) { Legacy = legacy };
    }

    public YautjaCharacterProfile WithUnique(YautjaUniqueSet unique)
    {
        return new(this) { Unique = unique };
    }

    public YautjaCharacterProfile WithFlavorText(string flavorText)
    {
        flavorText = flavorText.Trim();
        if (flavorText.Length > MaxFlavorTextLength)
            flavorText = flavorText[..MaxFlavorTextLength];

        return new(this) { FlavorText = flavorText };
    }

    public static string GetArmorStyleDisplayName(YautjaGearMaterial material, int style)
    {
        return GearDisplayName(material, "clan armor", Clamp(style, 1, 8));
    }

    public static string GetMaskStyleDisplayName(YautjaGearMaterial material, int style)
    {
        return GearDisplayName(material, "clan mask", Clamp(style, 1, 20));
    }

    public static string GetGreavesStyleDisplayName(YautjaGearMaterial material, int style)
    {
        return GearDisplayName(material, "clan greaves", Clamp(style, 1, 4));
    }

    public static string GetBracerDisplayName(YautjaBracerMaterial material)
    {
        return material is YautjaBracerMaterial.Dragon or
            YautjaBracerMaterial.Swamp or
            YautjaBracerMaterial.Enforcer or
            YautjaBracerMaterial.Collector
            ? $"{GetBracerMaterialDisplayName(material)} legacy bracers"
            : $"{GetBracerMaterialDisplayName(material)} clan bracers";
    }

    public static string GetCasterDisplayName(YautjaBracerMaterial material)
    {
        return $"{GetBracerMaterialDisplayName(material)} shoulder plasma caster";
    }

    public static string GetCapeDisplayName(YautjaCapeStyle style)
    {
        return style switch
        {
            YautjaCapeStyle.Ceremonial => "ceremonial cape",
            YautjaCapeStyle.Third => "third cape",
            YautjaCapeStyle.Half => "half cape",
            YautjaCapeStyle.Quarter => "quarter cape",
            YautjaCapeStyle.Poncho => "councilor poncho",
            YautjaCapeStyle.Damaged => "damaged cape",
            _ => "battle-worn cape",
        };
    }

    public static string GetMaskAccessoryDisplayName(int style, YautjaGearMaterial material)
    {
        return style == 0
            ? "None"
            : $"{GetMaterialDisplayName(material)} mask accessory {Clamp(style, 1, 3)}";
    }

    public static string GetMaterialDisplayName(YautjaGearMaterial material)
    {
        return material switch
        {
            YautjaGearMaterial.Bronze => "bronze",
            YautjaGearMaterial.Silver => "silver",
            YautjaGearMaterial.Crimson => "crimson",
            YautjaGearMaterial.Bone => "bone",
            _ => "ebony",
        };
    }

    public static string GetBracerMaterialDisplayName(YautjaBracerMaterial material)
    {
        return material switch
        {
            YautjaBracerMaterial.Retro => "retro",
            YautjaBracerMaterial.Silver => "silver",
            YautjaBracerMaterial.Bronze => "bronze",
            YautjaBracerMaterial.Crimson => "crimson",
            YautjaBracerMaterial.Bone => "bone",
            YautjaBracerMaterial.Dragon => "dragon",
            YautjaBracerMaterial.Swamp => "swamp",
            YautjaBracerMaterial.Enforcer => "enforcer",
            YautjaBracerMaterial.Collector => "collector",
            _ => "ebony",
        };
    }

    public static string GetTranslatorTypeDisplayName(YautjaTranslatorType type)
    {
        return type switch
        {
            YautjaTranslatorType.Retro => "Retro",
            YautjaTranslatorType.Combo => "Combo",
            _ => "Modern",
        };
    }

    public static string GetInvisibilitySoundDisplayName(YautjaInvisibilitySound sound)
    {
        return sound switch
        {
            YautjaInvisibilitySound.Retro => "Retro",
            _ => "Modern",
        };
    }

    public static string GetLegacyDisplayName(YautjaLegacySet legacy)
    {
        return legacy switch
        {
            YautjaLegacySet.Dragon => "Dragon",
            YautjaLegacySet.Swamp => "Swamp",
            YautjaLegacySet.Enforcer => "Enforcer",
            YautjaLegacySet.Collector => "Collector",
            _ => "None",
        };
    }

    public static string GetUniqueDisplayName(YautjaUniqueSet unique)
    {
        return unique switch
        {
            YautjaUniqueSet.Anubys => "Anubys",
            YautjaUniqueSet.Cleopatra => "Cleopatra",
            YautjaUniqueSet.Plated => "Plated",
            YautjaUniqueSet.Ronin => "Ronin",
            _ => "None",
        };
    }

    public static string GetSkinColorDisplayName(YautjaSkinColor skinColor)
    {
        return skinColor switch
        {
            YautjaSkinColor.Green => "green",
            YautjaSkinColor.Purple => "purple",
            YautjaSkinColor.Blue => "blue",
            YautjaSkinColor.Red => "red",
            YautjaSkinColor.Black => "black",
            _ => "tan",
        };
    }

    public static string GetEyeColorDisplayName(YautjaEyeColor eyeColor)
    {
        return eyeColor switch
        {
            YautjaEyeColor.Amber => "amber",
            YautjaEyeColor.Copper => "copper",
            YautjaEyeColor.Red => "red",
            YautjaEyeColor.Jade => "jade",
            YautjaEyeColor.Slate => "slate",
            YautjaEyeColor.Black => "black",
            _ => "gold",
        };
    }

    public static string GetQuillStyleDisplayName(YautjaQuillStyle style)
    {
        return style switch
        {
            YautjaQuillStyle.ShortThick => "Short Thick",
            YautjaQuillStyle.StraightThin => "Straight Thin",
            YautjaQuillStyle.LongTied => "Long Tied",
            YautjaQuillStyle.ShortThin => "Short Thin",
            YautjaQuillStyle.LongCurved => "Long Curved",
            YautjaQuillStyle.LongStraight => "Long Straight",
            YautjaQuillStyle.LongWide => "Long Wide",
            YautjaQuillStyle.ShortWide => "Short Wide",
            _ => "Standard",
        };
    }

    public static Color GetSkinToneColor(int index)
    {
        return SkinToneColors[Clamp(index, 0, SkinToneColors.Length - 1)];
    }

    public static int GetClosestSkinToneIndex(Color color)
    {
        return Array.IndexOf(SkinColorOrder, GetClosestSkinColor(color));
    }

    public static Color GetClosestSkinToneColor(Color color)
    {
        return GetSkinColorColor(GetClosestSkinColor(color));
    }

    public static Color GetSkinColorColor(YautjaSkinColor color)
    {
        return color switch
        {
            YautjaSkinColor.Green => SkinToneColors[1],
            YautjaSkinColor.Purple => SkinToneColors[2],
            YautjaSkinColor.Blue => SkinToneColors[3],
            YautjaSkinColor.Red => SkinToneColors[4],
            YautjaSkinColor.Black => SkinToneColors[5],
            _ => SkinToneColors[0],
        };
    }

    public static Color GetEyeColorColor(YautjaEyeColor color)
    {
        return color switch
        {
            YautjaEyeColor.Amber => EyeColors[1],
            YautjaEyeColor.Copper => EyeColors[2],
            YautjaEyeColor.Red => EyeColors[3],
            YautjaEyeColor.Jade => EyeColors[4],
            YautjaEyeColor.Slate => EyeColors[5],
            YautjaEyeColor.Black => EyeColors[6],
            _ => EyeColors[0],
        };
    }

    public static Color GetClosestEyeColorColor(Color color)
    {
        return GetEyeColorColor(GetClosestEyeColor(color));
    }

    public static string GetQuillMarkingId(YautjaQuillStyle style)
    {
        return style switch
        {
            YautjaQuillStyle.ShortThick => $"{QuillMarkingPrefix}ShortThick",
            YautjaQuillStyle.StraightThin => $"{QuillMarkingPrefix}StraightThin",
            YautjaQuillStyle.LongTied => $"{QuillMarkingPrefix}LongTied",
            YautjaQuillStyle.ShortThin => $"{QuillMarkingPrefix}ShortThin",
            YautjaQuillStyle.LongCurved => $"{QuillMarkingPrefix}LongCurved",
            YautjaQuillStyle.LongStraight => $"{QuillMarkingPrefix}LongStraight",
            YautjaQuillStyle.LongWide => $"{QuillMarkingPrefix}LongWide",
            YautjaQuillStyle.ShortWide => $"{QuillMarkingPrefix}ShortWide",
            _ => $"{QuillMarkingPrefix}Standard",
        };
    }

    private static HumanoidCharacterAppearance BuildDefaultAppearance()
    {
        var skin = GetSkinColorColor(YautjaSkinColor.Green);
        return new HumanoidCharacterAppearance(
            HairStyles.DefaultHairStyle,
            skin,
            HairStyles.DefaultFacialHairStyle,
            Color.Black,
            GetEyeColorColor(YautjaEyeColor.Black),
            skin,
            new List<Marking>
            {
                new(GetQuillMarkingId(YautjaQuillStyle.Standard), new List<Color> { skin }),
            },
            HairStyles.DefaultHairStyle,
            Color.Black,
            HairStyles.DefaultFacialHairStyle,
            Color.Black);
    }

    private static HumanoidCharacterAppearance SanitizeAppearance(HumanoidCharacterAppearance appearance)
    {
        var skinColor = GetClosestSkinToneColor(appearance.SkinColor);
        return ApplyQuillStyle(
            appearance.Clone()
                .WithSkinColor(skinColor)
                .WithHairColor(skinColor)
                .WithEyeColor(GetClosestEyeColorColor(appearance.EyeColor)),
            GetQuillStyle(appearance));
    }

    private static HumanoidCharacterAppearance ApplyQuillStyle(HumanoidCharacterAppearance appearance, YautjaQuillStyle style)
    {
        var markings = new List<Marking>();
        foreach (var marking in appearance.Markings)
        {
            if (IsQuillMarking(marking.MarkingId))
                continue;

            markings.Add(new Marking(marking));
        }

        markings.Add(new Marking(GetQuillMarkingId(style), new List<Color> { appearance.HairColor }));
        return appearance.WithMarkings(markings);
    }

    private static YautjaQuillStyle GetQuillStyle(HumanoidCharacterAppearance appearance)
    {
        foreach (var marking in appearance.Markings)
        {
            if (!IsQuillMarking(marking.MarkingId))
                continue;

            foreach (var style in QuillStyleOrder)
            {
                if (marking.MarkingId == GetQuillMarkingId(style))
                    return style;
            }
        }

        return YautjaQuillStyle.Standard;
    }

    private static bool IsQuillMarking(string markingId)
    {
        return markingId.StartsWith(QuillMarkingPrefix, StringComparison.Ordinal);
    }

    private static YautjaSkinColor GetClosestSkinColor(Color color)
    {
        var bestColor = YautjaSkinColor.Green;
        var bestDistance = int.MaxValue;

        foreach (var skinColor in SkinColorOrder)
        {
            var tone = GetSkinColorColor(skinColor);
            var red = color.RByte - tone.RByte;
            var green = color.GByte - tone.GByte;
            var blue = color.BByte - tone.BByte;
            var distance = red * red + green * green + blue * blue;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestColor = skinColor;
        }

        return bestColor;
    }

    private static YautjaEyeColor GetClosestEyeColor(Color color)
    {
        var bestColor = YautjaEyeColor.Black;
        var bestDistance = int.MaxValue;

        foreach (var eyeColor in EyeColorOrder)
        {
            var tone = GetEyeColorColor(eyeColor);
            var red = color.RByte - tone.RByte;
            var green = color.GByte - tone.GByte;
            var blue = color.BByte - tone.BByte;
            var distance = red * red + green * green + blue * blue;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestColor = eyeColor;
        }

        return bestColor;
    }

    private static string ClanPrototype(string prefix, YautjaGearMaterial material, int style)
    {
        var suffix = MaterialSuffix(material);
        if (material == YautjaGearMaterial.Ebony)
            return style == 1 ? prefix : $"{prefix}{style}";

        return style == 1 ? $"{prefix}{suffix}" : $"{prefix}{suffix}{style}";
    }

    private static string MaterialSuffix(YautjaGearMaterial material)
    {
        return material switch
        {
            YautjaGearMaterial.Bronze => "Bronze",
            YautjaGearMaterial.Silver => "Silver",
            YautjaGearMaterial.Crimson => "Crimson",
            YautjaGearMaterial.Bone => "Bone",
            _ => "Ebony",
        };
    }

    private static string GearDisplayName(YautjaGearMaterial material, string itemName, int style)
    {
        return $"{GetMaterialDisplayName(material)} {itemName}, pattern {style}";
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Clamp(value, min, max);
    }

    private static Color C(byte red, byte green, byte blue)
    {
        return new Color(red, green, blue);
    }
}
