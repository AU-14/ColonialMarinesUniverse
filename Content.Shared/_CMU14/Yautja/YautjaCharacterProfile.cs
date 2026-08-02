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
public enum YautjaDreadColor : byte
{
    MatchSkin,
    Black,
    DarkBrown,
    Brown,
    Auburn,
    Ash,
    Bone,
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

    public static readonly YautjaProfileStatus[] StatusOrder =
    [
        YautjaProfileStatus.Normal,
        YautjaProfileStatus.Council,
        YautjaProfileStatus.Leader,
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

    public static readonly YautjaDreadColor[] DreadColorOrder =
    [
        YautjaDreadColor.MatchSkin,
        YautjaDreadColor.Black,
        YautjaDreadColor.DarkBrown,
        YautjaDreadColor.Brown,
        YautjaDreadColor.Auburn,
        YautjaDreadColor.Ash,
        YautjaDreadColor.Bone,
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
    public YautjaDreadColor DreadColor { get; private set; } = YautjaDreadColor.MatchSkin;

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
    public YautjaRank? ClanRank { get; private set; }

    [DataField]
    public YautjaBracerOwnerRank OwnerRank { get; private set; } = YautjaBracerOwnerRank.Unblooded;

    [DataField]
    public YautjaProfileStatus Status { get; private set; } = YautjaProfileStatus.Normal;

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
    public Color DreadColorValue => GetDreadColorColor(DreadColor, Appearance.SkinColor);

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
        ? $"{GetLegacyDisplayName(Legacy)}-armor"
        : Unique != YautjaUniqueSet.None
            ? $"{GetUniqueDisplayName(Unique)}-armor"
            : GetArmorStyleDisplayName(ArmorMaterial, ArmorStyle);

    public string MaskDisplayName => Legacy != YautjaLegacySet.None
        ? $"{GetLegacyDisplayName(Legacy)}-mask"
        : Unique != YautjaUniqueSet.None
            ? $"{GetUniqueDisplayName(Unique)}-mask"
            : GetMaskStyleDisplayName(MaskMaterial, MaskStyle);

    public string GreavesDisplayName => Legacy != YautjaLegacySet.None
        ? $"{GetLegacyDisplayName(Legacy)}-greaves"
        : Unique != YautjaUniqueSet.None
            ? $"{GetUniqueDisplayName(Unique)}-greaves"
            : GetGreavesStyleDisplayName(GreavesMaterial, GreavesStyle);

    public string BracerDisplayName => Legacy != YautjaLegacySet.None
        ? $"{GetLegacyDisplayName(Legacy)}-bracer"
        : GetBracerDisplayName(BracerMaterial);

    public YautjaCharacterProfile()
    {
    }

    private YautjaCharacterProfile(YautjaCharacterProfile other)
    {
        Name = other.Name;
        Age = other.Age;
        var isFemale = other.Sex == Sex.Female || other.Gender == Gender.Female;
        Sex = isFemale ? Sex.Female : Sex.Male;
        Gender = isFemale ? Gender.Female : Gender.Male;
        DreadColor = SanitizeDreadColor(other.DreadColor);
        Appearance = SanitizeAppearance(other.Appearance, DreadColor);
        ArmorMaterial = SanitizeEnum(other.ArmorMaterial, YautjaGearMaterial.Ebony);
        ArmorStyle = other.ArmorStyle is >= 1 and <= 8 ? other.ArmorStyle : DefaultArmorStyle;
        MaskMaterial = SanitizeEnum(other.MaskMaterial, YautjaGearMaterial.Ebony);
        MaskStyle = other.MaskStyle is >= 1 and <= 20 ? other.MaskStyle : DefaultMaskStyle;
        MaskAccessoryStyle = other.MaskAccessoryStyle is >= 0 and <= 3 ? other.MaskAccessoryStyle : 0;
        GreavesMaterial = SanitizeEnum(other.GreavesMaterial, YautjaGearMaterial.Ebony);
        GreavesStyle = other.GreavesStyle is >= 1 and <= 4 ? other.GreavesStyle : DefaultGreavesStyle;
        BracerMaterial = SanitizeEnum(other.BracerMaterial, YautjaBracerMaterial.Ebony);
        CasterMaterial = SanitizeEnum(other.CasterMaterial, YautjaBracerMaterial.Ebony);
        ClanRank = other.ClanRank is { } clanRank && Enum.IsDefined(clanRank) ? clanRank : null;
        OwnerRank = SanitizeEnum(other.OwnerRank, YautjaBracerOwnerRank.Unblooded);
        Status = SanitizeEnum(other.Status, YautjaProfileStatus.Normal);
        CapeStyle = SanitizeEnum(other.CapeStyle, YautjaCapeStyle.Full);
        CapeColor = other.CapeColor;
        TranslatorType = SanitizeEnum(other.TranslatorType, YautjaTranslatorType.Modern);
        InvisibilitySound = SanitizeEnum(other.InvisibilitySound, YautjaInvisibilitySound.Modern);
        Legacy = SanitizeEnum(other.Legacy, YautjaLegacySet.None);
        Unique = SanitizeEnum(other.Unique, YautjaUniqueSet.None);
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
        var isFemale = sex == Sex.Female;
        return new(this)
        {
            Sex = isFemale ? Sex.Female : Sex.Male,
            Gender = isFemale ? Gender.Female : Gender.Male,
        };
    }

    public YautjaCharacterProfile WithGender(Gender gender)
    {
        var isFemale = gender == Gender.Female;
        return new(this)
        {
            Sex = isFemale ? Sex.Female : Sex.Male,
            Gender = isFemale ? Gender.Female : Gender.Male,
        };
    }

    public YautjaCharacterProfile WithAppearance(HumanoidCharacterAppearance appearance)
    {
        var profile = new YautjaCharacterProfile(this);
        profile.Appearance = SanitizeAppearance(appearance, profile.DreadColor);
        return profile;
    }

    public YautjaCharacterProfile WithSkinColor(YautjaSkinColor skinColor)
    {
        var color = GetSkinColorColor(skinColor);
        return WithAppearance(Appearance.WithSkinColor(color));
    }

    public YautjaCharacterProfile WithEyeColor(YautjaEyeColor eyeColor)
    {
        return WithAppearance(Appearance.WithEyeColor(GetEyeColorColor(eyeColor)));
    }

    public YautjaCharacterProfile WithDreadColor(YautjaDreadColor dreadColor)
    {
        var profile = new YautjaCharacterProfile(this)
        {
            DreadColor = SanitizeDreadColor(dreadColor),
        };
        profile.Appearance = SanitizeAppearance(profile.Appearance, profile.DreadColor);
        return profile;
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

    public YautjaCharacterProfile WithClanRank(YautjaRank rank)
    {
        return WithRank(rank);
    }

    public YautjaCharacterProfile WithRank(YautjaRank rank)
    {
        if (!Enum.IsDefined(rank))
            rank = YautjaRank.Blooded;

        var profile = new YautjaCharacterProfile(this)
        {
            ClanRank = rank,
            OwnerRank = YautjaRankResolver.ToOwnerRank(rank),
        };

        return YautjaRankResolver.CanUseUnique(rank)
            ? profile
            : profile.WithUnique(YautjaUniqueSet.None);
    }

    public YautjaCharacterProfile WithStatus(YautjaProfileStatus status)
    {
        return new(this)
        {
            Status = Enum.IsDefined(status) ? status : YautjaProfileStatus.Normal,
        };
    }

    private YautjaCharacterProfile WithActiveRank(YautjaRank rank)
    {
        if (!Enum.IsDefined(rank))
            rank = YautjaRank.Blooded;

        return new YautjaCharacterProfile(this)
        {
            ClanRank = rank,
            OwnerRank = YautjaRankResolver.ToOwnerRank(rank),
        };
    }

    public YautjaCharacterProfile SanitizeForCapabilities(YautjaProfileCapabilities capabilities)
    {
        var status = capabilities.SanitizeStatus(Status);
        var activeCapabilities = capabilities.ForStatus(status);
        var profile = WithStatus(status).WithActiveRank(activeCapabilities.Rank);

        if (!capabilities.CanUseLegacySet(profile.Legacy))
            profile = profile.WithLegacy(YautjaLegacySet.None);

        if (!capabilities.CanUseUnique || profile.Legacy != YautjaLegacySet.None)
            profile = profile.WithUnique(YautjaUniqueSet.None);

        if (!capabilities.CanUseCape(profile.CapeStyle))
            profile = profile.WithCapeStyle(YautjaCapeStyle.Full);

        if (!capabilities.CanUseBracer(profile.BracerMaterial))
            profile = profile.WithBracer(YautjaBracerMaterial.Ebony);

        return profile;
    }

    public YautjaCharacterProfile WithOwnerRank(YautjaBracerOwnerRank ownerRank)
    {
        return new(this)
        {
            ClanRank = null,
            OwnerRank = ownerRank,
        };
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
        return GearDisplayName(material, "armor", Clamp(style, 1, 8));
    }

    public static string GetMaskStyleDisplayName(YautjaGearMaterial material, int style)
    {
        return GearDisplayName(material, "mask", Clamp(style, 1, 20));
    }

    public static string GetGreavesStyleDisplayName(YautjaGearMaterial material, int style)
    {
        return GearDisplayName(material, "greaves", Clamp(style, 1, 4));
    }

    public static string GetBracerDisplayName(YautjaBracerMaterial material)
    {
        var variant = material is YautjaBracerMaterial.Dragon or
            YautjaBracerMaterial.Swamp or
            YautjaBracerMaterial.Enforcer or
            YautjaBracerMaterial.Collector
            ? "legacy"
            : "clan";
        return $"cmu-yautja-profile-bracer-{BracerMaterialKey(material)}-{variant}";
    }

    public static string GetCasterDisplayName(YautjaBracerMaterial material)
    {
        return $"cmu-yautja-profile-caster-{BracerMaterialKey(material)}";
    }

    public static string GetCapeDisplayName(YautjaCapeStyle style)
    {
        var suffix = style switch
        {
            YautjaCapeStyle.Ceremonial => "ceremonial",
            YautjaCapeStyle.Third => "third",
            YautjaCapeStyle.Half => "half",
            YautjaCapeStyle.Quarter => "quarter",
            YautjaCapeStyle.Poncho => "poncho",
            YautjaCapeStyle.Damaged => "damaged",
            _ => "full",
        };
        return $"cmu-yautja-profile-cape-{suffix}";
    }

    public static string GetMaskAccessoryDisplayName(int style, YautjaGearMaterial material)
    {
        return style == 0
            ? "cmu-yautja-profile-mask-accessory-none"
            : $"cmu-yautja-profile-mask-accessory-{MaterialKey(material)}-{Clamp(style, 1, 3)}";
    }

    public static string GetMaterialDisplayName(YautjaGearMaterial material)
    {
        return $"cmu-yautja-profile-material-{MaterialKey(material)}";
    }

    public static string GetBracerMaterialDisplayName(YautjaBracerMaterial material)
    {
        return $"cmu-yautja-profile-bracer-material-{BracerMaterialKey(material)}";
    }

    public static string GetTranslatorTypeDisplayName(YautjaTranslatorType type)
    {
        var suffix = type switch
        {
            YautjaTranslatorType.Retro => "retro",
            YautjaTranslatorType.Combo => "combo",
            _ => "modern",
        };
        return $"cmu-yautja-profile-translator-{suffix}";
    }

    public static string GetInvisibilitySoundDisplayName(YautjaInvisibilitySound sound)
    {
        var suffix = sound switch
        {
            YautjaInvisibilitySound.Retro => "retro",
            _ => "modern",
        };
        return $"cmu-yautja-profile-invisibility-sound-{suffix}";
    }

    public static string GetLegacyDisplayName(YautjaLegacySet legacy)
    {
        var suffix = legacy switch
        {
            YautjaLegacySet.Dragon => "dragon",
            YautjaLegacySet.Swamp => "swamp",
            YautjaLegacySet.Enforcer => "enforcer",
            YautjaLegacySet.Collector => "collector",
            _ => "none",
        };
        return $"cmu-yautja-profile-legacy-{suffix}";
    }

    public static string GetStatusDisplayName(YautjaProfileStatus status)
    {
        var suffix = status switch
        {
            YautjaProfileStatus.Council => "council",
            YautjaProfileStatus.Leader => "leader",
            _ => "normal",
        };
        return $"cmu-yautja-profile-status-{suffix}";
    }

    public static string GetUniqueDisplayName(YautjaUniqueSet unique)
    {
        var suffix = unique switch
        {
            YautjaUniqueSet.Anubys => "anubys",
            YautjaUniqueSet.Cleopatra => "cleopatra",
            YautjaUniqueSet.Plated => "plated",
            YautjaUniqueSet.Ronin => "ronin",
            _ => "none",
        };
        return $"cmu-yautja-profile-unique-{suffix}";
    }

    public static string GetSkinColorDisplayName(YautjaSkinColor skinColor)
    {
        var suffix = skinColor switch
        {
            YautjaSkinColor.Green => "green",
            YautjaSkinColor.Purple => "purple",
            YautjaSkinColor.Blue => "blue",
            YautjaSkinColor.Red => "red",
            YautjaSkinColor.Black => "black",
            _ => "tan",
        };
        return $"cmu-yautja-profile-skin-color-{suffix}";
    }

    public static string GetEyeColorDisplayName(YautjaEyeColor eyeColor)
    {
        var suffix = eyeColor switch
        {
            YautjaEyeColor.Amber => "amber",
            YautjaEyeColor.Copper => "copper",
            YautjaEyeColor.Red => "red",
            YautjaEyeColor.Jade => "jade",
            YautjaEyeColor.Slate => "slate",
            YautjaEyeColor.Black => "black",
            _ => "gold",
        };
        return $"cmu-yautja-profile-eye-color-{suffix}";
    }

    public static string GetDreadColorDisplayName(YautjaDreadColor dreadColor)
    {
        var suffix = dreadColor switch
        {
            YautjaDreadColor.Black => "black",
            YautjaDreadColor.DarkBrown => "dark brown",
            YautjaDreadColor.Brown => "brown",
            YautjaDreadColor.Auburn => "auburn",
            YautjaDreadColor.Ash => "ash",
            YautjaDreadColor.Bone => "bone",
            _ => "match-skin",
        };
        return $"cmu-yautja-profile-dread-color-{suffix.Replace(' ', '-')}";
    }

    public static string GetQuillStyleDisplayName(YautjaQuillStyle style)
    {
        var suffix = style switch
        {
            YautjaQuillStyle.ShortThick => "short-thick",
            YautjaQuillStyle.StraightThin => "straight-thin",
            YautjaQuillStyle.LongTied => "long-tied",
            YautjaQuillStyle.ShortThin => "short-thin",
            YautjaQuillStyle.LongCurved => "long-curved",
            YautjaQuillStyle.LongStraight => "long-straight",
            YautjaQuillStyle.LongWide => "long-wide",
            YautjaQuillStyle.ShortWide => "short-wide",
            _ => "standard",
        };
        return $"cmu-yautja-profile-quill-{suffix}";
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

    public static Color GetDreadColorColor(YautjaDreadColor color, Color skinColor)
    {
        return color switch
        {
            YautjaDreadColor.Black => C(20, 18, 16),
            YautjaDreadColor.DarkBrown => C(45, 32, 24),
            YautjaDreadColor.Brown => C(78, 54, 34),
            YautjaDreadColor.Auburn => C(94, 48, 36),
            YautjaDreadColor.Ash => C(105, 105, 100),
            YautjaDreadColor.Bone => C(185, 174, 145),
            _ => skinColor.WithAlpha(1f),
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

    private static HumanoidCharacterAppearance SanitizeAppearance(
        HumanoidCharacterAppearance appearance,
        YautjaDreadColor dreadColor)
    {
        var skinColor = GetClosestSkinToneColor(appearance.SkinColor);
        var hairColor = GetDreadColorColor(SanitizeDreadColor(dreadColor), skinColor);
        return ApplyQuillStyle(
            appearance.Clone()
                .WithSkinColor(skinColor)
                .WithHairColor(hairColor)
                .WithEyeColor(GetClosestEyeColorColor(appearance.EyeColor)),
            GetQuillStyle(appearance));
    }

    private static YautjaDreadColor SanitizeDreadColor(YautjaDreadColor dreadColor)
    {
        return Enum.IsDefined(dreadColor) ? dreadColor : YautjaDreadColor.MatchSkin;
    }

    private static T SanitizeEnum<T>(T value, T fallback) where T : struct, Enum
    {
        return Enum.IsDefined(value) ? value : fallback;
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
        return $"cmu-yautja-profile-{itemName}-{MaterialKey(material)}-{style}";
    }

    private static string MaterialKey(YautjaGearMaterial material)
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

    private static string BracerMaterialKey(YautjaBracerMaterial material)
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

    private static int Clamp(int value, int min, int max)
    {
        return Math.Clamp(value, min, max);
    }

    private static Color C(byte red, byte green, byte blue)
    {
        return new Color(red, green, blue);
    }
}
