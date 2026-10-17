using Content.Server.Preferences.Managers;
using Content.Shared.Preferences;
using Content.Shared.Roles;

namespace Content.IntegrationTests.Tests.Preferences;

public sealed partial class ServerDbSqliteTests
{
    [TestCase(ForceOnForceFallback.StayInLobby)]
    [TestCase(ForceOnForceFallback.OtherSide)]
    [TestCase(ForceOnForceFallback.OtherRole)]
    [TestCase(ForceOnForceFallback.Both)]
    public async Task ForceOnForcePreferencesRoundTrip(ForceOnForceFallback fallback)
    {
        var db = GetDb(Server);
        var user = NewUserId();
        var profile = CharlieCharlieson()
            .WithForceOnForcePreferences(ForceOnForceSide.Opfor, fallback)
            .WithGamemodeJobPriority("ForceOnForce", "AU14JobOPFORSquadRifleman", JobPriority.High);
        await db.InitPrefsAsync(user, CharlieCharlieson());
        await db.SaveCharacterSlotAsync(user, profile, 0);
        var saved = await db.GetPlayerPreferencesAsync(user);
        var preferences = (ServerPreferencesManager) Server.ResolveDependency<IServerPreferencesManager>();
        await Server.WaitAssertion(() =>
        {
            var restored = preferences.ConvertProfiles(saved!.Profiles.Single(p => p.Slot == 0));
            Assert.That(restored.FoFSide, Is.EqualTo(ForceOnForceSide.Opfor));
            Assert.That(restored.FoFFallback, Is.EqualTo(fallback));
            Assert.That(restored.GetJobPriorityForGamemode("ForceOnForce", "AU14JobOPFORSquadRifleman"),
                Is.EqualTo(JobPriority.High));
            Assert.That(restored.MemberwiseEquals(profile), Is.True);
        });
    }
}
