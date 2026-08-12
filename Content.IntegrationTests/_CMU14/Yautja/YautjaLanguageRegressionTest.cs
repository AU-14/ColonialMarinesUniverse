using System.Collections.Generic;
using System.Linq;
using Content.Server._CMU14.Yautja;
using Content.Server._RMC14.Language.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Robust.Server;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaLanguageRegressionTest
{
    private static readonly ProtoId<LanguagePrototype> YautjaLanguage = "Yautja";
    private static readonly ProtoId<LanguagePrototype> RussianLanguage = "Russian";

    [Test]
    public async Task HellhoundUnderstandsYautja()
    {
        using var server = CreateServer();
        await server.WaitIdleAsync();
        EntityUid hellhound = default;

        await server.WaitPost(() =>
        {
            hellhound = server.EntMan.SpawnEntity("CMUMobYautjaHellhound", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            var languages = server.EntMan.GetComponent<LanguageComponent>(hellhound);
            Assert.That(languages.UnderstoodLanguages, Does.Contain(YautjaLanguage));
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(hellhound));
    }

    [Test]
    public async Task HumanEnthrallPreservesLanguagesAndAddsYautja()
    {
        using var server = CreateServer();
        await server.WaitIdleAsync();
        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.EnsureComponent<YautjaComponent>(hunter);

            Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

            var language = entMan.System<LanguageSystem>();
            language.AddLanguage(human, RussianLanguage);
            var before = entMan.GetComponent<LanguageComponent>(human);
            var spokenBefore = before.SpokenLanguages.ToHashSet();
            var understoodBefore = before.UnderstoodLanguages.ToHashSet();

            Assert.That(
                entMan.System<YautjaMarkSystem>().TryMark(
                    (bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)),
                    hunter,
                    human,
                    YautjaMarkKind.Thrall,
                    "language regression"),
                Is.True);

            var after = entMan.GetComponent<LanguageComponent>(human);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<YautjaThrallComponent>(human), Is.True);
                Assert.That(after.SpokenLanguages, Is.SupersetOf(spokenBefore));
                Assert.That(after.UnderstoodLanguages, Is.SupersetOf(understoodBefore));
                Assert.That(after.SpokenLanguages, Does.Contain(YautjaLanguage));
                Assert.That(after.UnderstoodLanguages, Does.Contain(YautjaLanguage));
            });
        });

        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(human);
            server.EntMan.DeleteEntity(hunter);
            server.EntMan.DeleteEntity(bracer);
        });
    }

    [Test]
    public async Task YautjaItemInteractionsPreserveHumanLanguages()
    {
        using var server = CreateServer();
        await server.WaitIdleAsync();
        EntityUid human = default;
        EntityUid bracer = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            entMan.System<LanguageSystem>().AddLanguage(human, RussianLanguage);

            var component = entMan.GetComponent<LanguageComponent>(human);
            var spokenBefore = component.SpokenLanguages.ToHashSet();
            var understoodBefore = component.UnderstoodLanguages.ToHashSet();
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();

            Assert.That(hands.TryPickupAnyHand(human, bracer, checkActionBlocker: false), Is.True);
            AssertLanguagesUnchanged(entMan, human, spokenBefore, understoodBefore);

            entMan.EventBus.RaiseLocalEvent(bracer, new UseInHandEvent(human));
            AssertLanguagesUnchanged(entMan, human, spokenBefore, understoodBefore);

            Assert.That(inventory.TryUnequip(human, "gloves", silent: true, force: true), Is.True);
            AssertLanguagesUnchanged(entMan, human, spokenBefore, understoodBefore);
        });

        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(human);
            server.EntMan.DeleteEntity(bracer);
        });
    }

    private static void AssertLanguagesUnchanged(
        IEntityManager entMan,
        EntityUid human,
        IReadOnlySet<ProtoId<LanguagePrototype>> spoken,
        IReadOnlySet<ProtoId<LanguagePrototype>> understood)
    {
        var actual = entMan.GetComponent<LanguageComponent>(human);
        Assert.Multiple(() =>
        {
            Assert.That(actual.SpokenLanguages, Is.SupersetOf(spoken));
            Assert.That(actual.UnderstoodLanguages, Is.SupersetOf(understood));
        });
    }

    private static RobustIntegrationTest.ServerIntegrationInstance CreateServer()
    {
        return new RobustIntegrationTest.ServerIntegrationInstance(new RobustIntegrationTest.ServerIntegrationOptions
        {
            ContentStart = true,
            FailureLogLevel = LogLevel.Fatal,
            Options = new ServerOptions
            {
                LoadConfigAndUserData = false,
                LoadContentResources = true,
            },
            ContentAssemblies =
            [
                typeof(Shared.Entry.EntryPoint).Assembly,
                typeof(Server.Entry.EntryPoint).Assembly,
            ],
        });
    }
}
