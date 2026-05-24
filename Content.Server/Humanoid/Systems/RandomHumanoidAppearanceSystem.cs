using Content.Server.CharacterAppearance.Components;
using Content.Server.Humanoid.Components;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Random;
using Robust.Shared.Maths;

namespace Content.Server.Humanoid.Systems;

public sealed partial class RandomHumanoidAppearanceSystem : EntitySystem
{
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomHumanoidAppearanceComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<RandomHumanoidAppearanceComponent> ent, ref MapInitEvent args)
    {
        var uid = ent.Owner;
        var component = ent.Comp;

        if (!TryComp(uid, out HumanoidAppearanceComponent? humanoid) || !string.IsNullOrEmpty(humanoid.Initial))
        {
            return;
        }

        var profile = HumanoidCharacterProfile.RandomWithSpecies(humanoid.Species);

        // If we have a specified hair style, change it to this
        if (component.Hair != null)
            profile = profile.WithCharacterAppearance(profile.Appearance.WithHairStyleName(component.Hair));

        if (TryComp<RandomHumanoidAppearanceWhitelistedComponent>(uid, out var whitelist))
        {
            // Hair styles: pick according to sex-specific lists first, fallback to generic list
            List<string>? hairOptions = null;
            if (profile.Sex == Sex.Male && whitelist.AllowedMaleHairStyles != null && whitelist.AllowedMaleHairStyles.Count > 0)
                hairOptions = whitelist.AllowedMaleHairStyles;
            else if (profile.Sex == Sex.Female && whitelist.AllowedFemaleHairStyles != null && whitelist.AllowedFemaleHairStyles.Count > 0)
                hairOptions = whitelist.AllowedFemaleHairStyles;
            else if (whitelist.AllowedHairStyles != null && whitelist.AllowedHairStyles.Count > 0)
                hairOptions = whitelist.AllowedHairStyles;

            if (hairOptions != null && hairOptions.Count > 0)
            {
                var chosen = _random.Pick(hairOptions);
                profile = profile.WithCharacterAppearance(profile.Appearance.WithHairStyleName(chosen));
            }

            if (whitelist.AllowedHairColorsHex != null && whitelist.AllowedHairColorsHex.Count > 0)
            {
                var colors = new List<Color>();
                foreach (var hex in whitelist.AllowedHairColorsHex)
                {

                        var c = Color.FromHex(hex);
                        colors.Add(c);


                }

                if (colors.Count > 0)
                {
                    var chosenColor = _random.Pick(colors);
                    profile = profile.WithCharacterAppearance(profile.Appearance.WithHairColor(chosenColor));
                }
            }

            if (whitelist.AllowedEyeColorsHex != null && whitelist.AllowedEyeColorsHex.Count > 0)
            {
                var eyeColors = new List<Color>();
                foreach (var hex in whitelist.AllowedEyeColorsHex)
                {

                        var c = Color.FromHex(hex);
                        eyeColors.Add(c);

                }

                if (eyeColors.Count > 0)
                {
                    var chosenEye = _random.Pick(eyeColors);
                    profile = profile.WithCharacterAppearance(profile.Appearance.WithEyeColor(chosenEye));
                }
            }

            if (profile.Sex == Sex.Female)
            {
                profile = profile.WithCharacterAppearance(profile.Appearance.WithFacialHairStyleName(HairStyles.DefaultFacialHairStyle));
                profile = profile.WithCharacterAppearance(profile.Appearance.WithFacialHairColor(Color.Black));
            }
            else
            {
                if (whitelist.BeardChance > 0f)
                {
                    var roll = _random.NextFloat();
                    if (roll < whitelist.BeardChance)
                    {
                        if (whitelist.AllowedBeardStyles != null && whitelist.AllowedBeardStyles.Count > 0)
                        {
                            var chosenBeard = _random.Pick(whitelist.AllowedBeardStyles);
                            profile = profile.WithCharacterAppearance(profile.Appearance.WithFacialHairStyleName(chosenBeard));
                        }

                        profile = profile.WithCharacterAppearance(profile.Appearance.WithFacialHairColor(profile.Appearance.HairColor));
                    }
                    else
                    {
                        profile = profile.WithCharacterAppearance(profile.Appearance.WithFacialHairStyleName(HairStyles.DefaultFacialHairStyle));
                        profile = profile.WithCharacterAppearance(profile.Appearance.WithFacialHairColor(Color.Black));
                    }
                }
                else
                {
                    profile = profile.WithCharacterAppearance(profile.Appearance.WithFacialHairStyleName(HairStyles.DefaultFacialHairStyle));
                    profile = profile.WithCharacterAppearance(profile.Appearance.WithFacialHairColor(Color.Black));
                }
            }
        }

        _humanoid.LoadProfile(uid, profile, humanoid);

        if (component.RandomizeName)
            _metaData.SetEntityName(uid, profile.Name);
    }
}
