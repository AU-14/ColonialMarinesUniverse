// cmu edit start
using Content.Server.Preferences.Managers;
using Content.Shared.CMU14.Religion;
using Content.Shared.Preferences;

namespace Content.IntegrationTests.Tests.Preferences;

public sealed partial class ServerDbSqliteTests
{
    [Test]
    public async Task ReligionRoundTripsAndDefaultsToAtheist()
    {
        var db = GetDb(Server);
        var user = NewUserId();
        var profile = CharlieCharlieson().WithReligion("CMUBuddhism");

        Assert.That(CharlieCharlieson().Religion, Is.EqualTo(CMUReligionPrototype.Atheist));

        await db.InitPrefsAsync(user, CharlieCharlieson());
        await db.SaveCharacterSlotAsync(user, profile, 0);
        var saved = await db.GetPlayerPreferencesAsync(user);
        var preferences = (ServerPreferencesManager) Server.ResolveDependency<IServerPreferencesManager>();
        await Server.WaitAssertion(() =>
        {
            var restored = preferences.ConvertProfiles(saved!.Profiles.Single(p => p.Slot == 0));
            Assert.That(restored.Religion, Is.EqualTo(new Robust.Shared.Prototypes.ProtoId<CMUReligionPrototype>("CMUBuddhism")));
            Assert.That(restored.MemberwiseEquals(profile), Is.True);
        });
    }
}
// cmu edit end
