#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Chemistry.Effects.Positive;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Nutrition;

[TestFixture]
[TestOf(typeof(SatiationSystem))]
public sealed class CMUSatiationMigrationTest
{
    private static readonly ProtoId<SatiationTypePrototype> Hunger = SatiationSystem.Hunger;
    private static readonly ProtoId<SatiationTypePrototype> Thirst = SatiationSystem.Thirst;

    [Test]
    public async Task ForkMobParentsKeepTheirExactNutritionChannelsAndMaxima()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var satiation = entities.System<SatiationSystem>();

            var human = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var humanSatiation = entities.GetComponent<SatiationComponent>(human);
            AssertChannels(humanSatiation,
                (Hunger, "RMCHumanHunger"),
                (Thirst, "RMCHumanThirst"));
            Assert.Multiple(() =>
            {
                Assert.That(satiation.GetMaximumValue((human, humanSatiation), Hunger), Is.EqualTo(480));
                Assert.That(satiation.GetMaximumValue((human, humanSatiation), Thirst), Is.EqualTo(378));
            });

            satiation.SetValue((human, humanSatiation), Hunger, 50f);
            Assert.That(satiation.GetValueOrNull((human, humanSatiation), Hunger), Is.EqualTo(50),
                "SpawnHungryThirsty's historical Hunger=50 must remain valid below the 480 maximum.");

            var simpleMob = entities.SpawnEntity("CMMobSmallHostMonkey", MapCoordinates.Nullspace);
            var simpleMobSatiation = entities.GetComponent<SatiationComponent>(simpleMob);
            AssertChannels(simpleMobSatiation,
                (Hunger, "RMCSimpleMobHunger"),
                (Thirst, "RMCSimpleMobThirst"));
            Assert.Multiple(() =>
            {
                Assert.That(satiation.GetMaximumValue((simpleMob, simpleMobSatiation), Hunger), Is.EqualTo(100));
                Assert.That(satiation.GetMaximumValue((simpleMob, simpleMobSatiation), Thirst), Is.EqualTo(200));
            });

            var rodent = entities.SpawnEntity("RMCMobRat", MapCoordinates.Nullspace);
            var rodentSatiation = entities.GetComponent<SatiationComponent>(rodent);
            AssertChannels(rodentSatiation,
                (Hunger, "RMCRodentHunger"),
                (Thirst, "RMCRodentThirst"));
            Assert.Multiple(() =>
            {
                Assert.That(satiation.GetMaximumValue((rodent, rodentSatiation), Hunger), Is.EqualTo(35));
                Assert.That(satiation.GetMaximumValue((rodent, rodentSatiation), Thirst), Is.EqualTo(35));
                Assert.That(satiation.GetValueOrNull((rodent, rodentSatiation), Hunger), Is.EqualTo(25).Within(0.01));
                Assert.That(satiation.GetValueOrNull((rodent, rodentSatiation), Thirst), Is.EqualTo(25).Within(0.01));
            });

            var ape = entities.SpawnEntity("CMUMobApe", MapCoordinates.Nullspace);
            var apeSatiation = entities.GetComponent<SatiationComponent>(ape);
            AssertChannels(apeSatiation, (Hunger, "CMUApeHunger"));
            Assert.Multiple(() =>
            {
                Assert.That(satiation.GetMaximumValue((ape, apeSatiation), Hunger), Is.EqualTo(100));
                Assert.That(satiation.GetMaximumValue((ape, apeSatiation), Thirst), Is.Null,
                    "The ape deliberately removed Thirst before the Satiation migration.");
            });

            var trainingDummy = entities.SpawnEntity("RMCTrainingDummy", MapCoordinates.Nullspace);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<SatiationComponent>(trainingDummy), Is.False,
                    "The training-dummy removal registry must remove the combined component.");
                Assert.That(entities.HasComponent<SatiationSpeedModifierComponent>(trainingDummy), Is.False,
                    "The upstream satiation-effect dependency must not restore Satiation during MapInit.");
            });

            entities.DeleteEntity(human);
            entities.DeleteEntity(simpleMob);
            entities.DeleteEntity(rodent);
            entities.DeleteEntity(ape);
            entities.DeleteEntity(trainingDummy);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForkSatiationPrototypesPreserveLegacyThresholdsAndRates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            AssertSatiationPrototype(prototypes,
                "RMCHumanHunger",
                -0.1f,
                480,
                120,
                380,
                new Dictionary<string, int>
                {
                    ["Overfed"] = 480,
                    ["Okay"] = 380,
                    ["Peckish"] = 110,
                    ["Starving"] = 50,
                    ["Dead"] = 0,
                });
            var humanHunger = prototypes.Index<SatiationPrototype>("RMCHumanHunger");
            Assert.Multiple(() =>
            {
                Assert.That(humanHunger.ChangeModifiers["Overfed"], Is.EqualTo(1));
                Assert.That(humanHunger.ChangeModifiers["Okay"], Is.EqualTo(1));
                Assert.That(humanHunger.ChangeModifiers["Peckish"], Is.EqualTo(1));
                Assert.That(humanHunger.ChangeModifiers["Starving"], Is.EqualTo(1));
                Assert.That(humanHunger.ChangeModifiers["Dead"], Is.Zero);
            });

            AssertSatiationPrototype(prototypes,
                "RMCHumanThirst",
                -0.1f,
                378,
                108,
                277,
                new Dictionary<string, int>
                {
                    ["Overhydrated"] = 378,
                    ["Okay"] = 278,
                    ["Thirsty"] = 98,
                    ["Parched"] = 50,
                    ["Dead"] = 0,
                });
            AssertSatiationPrototype(prototypes,
                "RMCSimpleMobHunger",
                -0.05f,
                100,
                35,
                50,
                new Dictionary<string, int>
                {
                    ["Overfed"] = 100,
                    ["Okay"] = 50,
                    ["Peckish"] = 25,
                    ["Starving"] = 10,
                    ["Dead"] = 0,
                });
            AssertSatiationPrototype(prototypes,
                "RMCSimpleMobThirst",
                -0.04f,
                200,
                110,
                149,
                new Dictionary<string, int>
                {
                    ["Overhydrated"] = 200,
                    ["Okay"] = 150,
                    ["Thirsty"] = 100,
                    ["Parched"] = 10,
                    ["Dead"] = 0,
                });

            var human = prototypes.Index<EntityPrototype>("CMMobHuman");
            Assert.That(human.TryComp<SatiationSpeedModifierComponent>(out var speed, factory), Is.True);
            AssertSpeedThreshold(speed!, Hunger, "Peckish", 0.9f);
            AssertSpeedThreshold(speed, Hunger, "Starving", 0.72f);
            AssertSpeedThreshold(speed, Thirst, "Thirsty", 0.9f);
            AssertSpeedThreshold(speed, Thirst, "Parched", 0.72f);

            var synthRemovals = prototypes.Index<EntityPrototype>("RMCSynthRemoveComponents");
            Assert.That(synthRemovals.HasComp<SatiationComponent>(factory), Is.True,
                "Synthetic conversion must remove the combined Satiation component.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HemogenicKeepsInclusiveTwoHundredGateBelowRmcMaximum()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var satiationSystem = entities.System<SatiationSystem>();
            var effects = entities.System<SharedEntityEffectsSystem>();
            var human = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var satiation = entities.GetComponent<SatiationComponent>(human);
                var entity = new Entity<SatiationComponent>(human, satiation);
                var reagent = prototypes.Index<ReagentPrototype>("RMCIron");
                var hemogenic = reagent.Metabolisms!.Metabolisms.Values
                    .SelectMany(entry => entry.Effects)
                    .OfType<Hemogenic>()
                    .Single();

                Assert.That(satiationSystem.GetMaximumValue(entity, Hunger), Is.EqualTo(480));

                satiationSystem.SetValue(entity, Hunger, 199f);
                var belowGate = satiationSystem.GetValueOrNull(entity, Hunger)!.Value;
                ApplyHemogenic(effects, human, reagent, hemogenic);
                Assert.That(satiationSystem.GetValueOrNull(entity, Hunger),
                    Is.EqualTo(belowGate).Within(0.001),
                    "Hemogenic ran below the historical 200 Hunger gate.");

                satiationSystem.SetValue(entity, Hunger, 200f);
                var atGate = satiationSystem.GetValueOrNull(entity, Hunger)!.Value;
                ApplyHemogenic(effects, human, reagent, hemogenic);
                Assert.That(satiationSystem.GetValueOrNull(entity, Hunger),
                    Is.LessThan(atGate),
                    "Hemogenic did not run at the inclusive 200 Hunger boundary.");
            }
            finally
            {
                entities.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertChannels(
        SatiationComponent component,
        params (ProtoId<SatiationTypePrototype> Type, string Prototype)[] expected)
    {
        Assert.That(component.Satiations.Keys, Is.EquivalentTo(expected.Select(entry => entry.Type)));
        foreach (var (type, prototype) in expected)
            Assert.That(component.Satiations[type].Prototype.Id, Is.EqualTo(prototype), type.Id);
    }

    private static void AssertSatiationPrototype(
        IPrototypeManager prototypes,
        string id,
        float baseChangeRate,
        int maximumValue,
        int startingMinimum,
        int startingMaximum,
        IReadOnlyDictionary<string, int> thresholds)
    {
        var prototype = prototypes.Index<SatiationPrototype>(id);
        Assert.Multiple(() =>
        {
            Assert.That(prototype.BaseChangeRate, Is.EqualTo(baseChangeRate).Within(0.0001), id);
            Assert.That(prototype.MaximumValue, Is.EqualTo(maximumValue), id);
            Assert.That(prototype.StartingValueMinimum, Is.EqualTo(startingMinimum), id);
            Assert.That(prototype.StartingValueMaximum, Is.EqualTo(startingMaximum), id);
            Assert.That(prototype.Thresholds, Is.EqualTo(thresholds), id);
        });
    }

    private static void AssertSpeedThreshold(
        SatiationSpeedModifierComponent component,
        ProtoId<SatiationTypePrototype> type,
        string threshold,
        float expected)
    {
        Assert.That(component.Satiations[type].Thresholds.TryGetValue(threshold, out var value), Is.True,
            $"{type.Id}:{threshold}");
        Assert.That(value, Is.EqualTo(expected).Within(0.0001), $"{type.Id}:{threshold}");
    }

    private static void ApplyHemogenic(
        SharedEntityEffectsSystem system,
        EntityUid target,
        ReagentPrototype reagent,
        Hemogenic effect)
    {
        var source = new Solution(reagent.ID, 1);
        var context = new ReagentEffectContext(
            reagent,
            source,
            null,
            null,
            new ReagentQuantity(reagent.ID, 1),
            "Bloodstream",
            null,
            ReagentEffectOrigin.Metabolism);
        Assert.That(system.TryApplyEffect(target, effect, reagentContext: context), Is.True);
    }
}

#pragma warning restore RA0002
