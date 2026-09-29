using Content.Server.CMU14.Threats;
using Content.Server.Preferences.Managers;
using Content.Shared.CMU14.Threats;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Preferences;

public sealed partial class ServerDbSqliteTests
{
    [TestCase("AbominationsThreatCF", "BiomorphsThreatCF", "ColonyFall")]
    // [TestCase("AbominationsThreatDS", "BiomorphsThreatDS", "DistressSignal")]
    public async Task LegacyBiomorphPreferencesPreserveThreatConsent(string legacyId, string currentId, string preset)
    {
        var db = GetDb(Server);
        var user = NewUserId();
        var original = CharlieCharlieson()
            .WithThreatPreference(legacyId, true)
            .WithGamemodeThreatPreference(preset, legacyId, true)
            .WithGamemodeJobPriority(preset, ThreatVoteSelection.ThreatMemberJobId, JobPriority.High);
        await db.InitPrefsAsync(user, original);

        var manager = (ServerPreferencesManager) Server.ResolveDependency<IServerPreferencesManager>();
        var saved = await db.GetPlayerPreferencesAsync(user);
        var profile = manager.ConvertProfiles(saved!.Profiles.Single());

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.HasIndex<ThreatPrototype>(currentId), Is.True);
            Assert.That(profile.ThreatPreferences.Select(id => id.Id), Is.EquivalentTo(new[] { currentId }));
            Assert.That(profile.GetThreatPreferencesForGamemode(preset).Select(id => id.Id),
                Is.EquivalentTo(new[] { currentId }));
            Assert.That(ThreatVoteSelection.CanEnterThreatVotePoolForJob(profile, preset,
                new ProtoId<ThreatPrototype>[] { currentId }, ThreatVoteSelection.ThreatMemberJobId), Is.True);
            Assert.That(ThreatVoteSelection.CanEnterThreatVotePoolForJob(profile, preset,
                new ProtoId<ThreatPrototype>[] { "XenoThreat" }, ThreatVoteSelection.ThreatMemberJobId), Is.False,
                "Migrating a biomorph-only preference must not opt the player into other threats.");
        });

        await db.SaveCharacterSlotAsync(user, profile, 0);
        var reloaded = await db.GetPlayerPreferencesAsync(user);
        var roundTripped = manager.ConvertProfiles(reloaded!.Profiles.Single());
        Assert.That(roundTripped.ThreatPreferences, Is.EquivalentTo(profile.ThreatPreferences));
        Assert.That(roundTripped.GetThreatPreferencesForGamemode(preset),
            Is.EquivalentTo(profile.GetThreatPreferencesForGamemode(preset)));
    }

    [TestCase("\"AbominationsThreatCF\"")]
    [TestCase("AbominationsThreatCF")]
    [TestCase("[\"AbominationsThreatCF\",\"BiomorphsThreatCF\"]")]
    [TestCase("AbominationsThreatCF;BiomorphsThreatCF")]
    public async Task LegacyBiomorphPreferenceFormatsMigrateWithoutDuplicates(string raw)
    {
        var db = GetDb(Server);
        var user = NewUserId();
        await db.InitPrefsAsync(user, CharlieCharlieson());
        var saved = await db.GetPlayerPreferencesAsync(user);
        var stored = saved!.Profiles.Single();
        stored.ThreatPreference = raw;
        stored.GamemodeThreatPreferences =
            "{\"ColonyFall\":[\"AbominationsThreatCF\",\"BiomorphsThreatCF\",\"XenoThreat\"],\"DistressSignal\":[]}";

        var manager = (ServerPreferencesManager) Server.ResolveDependency<IServerPreferencesManager>();
        var profile = manager.ConvertProfiles(stored);
        Assert.That(profile.ThreatPreferences.Select(id => id.Id), Is.EquivalentTo(new[] { "BiomorphsThreatCF" }));
        Assert.That(profile.GetThreatPreferencesForGamemode("ColonyFall").Select(id => id.Id),
            Is.EquivalentTo(new[] { "BiomorphsThreatCF", "XenoThreat" }));
        Assert.That(profile.GetThreatPreferencesForGamemode("DistressSignal"), Is.Empty);
    }
}
