using Robust.Shared.Audio;

namespace Content.Shared._RMC14.Weapons.Ranged.Vulture;

[RegisterComponent]
public sealed partial class VultureProjectileComponent : Component
{
    [DataField]
    public SoundSpecifier ReportSound = new SoundPathSpecifier(
            "/Audio/_RMC14/Weapons/Guns/Gunshots/gun_vulture_report.ogg",
            AudioParams.Default
                .WithVolume(12f)
                .WithReferenceDistance(16f)
                .WithMaxDistance(70f)
                .WithVariation(0.05f));
}
