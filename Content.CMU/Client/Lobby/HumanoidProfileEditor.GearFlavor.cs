// ReSharper disable CheckNamespace

using Content.Shared.CMU14.GearFlavor;

namespace Content.Client.Lobby.UI;

// CMU14: text on the armor and helmet a GOVFOR/OPFOR character draws from their vendor.
public sealed partial class HumanoidProfileEditor
{
    private void InitializeGearFlavor()
    {
        ArmorFlavorTextEdit.IsValid = text => text.Length <= CMUGearFlavorText.MaxLength;
        HelmetFlavorTextEdit.IsValid = text => text.Length <= CMUGearFlavorText.MaxLength;

        ArmorFlavorTextEdit.OnTextChanged += args =>
        {
            Profile = Profile?.WithArmorFlavorText(args.Text);
            SetDirty();
        };

        HelmetFlavorTextEdit.OnTextChanged += args =>
        {
            Profile = Profile?.WithHelmetFlavorText(args.Text);
            SetDirty();
        };
    }

    private void UpdateGearFlavorControls()
    {
        ArmorFlavorTextEdit.Text = Profile?.ArmorFlavorText ?? string.Empty;
        HelmetFlavorTextEdit.Text = Profile?.HelmetFlavorText ?? string.Empty;
    }
}
