using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffect;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Localization;

[TestFixture]
public sealed class RMCLocalizationRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private static readonly (string Reagent, int Threshold, int Seconds)[] UnconsciousEffects =
    [
        ("RMCSpaceDrugs", 50, 40),
        ("RMCPsilocybin", 50, 40),
        ("RMCMindbreakerToxin", 50, 40),
        ("RMCChloralhydrate", 15, 3),
    ];

    private static readonly string[] EqualHealthChangeReagents =
    [
        "AU14Albuterol",
        "CMCryoxadone",
        "CMClonexadone",
        "RMCRussianRed",
    ];

    [Test]
    public async Task LegacyUnconsciousEffectsHaveLocalizedGuidebookText()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var systems = Server.ResolveDependency<IEntitySystemManager>();

            Assert.Multiple(() =>
            {
                foreach (var (id, threshold, seconds) in UnconsciousEffects)
                {
                    var reagent = prototypes.Index<ReagentPrototype>(id);
                    var effect = reagent.Metabolisms!.Metabolisms.Values
                        .SelectMany(metabolism => metabolism.Effects)
                        .OfType<GenericStatusEffect>()
                        .Single(candidate => candidate.Key == "Unconscious");
                    var condition = effect.Conditions!.OfType<ReagentCondition>().Single();
                    var guidebook = effect.EntityEffectGuidebookText(prototypes, systems);

                    Assert.That(effect.Component, Is.EqualTo("RMCUnconscious"), id);
                    Assert.That(effect.Type, Is.EqualTo(StatusEffectMetabolismType.Update), id);
                    Assert.That(effect.Time, Is.EqualTo(TimeSpan.FromSeconds(seconds)), id);
                    Assert.That(condition.Min, Is.EqualTo(FixedPoint2.New(threshold)), id);
                    Assert.That(guidebook, Does.Contain("unconsciousness").IgnoreCase, id);
                    Assert.That(guidebook, Does.Not.Contain("entity-effect-status-effect-Unconscious"), id);
                }
            });
        });
    }

    [Test]
    public async Task EqualHealthChangeEffectsUseCurrentLocalizedGuidebookSelectors()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var systems = Server.ResolveDependency<IEntitySystemManager>();

            Assert.Multiple(() =>
            {
                foreach (var id in EqualHealthChangeReagents)
                {
                    var reagent = prototypes.Index<ReagentPrototype>(id);
                    var effect = reagent.Metabolisms!.Metabolisms.Values
                        .SelectMany(metabolism => metabolism.Effects)
                        .OfType<EqualHealthChange>()
                        .Single();
                    var guidebook = effect.EntityEffectGuidebookText(prototypes, systems);

                    Assert.That(guidebook, Does.Contain("heal").IgnoreCase, id);
                    Assert.That(guidebook, Does.Not.Contain("entity-effect-guidebook-health-change"), id);
                    Assert.That(guidebook, Does.Not.Contain("reagent-effect-guidebook-health-change"), id);
                }

                Assert.That(Guidebook([("Brute", -1)]), Does.StartWith("Heals"));
                Assert.That(Guidebook([("Brute", 1)]), Does.StartWith("Deals"));
                Assert.That(Guidebook([("Brute", -1), ("Burn", 1)]), Does.StartWith("Modifies health by"));
            });

            string Guidebook(List<(ProtoId<DamageGroupPrototype> Group, FixedPoint2 Amount)> damage)
            {
                var effect = new EqualHealthChange { Damage = damage };
                return effect.EntityEffectGuidebookText(prototypes, systems)!;
            }
        });
    }

    [Test]
    public async Task RmcLightCommandsShareLocalizedDescriptionAndCompleteHelp()
    {
        await Server.WaitAssertion(() =>
        {
            var commands = Server.ResolveDependency<IConsoleHost>().AvailableCommands;
            Assert.That(commands.ContainsKey("rmclight"), Is.True);
            Assert.That(commands.ContainsKey("rmclightsequence"), Is.True);

            var light = commands["rmclight"];
            var sequence = commands["rmclightsequence"];
            Assert.Multiple(() =>
            {
                Assert.That(light.Description, Is.EqualTo(sequence.Description));
                Assert.That(light.Description, Does.Contain("ambient light").IgnoreCase);
                Assert.That(light.Description, Does.Not.Contain("cmd-rmclight-desc"));
                Assert.That(light.Help, Is.EqualTo(sequence.Help));
                Assert.That(light.Help, Does.Contain("rmclight <gridUid> <color|null> [durationSeconds]"));
                Assert.That(light.Help, Does.Contain("rmclightsequence <gridUid> <dataset|null> [durationSeconds]"));
                Assert.That(light.Help, Does.Not.Contain("cmd-rmclight-help"));
            });
        });
    }
}
