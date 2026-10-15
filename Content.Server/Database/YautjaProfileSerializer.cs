using System.Linq;
using System.Text.Json;
using Content.Shared.Body;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server.Database;

// CMU14: shared profile persistence for database writes and preference loading.
internal static class YautjaProfileSerializer
{
    private sealed class SerializedYautjaProfile
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public Sex Sex { get; set; }
        public Gender Gender { get; set; }
        public string HairStyleId { get; set; } = string.Empty;
        public string HairColor { get; set; } = string.Empty;
        public string FacialHairStyleId { get; set; } = string.Empty;
        public string FacialHairColor { get; set; } = string.Empty;
        public string EyeColor { get; set; } = string.Empty;
        public string SkinColor { get; set; } = string.Empty;
        public List<string> Markings { get; set; } = new();
        public YautjaQuillStyle? QuillStyle { get; set; }
        public YautjaDreadColor? DreadColor { get; set; }
        public YautjaSkinColor? SkinColorPreset { get; set; }
        public YautjaGearMaterial ArmorMaterial { get; set; }
        public int ArmorStyle { get; set; }
        public YautjaGearMaterial MaskMaterial { get; set; }
        public int MaskStyle { get; set; }
        public int MaskAccessoryStyle { get; set; }
        public YautjaGearMaterial GreavesMaterial { get; set; }
        public int GreavesStyle { get; set; }
        public YautjaBracerMaterial? BracerMaterial { get; set; }
        public YautjaBracerMaterial? CasterMaterial { get; set; }
        public YautjaTranslatorType? TranslatorType { get; set; }
        public YautjaInvisibilitySound? InvisibilitySound { get; set; }
        public YautjaLegacySet? Legacy { get; set; }
        public YautjaUniqueSet? Unique { get; set; }
        public YautjaProfileStatus? Status { get; set; }
        public YautjaCapeStyle? CapeStyle { get; set; }
        public string CapeColor { get; set; } = string.Empty;
        public string FlavorText { get; set; } = string.Empty;
    }

    public static YautjaCharacterProfile DeserializeYautjaProfile(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            return YautjaCharacterProfile.Default;

        SerializedYautjaProfile? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SerializedYautjaProfile>(serialized);
        }
        catch (JsonException)
        {
            return YautjaCharacterProfile.Default;
        }

        if (parsed == null)
            return YautjaCharacterProfile.Default;

        var markings = new List<Marking>();
        foreach (var marking in parsed.Markings)
        {
            var parsedMarking = Marking.ParseFromDbString(marking);
            if (parsedMarking != null)
                markings.Add(parsedMarking.Value);
        }

        var appearance = new HumanoidCharacterAppearance(
            ReadColor(parsed.EyeColor, Color.Gold),
            ReadColor(parsed.SkinColor, new Color((byte) 56, (byte) 90, (byte) 48)),
            new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>
            {
                ["Head"] = new() { [HumanoidVisualLayers.Hair] = markings },
            });

        var yautjaProfile = YautjaCharacterProfile.Default
            .WithName(string.IsNullOrWhiteSpace(parsed.Name) ? YautjaCharacterProfile.Default.Name : parsed.Name)
            .WithAge(parsed.Age)
            .WithSex(parsed.Sex)
            .WithGender(parsed.Gender)
            .WithAppearance(appearance)
            .WithDreadColor(parsed.DreadColor ?? YautjaDreadColor.MatchSkin)
            .WithArmor(parsed.ArmorMaterial, parsed.ArmorStyle)
            .WithMask(parsed.MaskMaterial, parsed.MaskStyle)
            .WithMaskAccessory(parsed.MaskAccessoryStyle)
            .WithGreaves(parsed.GreavesMaterial, parsed.GreavesStyle)
            .WithBracer(parsed.BracerMaterial ?? YautjaCharacterProfile.Default.BracerMaterial)
            .WithCaster(parsed.CasterMaterial ?? YautjaCharacterProfile.Default.CasterMaterial)
            .WithTranslatorType(parsed.TranslatorType ?? YautjaCharacterProfile.Default.TranslatorType)
            .WithInvisibilitySound(parsed.InvisibilitySound ?? YautjaCharacterProfile.Default.InvisibilitySound)
            .WithLegacy(parsed.Legacy ?? YautjaCharacterProfile.Default.Legacy)
            .WithUnique(parsed.Unique ?? YautjaCharacterProfile.Default.Unique)
            .WithStatus(parsed.Status ?? YautjaCharacterProfile.Default.Status)
            .WithCapeStyle(parsed.CapeStyle ?? YautjaCharacterProfile.Default.CapeStyle)
            .WithCapeColor(ReadColor(parsed.CapeColor, YautjaCharacterProfile.Default.CapeColor))
            .WithFlavorText(parsed.FlavorText ?? string.Empty);

        if (parsed.SkinColorPreset is { } skinColor)
            yautjaProfile = yautjaProfile.WithSkinColor(skinColor);

        if (parsed.QuillStyle is { } quillStyle)
            yautjaProfile = yautjaProfile.WithQuillStyle(quillStyle);

        return yautjaProfile;
    }

    public static string SerializeYautjaProfile(YautjaCharacterProfile profile)
    {
        var appearance = profile.Appearance;
        var serialized = new SerializedYautjaProfile
        {
            Name = profile.Name,
            Age = profile.Age,
            Sex = profile.Sex,
            Gender = profile.Gender,
            HairStyleId = HairStyles.DefaultHairStyle,
            HairColor = YautjaCharacterProfile.GetQuillColor(appearance).ToHex(),
            FacialHairStyleId = HairStyles.DefaultFacialHairStyle,
            FacialHairColor = Color.Black.ToHex(),
            EyeColor = appearance.EyeColor.ToHex(),
            SkinColor = appearance.SkinColor.ToHex(),
            Markings = appearance.Markings.Values.SelectMany(layers => layers.Values).SelectMany(markings => markings).Select(marking => marking.ToLegacyDbString()).ToList(),
            QuillStyle = profile.QuillStyle,
            DreadColor = profile.DreadColor,
            SkinColorPreset = profile.SkinColor,
            ArmorMaterial = profile.ArmorMaterial,
            ArmorStyle = profile.ArmorStyle,
            MaskMaterial = profile.MaskMaterial,
            MaskStyle = profile.MaskStyle,
            MaskAccessoryStyle = profile.MaskAccessoryStyle,
            GreavesMaterial = profile.GreavesMaterial,
            GreavesStyle = profile.GreavesStyle,
            BracerMaterial = profile.BracerMaterial,
            CasterMaterial = profile.CasterMaterial,
            TranslatorType = profile.TranslatorType,
            InvisibilitySound = profile.InvisibilitySound,
            Legacy = profile.Legacy,
            Unique = profile.Unique,
            Status = profile.Status,
            CapeStyle = profile.CapeStyle,
            CapeColor = profile.CapeColor.ToHex(),
            FlavorText = profile.FlavorText,
        };

        return JsonSerializer.Serialize(serialized);
    }

    private static Color ReadColor(string value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        try
        {
            return Color.FromHex(value);
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

}
