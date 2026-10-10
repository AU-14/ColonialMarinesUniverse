using System;
using Content.Shared.CMU14.Lobby;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using NUnit.Framework;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.Tests.Shared.CMU14;

[TestFixture]
public sealed class LobbyLineupEntryTest
{
    [Test]
    public void PreviewContainsAppearanceAndChosenOutfitButNoPrivateProfileData()
    {
        var profile = new HumanoidCharacterProfile()
            .WithName("Ready Fighter")
            .WithMedicalRecord("Private medical record")
            .WithFlavorText("Private background")
            .WithAntagPreference("Traitor", true);
        var loadout = new RoleLoadout("JobAU14JobGOVFORSquadRifleman");
        var entry = new LobbyLineupEntry(new NetUserId(Guid.NewGuid()), profile,
            "AU14JobGOVFORSquadRifleman", loadout.Role.Id, loadout, "squad", "Squad", Color.Red, 0);
        var preview = entry.ToPreviewProfile();

        Assert.Multiple(() =>
        {
            Assert.That(preview.Name, Is.EqualTo(profile.Name));
            Assert.That(preview.Appearance, Is.EqualTo(profile.Appearance));
            Assert.That(preview.MedicalRecord, Is.Empty);
            Assert.That(preview.FlavorText, Is.Empty);
            Assert.That(preview.AntagPreferences, Is.Empty);
            Assert.That(preview.Loadouts[loadout.Role.Id], Is.EqualTo(loadout));
            Assert.That(preview.Loadouts[loadout.Role.Id], Is.Not.SameAs(loadout));
        });
    }
}
