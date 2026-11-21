using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Language;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CMU14.Ambassador;
using Content.Shared.CMU14.Threats;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.ThirdParty;

[TestFixture]
public sealed class CMUThirdPartyCallerTest : GameTest
{
    [Test]
    public async Task GhostRoleNamesWhoCalledThePartyIn()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var ghostRole = SEntMan.EnsureComponent<GhostRoleComponent>(mob);
            ghostRole.RoleDescription = "A test role.";

            Server.System<CMUThirdPartyCallerSystem>().ApplyCaller(mob, new ThirdPartyCaller("Ivan Petrov", "UPP"));

            var caller = SEntMan.GetComponent<CMUThirdPartyCallerComponent>(mob);
            Assert.Multiple(() =>
            {
                Assert.That(caller.CallerName, Is.EqualTo("Ivan Petrov"));
                Assert.That(caller.CallerFaction, Is.EqualTo("UPP"));
                Assert.That(ghostRole.RoleDescription, Does.StartWith("A test role."), "the original description stays");
                Assert.That(ghostRole.RoleDescription, Does.Contain("Ivan Petrov"));
                Assert.That(ghostRole.RoleDescription, Does.Contain("UPP"));
            });
        });
    }

    [Test]
    public async Task CallerLanguagesAreLearnedAndCanBecomeDefault()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            ProtoId<LanguagePrototype>[] upp = ["Mandarin", "Russian"];

            Server.System<CMUCharacterLanguageSystem>().AddLanguages(mob, upp, upp[0]);

            var language = SEntMan.GetComponent<LanguageComponent>(mob);
            Assert.Multiple(() =>
            {
                Assert.That(language.SpokenLanguages, Does.Contain(upp[0]));
                Assert.That(language.SpokenLanguages, Does.Contain(upp[1]));
                Assert.That(language.UnderstoodLanguages, Does.Contain(upp[1]));
                Assert.That(language.DefaultLanguage, Is.EqualTo(upp[0]));
            });
        });
    }

    [Test]
    public async Task DelegationsAreOnTheirConsoles()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            AmbassadorConsoleComponent Console(string id) =>
                Server.ProtoMan.Index<EntityPrototype>(id).TryComp(out AmbassadorConsoleComponent? comp, factory)
                    ? comp
                    : throw new AssertionException($"{id} has no AmbassadorConsole");

            foreach (var id in new[] { "AU14AmbassadorConsoleUPP", "AU14AmbassadorConsoleUA", "AU14AmbassadorConsoleTWE", "AU14AmbassadorConsoleIS", "AU14AmbassadorConsoleCCA" })
            {
                var console = Console(id);
                Assert.That(console.CallableParties, Does.ContainKey("CMUEmbassyStaff"), id);
                Assert.That(console.Languages, Is.Not.Empty, $"{id} needs languages for parties that speak their caller's language");

                // every call-in on the console has to be a real third party, or the button does nothing
                foreach (var party in console.CallableParties.Keys)
                    Assert.That(Server.ProtoMan.HasIndex<ThirdPartyPrototype>(party), Is.True, $"{id} lists unknown third party {party}");
            }
        });
    }
}
