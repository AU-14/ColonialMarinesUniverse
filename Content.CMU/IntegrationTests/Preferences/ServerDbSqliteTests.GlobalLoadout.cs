using Content.Server.Preferences.Managers;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;

namespace Content.IntegrationTests.Tests.Preferences;

public sealed partial class ServerDbSqliteTests
{
    [Test]
    public async Task GlobalLoadoutRoundTripsAndCanBeCleared()
    {
        var db = GetDb(Server);
        var user = NewUserId();
        var global = new RoleLoadout(HumanoidCharacterProfile.GlobalLoadoutId);
        global.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup] = [new() { Prototype = "RMCPinLGBT" }];
        var profile = CharlieCharlieson().WithLoadout(HumanoidCharacterProfile.GlobalLoadoutId, global);
        await db.InitPrefsAsync(user, CharlieCharlieson());
        await db.SaveCharacterSlotAsync(user, profile, 0);
        var saved = await db.GetPlayerPreferencesAsync(user);
        var preferences = (ServerPreferencesManager) Server.ResolveDependency<IServerPreferencesManager>();
        await Server.WaitAssertion(() =>
        {
            var restored = preferences.ConvertProfiles(saved!.Profiles.Single(p => p.Slot == 0));
            Assert.That(restored.MemberwiseEquals(profile), Is.True);
            Assert.That(restored.Loadouts[HumanoidCharacterProfile.GlobalLoadoutId]
                .SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup].Single().Prototype.Id, Is.EqualTo("RMCPinLGBT"));
        });

        global.SelectedLoadouts[HumanoidCharacterProfile.GlobalPinsGroup].Clear();
        profile = profile.WithLoadout(HumanoidCharacterProfile.GlobalLoadoutId, global);
        await db.SaveCharacterSlotAsync(user, profile, 0);
        saved = await db.GetPlayerPreferencesAsync(user);
        await Server.WaitAssertion(() =>
        {
            var restored = preferences.ConvertProfiles(saved!.Profiles.Single(p => p.Slot == 0));
            Assert.That(restored.Loadouts, Does.ContainKey(HumanoidCharacterProfile.GlobalLoadoutId),
                "An explicit empty global loadout must survive saving to prevent repeating legacy migration.");
            Assert.That(restored.Loadouts[HumanoidCharacterProfile.GlobalLoadoutId].SelectedLoadouts.Values.SelectMany(pins => pins),
                Is.Empty);
        });
    }
}
