using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Server.Humanoid.Components
{
    [RegisterComponent]
    public sealed partial class RandomHumanoidAppearanceWhitelistedComponent : Component
    {

        [DataField("allowedHairStyles")]
        public List<string>? AllowedHairStyles;


        [DataField("allowedMaleHairStyles")]
        public List<string>? AllowedMaleHairStyles;


        [DataField("allowedFemaleHairStyles")]
        public List<string>? AllowedFemaleHairStyles;


        [DataField("allowedHairColorsHex")]
        public List<string>? AllowedHairColorsHex;


        [DataField("allowedBeardStyles")]
        public List<string>? AllowedBeardStyles;

        [DataField("allowedBeardColorsHex")]
        public List<string>? AllowedBeardColorsHex;


        [DataField("allowedEyeColorsHex")]
        public List<string>? AllowedEyeColorsHex;


        [DataField("beardChance")]
        public float BeardChance;
    }
}
