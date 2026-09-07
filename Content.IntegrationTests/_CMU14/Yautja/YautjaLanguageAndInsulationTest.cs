using Content.Server._RMC14.Language.Systems;
using Content.Server.Electrocution;
using Content.Server.Humanoid.Systems;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaLanguageAndInsulationTest
{
    private static readonly ProtoId<LanguagePrototype> XenoLanguage = "Xeno";
    private static readonly ProtoId<LanguagePrototype> YautjaLanguage = "Yautja";

    private static readonly string[] RegularLoadouts =
    {
        "CMUYautjaHunterGear",
        "CMUYautjaHunterGearBronze",
        "CMUYautjaHunterGearSilver",
        "CMUYautjaHunterGearCrimson",
        "CMUYautjaHunterGearBone",
    };

    [Test]
    public async Task DirectAndRandomYautjaKeepNativeXenoWithoutTranslatorGear()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var language = entMan.System<LanguageSystem>();
            var randomHumanoid = entMan.System<RandomHumanoidSystem>();
            var directHunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var randomHunter = randomHumanoid.SpawnRandomHumanoid(
                "CMUYautjaHunter",
                EntityCoordinates.Invalid,
                string.Empty);

            try
            {
                foreach (var hunter in new[] { directHunter, randomHunter })
                {
                    AssertNativeLanguages(entMan, language, hunter);

                    Assert.That(inventory.TryUnequip(hunter, "mask", force: true), Is.True);
                    Assert.That(inventory.TryUnequip(hunter, "gloves", force: true), Is.True);
                    language.UpdateEntityLanguages(hunter);

                    AssertNativeLanguages(entMan, language, hunter);
                }
            }
            finally
            {
                entMan.DeleteEntity(directHunter);
                entMan.DeleteEntity(randomHunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrdinaryHumanDoesNotGainYautjaOrXeno()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var language = entMan.System<LanguageSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(language.CanSpeak(human, YautjaLanguage), Is.False);
                    Assert.That(language.CanUnderstand(human, YautjaLanguage), Is.False);
                    Assert.That(language.CanSpeak(human, XenoLanguage), Is.False);
                    Assert.That(language.CanUnderstand(human, XenoLanguage), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(RegularLoadouts))]
    public async Task RegularLoadoutBracerInsulatesOnlyWhileWorn(string loadoutId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var spawning = entMan.System<StationSpawningSystem>();
            var electrocution = entMan.System<ElectrocutionSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gear = server.ProtoMan.Index<StartingGearPrototype>(loadoutId);

            try
            {
                spawning.EquipStartingGear(hunter, gear);
                Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
                Assert.That(bracer, Is.Not.Null);
                Assert.That(PrototypeId(entMan, bracer!.Value), Is.EqualTo("CMUYautjaBracer"));
                Assert.That(entMan.HasComponent<YautjaBracerComponent>(bracer.Value), Is.True);
                var gearContainer = entMan.GetComponent<YautjaGearContainerComponent>(bracer.Value);
                Assert.That(gearContainer.Container, Is.Not.Null,
                    "adding insulation must not disturb the bracer's stored gear container");

                Assert.That(TryShock(electrocution, hunter), Is.False,
                    "a bracer in the gloves slot must fully insulate its wearer");

                Assert.That(inventory.TryUnequip(hunter, "gloves", out var removed, force: true), Is.True);
                Assert.That(removed, Is.EqualTo(bracer));
                Assert.That(TryShock(electrocution, hunter), Is.True,
                    "removing the bracer must restore ordinary electrocution");

                Assert.That(hands.TryPickupAnyHand(hunter, bracer.Value, checkActionBlocker: false, animate: false),
                    Is.True);
                Assert.That(TryShock(electrocution, hunter), Is.True,
                    "holding the bracer must not insulate the holder");
            }
            finally
            {
                entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InsulationBypassStillElectrocutesWornBracerUser()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var electrocution = entMan.System<ElectrocutionSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(TryShock(electrocution, hunter, ignoreInsulation: true), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertNativeLanguages(
        IEntityManager entMan,
        LanguageSystem language,
        EntityUid hunter)
    {
        var component = entMan.GetComponent<LanguageComponent>(hunter);

        Assert.Multiple(() =>
        {
            Assert.That(language.CanSpeak(hunter, XenoLanguage), Is.True);
            Assert.That(language.CanUnderstand(hunter, XenoLanguage), Is.True);
            Assert.That(language.CanSpeak(hunter, YautjaLanguage), Is.True);
            Assert.That(language.CanUnderstand(hunter, YautjaLanguage), Is.True);
            Assert.That(language.GetCurrentLanguage(hunter), Is.EqualTo(YautjaLanguage));
            Assert.That(component.DefaultLanguage, Is.EqualTo(YautjaLanguage));
        });
    }

    private static bool TryShock(
        ElectrocutionSystem electrocution,
        EntityUid target,
        bool ignoreInsulation = false)
    {
        return electrocution.TryDoElectrocution(
            target,
            null,
            10,
            TimeSpan.FromSeconds(1),
            refresh: true,
            ignoreInsulation: ignoreInsulation);
    }

    private static string? PrototypeId(IEntityManager entMan, EntityUid uid)
    {
        return entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
    }
}
