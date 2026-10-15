using Content.Shared.CMU14.GearFlavor;

namespace Content.Shared.Preferences;

// CMU14: text written on the armor and helmet a GOVFOR/OPFOR character draws from their vendor.
public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public string ArmorFlavorText { get; private set; } = string.Empty;

    [DataField]
    public string HelmetFlavorText { get; private set; } = string.Empty;

    public HumanoidCharacterProfile WithArmorFlavorText(string text)
    {
        return new(this) { ArmorFlavorText = CMUGearFlavorText.Clamp(text) };
    }

    public HumanoidCharacterProfile WithHelmetFlavorText(string text)
    {
        return new(this) { HelmetFlavorText = CMUGearFlavorText.Clamp(text) };
    }
}
