using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Language.Systems;
using Content.Server.CMU14.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Roles;
using Content.Server.CMU14.Platoon;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CMU14.util;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Traits;

[TestFixture]
public sealed class CMUCharacterLanguageTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private static readonly ProtoId<LanguagePrototype> English = "English";
    private static readonly ProtoId<LanguagePrototype> Mandarin = "Mandarin";
    private static readonly ProtoId<LanguagePrototype> Portuguese = "Portuguese";
    private static readonly ProtoId<TraitPrototype> EnglishTrait = "EnglishLanguage";

    private const string UppColony = "AUPlanetSorokyne";
    private const string Colonist = "AU14JobCivilianColonist";
    private const string Rifleman = "AU14JobGOVFORSquadRifleman";
    private const string ClfGuerilla = "AU14JobCLFGuerilla";
    private const string ClfReinforcement = "AU14JobCLFGuerillaReinforcement";

    [Test]
    public async Task ColonistSpeaksColonyLanguageWithoutAutomaticEnglish()
    {
        var language = await SpawnCharacter(Colonist, UppColony, new HumanoidCharacterProfile());

        Assert.Multiple(() =>
        {
            Assert.That(language.SpokenLanguages, Does.Contain(Mandarin), "a UPP colony's colonists speak Mandarin");
            Assert.That(language.SpokenLanguages, Does.Not.Contain(English), "English is a trait, not a given");
            Assert.That(language.UnderstoodLanguages, Does.Not.Contain(English));
            Assert.That(language.DefaultLanguage, Is.EqualTo(Mandarin));
            Assert.That(language.CurrentLanguage, Is.EqualTo(Mandarin));
        });
    }

    [Test]
    public async Task EnglishTraitAddsEnglishButColonyLanguageStaysDefault()
    {
        HumanoidCharacterProfile profile = default!;
        await Server.WaitPost(() => profile = new HumanoidCharacterProfile()
            .WithTraitPreference(EnglishTrait, Server.ProtoMan));

        var language = await SpawnCharacter(Colonist, UppColony, profile);

        Assert.Multiple(() =>
        {
            Assert.That(language.SpokenLanguages, Does.Contain(English));
            Assert.That(language.SpokenLanguages, Does.Contain(Mandarin));
            Assert.That(language.DefaultLanguage, Is.EqualTo(Mandarin));
        });
    }

    [Test]
    public async Task CharacterWithNoOtherLanguageFallsBackToEnglish()
    {
        // no platoon selected and GOVFOR isn't a colony job, so nothing else gives this rifleman a language
        await Server.WaitPost(() => Server.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = null);
        var language = await SpawnCharacter(Rifleman, UppColony, new HumanoidCharacterProfile());

        Assert.Multiple(() =>
        {
            Assert.That(language.SpokenLanguages, Does.Contain(English));
            Assert.That(language.SpokenLanguages, Does.Not.Contain(Mandarin), "only colony jobs get the colony language");
            Assert.That(language.CurrentLanguage, Is.EqualTo(English));
        });
    }

    [Test]
    public async Task PlatoonsDeclareASpokenDefaultLanguage()
    {
        await Server.WaitAssertion(() =>
        {
            foreach (var platoon in Server.ProtoMan.EnumeratePrototypes<PlatoonPrototype>())
            {
                if (platoon.Languages.Count == 0)
                    continue;

                Assert.That(platoon.DefaultLanguage, Is.Not.Null, $"platoon {platoon.ID} lists languages but no defaultLanguage");
                Assert.That(Server.ProtoMan.HasIndex(platoon.DefaultLanguage!.Value), Is.True);
            }

            var upp = Server.ProtoMan.Index<PlatoonPrototype>("UPP");
            Assert.That(PlatoonLanguageSystem.GetDefaultLanguage(upp), Is.EqualTo(Mandarin));

            var lacn = Server.ProtoMan.Index<PlatoonPrototype>("LACN");
            Assert.That(PlatoonLanguageSystem.GetDefaultLanguage(lacn), Is.EqualTo(Portuguese));
        });
    }

    [Test]
    public async Task ClfDefaultsToColonyLanguage()
    {
        var language = await SpawnCharacter(ClfGuerilla, UppColony, new HumanoidCharacterProfile());

        Assert.Multiple(() =>
        {
            Assert.That(language.SpokenLanguages, Does.Contain(Mandarin), "the CLF speak their colony's language");
            Assert.That(language.DefaultLanguage, Is.EqualTo(Mandarin));
        });
    }

    [Test]
    public async Task ClfReinforcementGhostRoleDefaultsToColonyLanguage()
    {
        var map = await Pair.CreateTestMap();
        LanguageComponent language = default!;

        await Server.WaitPost(() =>
        {
            Assert.That(Server.System<AuRoundSystem>().SetPlanet(UppColony), Is.True);

            // third parties spawn as ghost-role bodies and never raise PlayerSpawnCompleteEvent
            var mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var ghostRole = SEntMan.EnsureComponent<GhostRoleComponent>(mob);
            ghostRole.JobProto = ClfReinforcement;
            var job = Server.ProtoMan.Index<JobPrototype>(ClfReinforcement);
            Assert.That(Server.System<RoundJobProfileSystem>().ApplyJobProfile(mob, job), Is.True);
            language = SEntMan.GetComponent<LanguageComponent>(mob);
        });

        await Pair.RunTicksSync(1);

        Assert.Multiple(() =>
        {
            Assert.That(language.SpokenLanguages, Does.Contain(Mandarin), "CLF reinforcements speak their colony's language");
            Assert.That(language.DefaultLanguage, Is.EqualTo(Mandarin));
        });
    }

    private async Task<LanguageComponent> SpawnCharacter(string job, string planet, HumanoidCharacterProfile profile)
    {
        var map = await Pair.CreateTestMap();
        LanguageComponent language = default!;

        await Server.WaitPost(() =>
        {
            Assert.That(Server.System<AuRoundSystem>().SetPlanet(planet), Is.True);

            var mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var ev = new PlayerSpawnCompleteEvent(mob, ServerSession!, job, false, true, 0, EntityUid.Invalid, profile);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ev);
            language = SEntMan.GetComponent<LanguageComponent>(mob);
        });

        await Pair.RunTicksSync(1);
        return language;
    }
}
