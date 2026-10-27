using Content.Server.CMU14.Threats;
using Content.Server.Preferences.Managers;
using Content.Shared.CMU14.Threats;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Preferences;

public sealed partial class ServerDbSqliteTests
{
    [Test]
    public async Task SavingLastThreatAndAntagOptOutDoesNotRestoreLegacyConsent()
    {
        var db = GetDb(Server);
        var user = NewUserId();
        const string mode = "ColonyFall";
        const string otherMode = "DistressSignal";
        const string antag = "RunawaySynthRole";
        ProtoId<ThreatPrototype> threat = "XenoThreat";
        var original = CharlieCharlieson()
            .WithThreatPreference(threat, true)
            .WithAntagPreference(antag, true)
            .WithGamemodeJobPriority(mode, ThreatVoteSelection.ThreatMemberJobId, JobPriority.High);
        await db.InitPrefsAsync(user, original);

        var optedOut = original
            .WithGamemodeThreatPreference(mode, threat, false)
            .WithGamemodeAntagPreference(mode, antag, false);
        await db.SaveCharacterSlotAsync(user, optedOut, 0);
        var saved = await db.GetPlayerPreferencesAsync(user);
        var manager = (ServerPreferencesManager) Server.ResolveDependency<IServerPreferencesManager>();
        await Server.WaitAssertion(() =>
        {
            var restored = manager.ConvertProfiles(saved!.Profiles.Single(p => p.Slot == 0));
            var held = ThreatVoteSelection.BuildHeldAssignments([user],
                new Dictionary<Robust.Shared.Network.NetUserId, HumanoidCharacterProfile> { [user] = restored },
                [threat], leaderSlots: 0, memberSlots: 1, presetId: mode);
            Assert.That(held, Is.Empty, "A high role priority must not override an explicit threat opt-out after saving.");
            Assert.That(restored.AllowsThreatForGamemode(mode, threat), Is.False,
                "The winning threat must still be rejected by the post-vote consent check.");
            Assert.That(restored.GetAntagPreferencesForGamemode(mode), Is.Empty,
                "The antag selector must not inherit the legacy opt-in after an explicit No to all.");
            Assert.That(restored.AllowsThreatForGamemode(otherMode, threat), Is.True);
            Assert.That(restored.GetAntagPreferencesForGamemode(otherMode).Select(id => id.Id), Does.Contain(antag),
                "Opting out of one mode must not change consent in another mode.");
        });
    }
}
