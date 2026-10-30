using System.Linq;
using Content.Server.CMU14.Yautja;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Yautja;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Item.ItemToggle;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUYautjaExperimentTest
{
    [Test]
    public async Task MovingBracerIntoAHandCancelsWorkWithoutSpendingTheSample()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid user = default, bracer = default, screwdriver = default;
        ProtoId<CMUYautjaExperimentPrototype>[] challenge = [];
        string clues = "";
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.System<SkillsSystem>().SetSkill(user, "RMCSkillResearch", 1);
            entities.EnsureComponent<YautjaTechAuthorizedComponent>(user);
            bracer = entities.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            screwdriver = entities.SpawnEntity("Screwdriver", map.GridCoords);
            var beaker = entities.SpawnEntity("Beaker", map.GridCoords);
            var solutions = entities.System<SharedSolutionContainerSystem>();
            solutions.TryGetDrainableSolution(beaker, out var source, out _);
            solutions.TryAddReagent(source!.Value, "Water", 5);
            var hands = entities.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, beaker), Is.True);
            var add = new InteractUsingEvent(user, beaker, bracer, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(bracer, add);
            Assert.That(add.Handled, Is.True);
            hands.TryDrop(user, beaker);
            hands.TryPickupAnyHand(user, screwdriver);
            var research = entities.System<CMUYautjaExperimentSystem>();
            var comp = entities.GetComponent<CMUYautjaExperimentComponent>(bracer);
            challenge = comp.RequiredExperiments.ToArray();
            Assert.That(challenge, Has.Length.EqualTo(5));
            clues = research.ChallengeClues(comp);
            Assert.That(research.StartExperiment((bracer, comp), user, screwdriver), Is.True);
            Assert.That(research.StartExperiment((bracer, comp), user, screwdriver), Is.False,
                "Two operations cannot consume the same sample well.");
            hands.TryPickupAnyHand(user, bracer);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(9));
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var comp = entities.GetComponent<CMUYautjaExperimentComponent>(bracer);
            Assert.That(comp.Discoveries, Is.Empty);
            Assert.That(comp.Operator, Is.Null);
            Assert.That(comp.RequiredExperiments, Is.EquivalentTo(challenge), "Picking up a bracer must not reroll its challenge.");
            Assert.That(entities.System<CMUYautjaExperimentSystem>().ChallengeClues(comp), Is.EqualTo(clues),
                "An interrupted attempt must preserve the clues and cannot release a seal.");
            entities.System<SharedSolutionContainerSystem>().TryGetSolution(bracer, CMUYautjaExperimentSystem.SampleWell, out _, out var well);
            Assert.That(well!.GetTotalPrototypeQuantity("Water"), Is.EqualTo((FixedPoint2) 5));
            Assert.That(entities.System<CMUYautjaExperimentSystem>().StartExperiment((bracer, comp), user, screwdriver), Is.False,
                "Held bracers cannot be experimented on.");
            var hands = entities.System<SharedHandsSystem>();
            hands.TryDrop(user, bracer);
            hands.IsHolding(user, screwdriver, out var hand);
            hands.TrySetActiveHand(user, hand);
            Assert.That(entities.System<CMUYautjaExperimentSystem>().StartExperiment((bracer, comp), user, screwdriver), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(9));
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<CMUYautjaExperimentComponent>(bracer).Discoveries,
                Does.Contain(new ProtoId<CMUYautjaExperimentPrototype>("CMUYautjaDiagnostic")));
            entities.System<SharedSolutionContainerSystem>().TryGetSolution(bracer, CMUYautjaExperimentSystem.SampleWell, out _, out var well);
            Assert.That(well!.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.Zero));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FiveChosenExperimentsUnlockRealGearWithoutPrerequisitesOrRepeatRewards(bool requireRoot)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid user = default, bracer = default;
        var tools = new Dictionary<string, EntityUid>();
        // Exercise both a challenge containing Root and one where Root must not bypass the five seals.
        var required = new[] { "CMUYautjaPlasma", "CMUYautjaStatic", "CMUYautjaDecoy", "CMUYautjaContainment",
            requireRoot ? "CMUYautjaRoot" : "CMUYautjaVeil" };
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.System<SkillsSystem>().SetSkill(user, "RMCSkillResearch", 1);
            bracer = entities.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var comp = entities.GetComponent<CMUYautjaExperimentComponent>(bracer);
            comp.RequiredExperiments = required.Select(id => new ProtoId<CMUYautjaExperimentPrototype>(id)).ToHashSet();
            foreach (var (quality, prototype) in new[] { ("Screwing", "Screwdriver"), ("Pulsing", "Multitool"),
                         ("Anchoring", "Wrench"), ("Welding", "RMCWelderAdmin") })
                tools[quality] = entities.SpawnEntity(prototype, map.GridCoords);
            entities.System<ItemToggleSystem>().TryActivate((tools["Welding"], null), user);
        });

        // Real chemistry, tools and DoAfters: no prerequisite discoveries or extra cooling experiments.
        var order = new List<string> { "Root", "Diagnostic", "Diagnostic", "Plasma", "Static", "Decoy", "Containment" };
        if (!requireRoot)
            order.Add("Veil");
        var rewarded = new HashSet<string>();
        foreach (var name in order)
        {
            var id = "CMUYautja" + name;
            TimeSpan duration = default;
            var beforeCredits = 0;
            var expectedReward = 0;
            await server.WaitAssertion(() =>
            {
                var entities = server.EntMan;
                var recipe = server.ProtoMan.Index<CMUYautjaExperimentPrototype>(id);
                duration = recipe.Duration;
                var hands = entities.System<SharedHandsSystem>();
                if (hands.GetActiveItem(user) is { } previous)
                    hands.TryDrop(user, previous);
                var tool = tools[recipe.Tool];
                Assert.That(hands.TryPickupAnyHand(user, tool), Is.True);
                var solutions = entities.System<SharedSolutionContainerSystem>();
                solutions.TryGetSolution(bracer, CMUYautjaExperimentSystem.SampleWell, out var well, out _);
                solutions.RemoveAllSolution(well!.Value);
                foreach (var (reagent, amount) in recipe.Reagents)
                    solutions.TryAddReagent(well.Value, reagent, amount);
                var research = entities.System<CMUYautjaExperimentSystem>();
                var lab = entities.System<SharedResearchDataTerminalSystem>();
                beforeCredits = lab.GetCredits("corporate");
                expectedReward = rewarded.Add(id) ? recipe.ResearchReward : 0;
                Assert.That(research.SelectExperiment(bracer, user, id), Is.True, id);
                var useTool = new InteractUsingEvent(user, tool, bracer, map.GridCoords);
                entities.EventBus.RaiseLocalEvent(bracer, useTool);
                Assert.That(entities.GetComponent<CMUYautjaExperimentComponent>(bracer).Operator, Is.EqualTo(user), id);
                Assert.That(entities.HasComponent<YautjaTechAuthorizedComponent>(user), Is.False,
                    "Authorization must wait for the fifth chosen operation to finish.");
            });
            await pair.RunTicksSync(pair.SecondsToTicks((float) duration.TotalSeconds + .3f));
            await server.WaitAssertion(() =>
            {
                var entities = server.EntMan;
                Assert.That(entities.GetComponent<CMUYautjaExperimentComponent>(bracer).Discoveries,
                    Does.Contain(new ProtoId<CMUYautjaExperimentPrototype>(id)), id);
                Assert.That(entities.System<SharedResearchDataTerminalSystem>().GetCredits("corporate"),
                    Is.EqualTo(beforeCredits + expectedReward), "Only a new discovery earns research credit.");
                Assert.That(entities.EntityQuery<CMUYautjaEffectComponent>().Any(effect => effect.Experiment == id), Is.True,
                    "A completed procedure must publish its visible result to nearby clients.");
                var complete = required.All(rewarded.Contains);
                Assert.That(entities.GetComponent<CMUYautjaExperimentComponent>(bracer).Unlocked, Is.EqualTo(complete),
                    "Only the five chosen procedures count; Root alone, unrelated work and repeats cannot unlock the bracer.");
                Assert.That(entities.HasComponent<CMUYautjaResearchOperatorComponent>(user), Is.EqualTo(complete));
            });
        }
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.HasComponent<YautjaTechAuthorizedComponent>(user), Is.True);
            Assert.That(entities.HasComponent<CMUYautjaResearchOperatorComponent>(user), Is.True);
            Assert.That(entities.System<InventorySystem>().TryEquip(user, bracer, "gloves", silent: true), Is.True);
            var power = entities.GetComponent<YautjaBracerComponent>(bracer);
            var cloak = new YautjaToggleCloakActionEvent { Performer = user, Action = power.ToggleCloakAction!.Value };
            entities.EventBus.RaiseLocalEvent(bracer, cloak);
            Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(user), Is.True,
                "The stolen interface must activate the real worn bracer, not just add an authorization marker.");
            entities.System<YautjaCloakSystem>().ForceDecloak(user);
            var disc = entities.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            if (hands.GetActiveItem(user) is { } held)
                hands.TryDrop(user, held);
            Assert.That(hands.TryPickupAnyHand(user, disc), Is.True);
            var bind = new Content.Shared.Interaction.Events.UseInHandEvent(user);
            entities.EventBus.RaiseLocalEvent(disc, bind);
            Assert.That(entities.System<ItemToggleSystem>().TryActivate((disc, null), user), Is.True,
                "A recovered disc must accept the reconstructed operator instead of entering rogue mode.");
            Assert.That(entities.GetComponent<YautjaSmartDiscComponent>(disc).RogueTarget, Is.Null);
        });
        await pair.CleanReturnAsync();
    }
}
