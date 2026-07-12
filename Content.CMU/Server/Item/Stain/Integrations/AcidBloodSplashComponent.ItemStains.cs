// ReSharper disable CheckNamespace

namespace Content.Server._RMC14.Xenonids.AcidBloodSplash;

public sealed partial class AcidBloodSplashComponent
{
    /// <summary>
    /// CMU item-stain tint applied to exposed clothing hit by acid blood.
    /// </summary>
    [DataField]
    public Color StainColor = Color.FromHex("#BED700");
}
