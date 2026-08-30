#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.CMU14.Chemistry.Research;
using Content.Server.CMU14.Chemistry.HydroTrayEffects;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Chemistry.Effects.Positive;
using Content.Shared.CMU14.Chemistry.Effects.Special;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Traits.NicotineAddiction;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Chemistry;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects.Negative;
using Content.Shared._RMC14.Chemistry.Effects.Positive;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Body.Part;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Metabolism;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
public sealed class BeneficialChemicalPropertyPrototypeTest
{
    private const string TestReagent = "CMUTestAllBeneficialChemicalProperties";
    private const string DefibrillatingTestReagent = "CMUTestLevelSixDefibrillating";
    private const string InorganicTarget = "CMUTestRepairingInorganicTarget";
    private const string OrganicTarget = "CMUTestRepairingOrganicTarget";
    private const string XenoTarget = "CMUTestRepairingXenoTarget";

    private static readonly string[] PropertyIds =
    [
        "Antitoxic",
        "Anticorrosive",
        "Neogenetic",
        "Repairing",
        "Hemogenic",
        "Yautjahemogenic",
        "Hemostatic",
        "Nervestimulating",
        "Musclestimulating",
        "Painkilling",
        "Hepatopeutic",
        "Nephropeutic",
        "Pneumopeutic",
        "Oculopeutic",
        "Cardiopeutic",
        "Neuropeutic",
        "Bonemending",
        "Fluxing",
        "Neurocryogenic",
        "Antiparasitic",
        "Electrogenetic",
        "Defibrillating",
        "Hyperdensificating",
        "Neuroshielding",
        "Antiaddictive",
    ];

    private static readonly string ReagentPrototype = $$"""
        - type: reagent
          id: {{TestReagent}}
          name: all beneficial property test reagent
          desc: all beneficial property test reagent
          physicalDesc: reagent-physical-desc-translucent
          color: "#ffffff"
          worksOnTheDead: true
          overdose: 10
          criticalOverdose: 20
          metabolisms:
            Bloodstream:
              metabolismRate: 0.1
              effects:
        {{string.Join('\n', PropertyIds.Select(id => $"      - !type:{id}\n        potency: 2"))}}

        - type: reagent
          id: {{DefibrillatingTestReagent}}
          name: level six defibrillating test reagent
          desc: level six defibrillating test reagent
          physicalDesc: reagent-physical-desc-translucent
          color: "#ffffff"
          worksOnTheDead: true
          overdose: 35
          criticalOverdose: 40
          metabolisms:
            Bloodstream:
              metabolismRate: 0.1
              effects:
              - !type:Defibrillating
                potency: 6

        - type: entity
          id: {{InorganicTarget}}
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: StructuralInorganic

        - type: entity
          id: {{OrganicTarget}}
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: Biological

        - type: entity
          id: {{XenoTarget}}
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: StructuralInorganic
          - type: RepairableXenoStructure
            plasmaCost: 1
        """;

    [Test]
    public async Task AdminContractMaterializerExposesEveryBeneficialProperty()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var console = entities.SpawnEntity("CMUAdminChemicalContractConsole", MapCoordinates.Nullspace);
            var contract = entities.SpawnEntity("CMUAdminChemicalContract", MapCoordinates.Nullspace);
            try
            {
                var component = entities.GetComponent<AdminChemicalContractConsoleComponent>(console);
                Assert.Multiple(() =>
                {
                    Assert.That(component.AvailableProperties.Select(property => property.Id),
                        Is.EquivalentTo(PropertyIds));
                    Assert.That(component.AvailableProperties, Has.Count.EqualTo(PropertyIds.Length));
                    Assert.That(component.OutputAmount, Is.EqualTo((FixedPoint2)30));
                    Assert.That(entities.HasComponent<AdminChemicalContractPaperComponent>(contract), Is.True);
                    Assert.That(entities.HasComponent<ResearchReportComponent>(contract), Is.True);
                });
            }
            finally
            {
                entities.DeleteEntity(console);
                entities.DeleteEntity(contract);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdminContractRegistersAndMaterializesItsGeneratedChemical()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var console = entities.SpawnEntity("CMUAdminChemicalContractConsole", MapCoordinates.Nullspace);
            EntityUid? contract = null;
            EntityUid? vial = null;

            try
            {
                var data = new GeneratedReagentData
                {
                    ID = "TAU-ADMIN-INTEGRATION-TEST",
                    Name = "Adminium",
                    Class = ReagentClass.Ultra,
                    GenTier = 3,
                    RecipeHint = "CMInaprovaline",
                    Effects = new Dictionary<string, int>
                    {
                        ["Antitoxic"] = 2,
                        ["Hemogenic"] = 3,
                        ["Defibrillating"] = 4,
                    },
                };

                entities.System<ServerReagentGeneratorSystem>().GenerateStats(ref data, true);

                contract = entities.System<ServerResearchDataTerminalSystem>()
                    .IssueAdminContract(console, data);
                Assert.That(contract, Is.Not.Null);

                var report = entities.GetComponent<ResearchReportComponent>(contract!.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(report.Valid, Is.True);
                    Assert.That(report.Completed, Is.True);
                    Assert.That(report.Data, Is.Not.Null);
                    Assert.That(report.Data!.Value.Recipe, Is.Not.Empty);
                    Assert.That(report.Data.Value.Effects, Is.EqualTo(data.Effects));
                    Assert.That(server.ResolveDependency<IPrototypeManager>().HasIndex<ReagentPrototype>(data.ID), Is.True);
                    Assert.That(server.ResolveDependency<IPrototypeManager>().Index<ReagentPrototype>(data.ID).WorksOnTheDead,
                        Is.True);
                });

                var consoleComponent = entities.GetComponent<AdminChemicalContractConsoleComponent>(console);
                Assert.That(
                    entities.System<AdminChemicalContractConsoleSystem>().TryMaterializeContract(
                        (console, consoleComponent),
                        contract.Value,
                        out vial,
                        out var materializedData),
                    Is.True);
                Assert.That(materializedData.ID, Is.EqualTo(data.ID));

                var solutions = entities.System<SharedSolutionContainerSystem>();
                Assert.That(vial, Is.Not.Null);
                Assert.That(entities.HasComponent<VialComponent>(vial), Is.True);
                Assert.That(solutions.TryGetSolution(vial!.Value, "beaker", out var solution), Is.True);
                Assert.That(solution!.Value.Comp.Solution.GetTotalPrototypeQuantity(data.ID),
                    Is.EqualTo((FixedPoint2)30));
            }
            finally
            {
                if (vial is { } vialEntity && entities.EntityExists(vialEntity))
                    entities.DeleteEntity(vialEntity);
                if (contract is { } contractEntity && entities.EntityExists(contractEntity))
                    entities.DeleteEntity(contractEntity);
                entities.DeleteEntity(console);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllPropertiesDeserializeAndHaveRealGuidebookDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([ReagentPrototype]);

        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var reflection = pair.Server.ResolveDependency<IReflectionManager>();
            var systems = pair.Server.ResolveDependency<IEntitySystemManager>();
            var reagent = prototypes.Index<ReagentPrototype>(TestReagent);
            var effects = reagent.Metabolisms!.Metabolisms.Values
                .SelectMany(entry => entry.Effects)
                .ToArray();

            Assert.That(effects, Has.Length.EqualTo(PropertyIds.Length));
            Assert.That(reagent.WorksOnTheDead, Is.True);

            Assert.Multiple(() =>
            {
                foreach (var id in PropertyIds)
                {
                    var property = prototypes.Index<ReagentPropertyPrototype>(id);
                    Assert.That(reflection.TryLooseGetType(property.EffectName, out var effectType), Is.True,
                        $"{id} has no resolvable effect type named {property.EffectName}.");
                    Assert.That(typeof(RMCChemicalEffect).IsAssignableFrom(effectType!), Is.True,
                        $"{id} does not resolve to a chemical effect.");

                    var effect = effects.Single(candidate => candidate.GetType() == effectType);
                    Assert.That(effect, Is.InstanceOf<EntityEffect>());

                    var chemical = (RMCChemicalEffect)effect;
                    var guidebook = chemical.EntityEffectGuidebookText(prototypes, systems);
                    Assert.That(guidebook, Is.Not.Null.And.Not.Empty, $"{id} has no guidebook description.");
                    Assert.That(guidebook, Does.Not.Contain("PLACEHOLDER").IgnoreCase,
                        $"{id} still has placeholder guidebook text.");
                    Assert.That(guidebook, Does.Not.Contain("NOT IMPLEMENTED").IgnoreCase,
                        $"{id} still advertises an unimplemented effect.");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryGeneratedPropertyResolvesToOneTypedChemicalEffect()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var reflection = pair.Server.ResolveDependency<IReflectionManager>();
            var properties = prototypes.EnumeratePrototypes<ReagentPropertyPrototype>()
                .Where(property => !property.Abstract && !string.IsNullOrWhiteSpace(property.EffectName))
                .ToArray();

            Assert.That(properties, Has.Length.EqualTo(102));
            Assert.That(properties.Select(property => property.ID), Is.Unique);
            Assert.That(properties.Select(property => property.EffectName), Is.Unique);
            Assert.Multiple(() =>
            {
                foreach (var property in properties)
                {
                    Assert.That(reflection.TryLooseGetType(property.EffectName, out var effectType), Is.True,
                        $"{property.ID} has no typed effect named {property.EffectName}.");
                    Assert.That(effectType != null && typeof(RMCChemicalEffect).IsAssignableFrom(effectType), Is.True,
                        $"{property.ID} resolves to a non-chemical effect.");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryReagentUsesAResolvedMetabolismStage()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var legacyGroups = new HashSet<string>
            {
                "Medicine",
                "Food",
                "Narcotic",
                "Alcohol",
                "Poison",
            };

            Assert.Multiple(() =>
            {
                foreach (var reagent in prototypes.EnumeratePrototypes<ReagentPrototype>())
                {
                    if (reagent.Metabolisms == null)
                        continue;

                    foreach (var stage in reagent.Metabolisms.Metabolisms.Keys)
                    {
                        Assert.That(legacyGroups, Does.Not.Contain(stage.Id),
                            $"Reagent {reagent.ID} still uses legacy metabolism group {stage.Id}.");
                        Assert.That(prototypes.HasIndex<MetabolismStagePrototype>(stage), Is.True,
                            $"Reagent {reagent.ID} uses missing metabolism stage {stage.Id}.");
                    }
                }

                var nutriment = prototypes.Index<ReagentPrototype>("Nutriment");
                Assert.That(nutriment.Metabolisms!.Metabolisms.Keys.Select(stage => stage.Id),
                    Does.Contain("Metabolites"),
                    "Base Nutriment's intentional Metabolites-stage effects were moved to Digestion.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdminContractDynamicallyLoadsEveryGeneratedPropertyExactlyOnce()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            const string generatedId = "TAU-ALL-TYPED-PROPERTIES-TEST";
            var server = pair.Server;
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var reflection = server.ResolveDependency<IReflectionManager>();
            // Test maps do not raise the lifecycle event that prepares generator classes and property buckets.
            entities.EventBus.RaiseEvent(EventSource.Local, new LoadingMapsEvent([]));
            var properties = prototypes.EnumeratePrototypes<ReagentPropertyPrototype>()
                .Where(property => !property.Abstract && !string.IsNullOrWhiteSpace(property.EffectName))
                .OrderBy(property => property.ID)
                .ToArray();
            var console = entities.SpawnEntity("CMUAdminChemicalContractConsole", MapCoordinates.Nullspace);
            EntityUid? contract = null;

            try
            {
                Assert.That(properties, Has.Length.EqualTo(102));
                var data = new GeneratedReagentData
                {
                    ID = generatedId,
                    Name = "Omniproperty test reagent",
                    Class = ReagentClass.Ultra,
                    GenTier = 3,
                    RecipeHint = "CMInaprovaline",
                    Effects = properties.ToDictionary(property => property.ID, _ => 1),
                };

                contract = entities.System<ServerResearchDataTerminalSystem>()
                    .IssueAdminContract(console, data);
                Assert.That(contract, Is.Not.Null);
                Assert.That(prototypes.HasIndex<ReagentPrototype>(generatedId), Is.True);

                var reagent = prototypes.Index<ReagentPrototype>(generatedId);
                var effects = reagent.Metabolisms!.Metabolisms.Values
                    .SelectMany(entry => entry.Effects)
                    .ToArray();
                Assert.That(effects, Has.Length.EqualTo(properties.Length));
                Assert.That(effects.Select(effect => effect.GetType()), Is.Unique);
                Assert.Multiple(() =>
                {
                    foreach (var property in properties)
                    {
                        Assert.That(reflection.TryLooseGetType(property.EffectName, out var effectType), Is.True);
                        Assert.That(effects.Count(effect => effect.GetType() == effectType), Is.EqualTo(1),
                            $"Generated property {property.ID} did not load exactly one {property.EffectName} effect.");
                    }
                });
            }
            finally
            {
                if (contract is { } contractEntity && entities.EntityExists(contractEntity))
                    entities.DeleteEntity(contractEntity);
                entities.DeleteEntity(console);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryPropertyExecutesAtNormalOverdoseAndCriticalThresholds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([ReagentPrototype]);

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var reagent = prototypes.Index<ReagentPrototype>(TestReagent);
            var effects = reagent.Metabolisms!.Metabolisms.Values
                .SelectMany(entry => entry.Effects)
                .OfType<RMCChemicalEffect>()
                .ToArray();

            Assert.That(effects, Has.Length.EqualTo(PropertyIds.Length));
            foreach (var effect in effects)
            {
                foreach (var quantity in new FixedPoint2[] { 1, 10, 20 })
                {
                    var human = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                    try
                    {
                        Assert.That(() => ApplyEffect(entities, human, effect, reagent, quantity),
                            Throws.Nothing,
                            $"{effect.GetType().Name} failed at bloodstream quantity {quantity}u.");
                        Assert.That(entities.EntityExists(human), Is.True,
                            $"{effect.GetType().Name} unexpectedly deleted its target at {quantity}u.");
                    }
                    finally
                    {
                        if (entities.EntityExists(human))
                            entities.DeleteEntity(human);
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TypedReagentConditionUsesLiveSourceAndCurrentContextReagent()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([ReagentPrototype]);

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var reagent = prototypes.Index<ReagentPrototype>(TestReagent);
            var target = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var implicitCondition = new Antitoxic
                {
                    Potency = 2,
                    Conditions = [new ReagentCondition { Min = 2 }],
                };
                var explicitCondition = new Antitoxic
                {
                    Potency = 2,
                    Conditions = [new ReagentCondition { Reagent = "Water", Min = 2 }],
                };
                var boundedCondition = new Antitoxic
                {
                    Potency = 2,
                    Conditions = [new ReagentCondition { Min = 2, Max = 3 }],
                };
                var invertedCondition = new Antitoxic
                {
                    Potency = 2,
                    Conditions = [new ReagentCondition { Min = 2, Max = 3, Inverted = true }],
                };
                var system = entities.System<SharedEntityEffectsSystem>();

                var insufficient = new Solution(TestReagent, 1);
                var sufficient = new Solution(TestReagent, 2);
                var excessive = new Solution(TestReagent, 4);
                var explicitSource = new Solution(TestReagent, 1);
                explicitSource.AddReagent("Water", 2);

                Assert.Multiple(() =>
                {
                    Assert.That(TryApply(system, target, implicitCondition, reagent, insufficient), Is.False);
                    Assert.That(TryApply(system, target, implicitCondition, reagent, sufficient), Is.True,
                        "The implicit condition did not bind to the current context reagent.");
                    Assert.That(TryApply(system, target, explicitCondition, reagent, sufficient), Is.False);
                    Assert.That(TryApply(system, target, explicitCondition, reagent, explicitSource), Is.True,
                        "The explicit condition did not inspect its named reagent in the live source.");
                    Assert.That(TryApply(system, target, implicitCondition, reagent, null), Is.False,
                        "A reagent condition passed without a source solution.");
                    Assert.That(TryApply(system, target, boundedCondition, reagent, sufficient), Is.True);
                    Assert.That(TryApply(system, target, boundedCondition, reagent, excessive), Is.False,
                        "The reagent condition ignored its maximum quantity.");
                    Assert.That(TryApply(system, target, invertedCondition, reagent, sufficient), Is.False,
                        "Central condition inversion did not reject a matching source.");
                    Assert.That(TryApply(system, target, invertedCondition, reagent, insufficient), Is.True,
                        "Central condition inversion did not accept a non-matching source.");
                    Assert.That(TryApply(system, target, invertedCondition, reagent, null), Is.True,
                        "The missing-source false result was not centrally inverted.");
                });
            }
            finally
            {
                entities.DeleteEntity(target);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TemporaryPropertiesFallBackToTheStrongestStillActiveSource()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        EntityUid human = default;

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            human = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entities.System<ChemicalPropertyStatusSystem>()
                .ApplyNerveStimulation(human, 3f, "high-strength-reagent");
            Assert.That(entities.GetComponent<ChemicalNerveStimulationComponent>(human).Strength,
                Is.EqualTo(3f));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<ChemicalPropertyStatusSystem>()
                .ApplyNerveStimulation(human, 1f, "low-strength-reagent");
            Assert.That(entities.GetComponent<ChemicalNerveStimulationComponent>(human).Strength,
                Is.EqualTo(3f));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.25f));

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<ChemicalNerveStimulationComponent>(human).Strength,
                Is.EqualTo(1f));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.HasComponent<ChemicalNerveStimulationComponent>(human), Is.False);
            entities.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElectrogeneticDefibrillationHealsAndConsumesOneUnit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([ReagentPrototype]);

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var human = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var defibrillator = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var damageable = entities.System<DamageableSystem>();
                var index = entities.System<CMUMedicalBodyIndexSystem>();
                var bloodstream = entities.System<SharedRMCBloodstreamSystem>();
                var heart = index.TryGetOrgan<HeartComponent>(human, out var organ)
                    ? organ
                    : throw new AssertionException("Test human has no heart.");
                var heartComponent = entities.GetComponent<HeartComponent>(heart);

                SetField(heartComponent, nameof(HeartComponent.Stopped), true);
                SetField(heartComponent, nameof(HeartComponent.BeatsPerMinute), 0);

                var damage = new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Blunt"] = 20,
                        ["Heat"] = 20,
                        ["Poison"] = 20,
                    },
                };
                damageable.TryChangeDamage(human, damage, true, interruptsDoAfters: false);

                Assert.That(bloodstream.TryGetChemicalSolution(human, out var solution, out _), Is.True);
                solution.Comp.Solution.AddReagent(TestReagent, 2);

                entities.EnsureComponent<DefibrillatorComponent>(defibrillator);
                var attempt = new RMCDefibrillatorDamageModifyEvent(human, new DamageSpecifier());
                entities.EventBus.RaiseLocalEvent(defibrillator, ref attempt);

                Assert.Multiple(() =>
                {
                    Assert.That(attempt.Heal.GetTotal(), Is.LessThan(FixedPoint2.Zero));
                    Assert.That(solution.Comp.Solution.GetTotalPrototypeQuantity(TestReagent),
                        Is.EqualTo((FixedPoint2)1));
                    Assert.That(heartComponent.Stopped, Is.False);
                });
            }
            finally
            {
                entities.DeleteEntity(human);
                entities.DeleteEntity(defibrillator);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChemicalDefibrillatingRevivesARevivableCorpseAndTriggersElectrogenetic()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([ReagentPrototype]);

        EntityUid human = default;

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var reagent = prototypes.Index<ReagentPrototype>(DefibrillatingTestReagent);
            Assert.That(reagent.WorksOnTheDead, Is.True);

            human = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bloodstream = entities.System<SharedRMCBloodstreamSystem>();
            Assert.That(bloodstream.TryGetChemicalSolution(human, out var bloodSolution, out _), Is.True);

            var mobState = entities.GetComponent<MobStateComponent>(human);
            entities.System<MobStateSystem>().ChangeMobState(human, MobState.Dead, mobState, human);
            bloodSolution.Comp.Solution.AddReagent(DefibrillatingTestReagent, 30);
            Assert.That(mobState.CurrentState, Is.EqualTo(MobState.Dead));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2));

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            var bloodstream = entities.System<SharedRMCBloodstreamSystem>();
            Assert.That(index.TryGetOrgan<HeartComponent>(human, out var heart), Is.True);
            Assert.That(bloodstream.TryGetChemicalSolution(human, out var bloodSolution, out _), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<MobStateComponent>(human).CurrentState,
                    Is.Not.EqualTo(MobState.Dead));
                Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.False);
                Assert.That(bloodSolution.Comp.Solution.GetTotalPrototypeQuantity(DefibrillatingTestReagent),
                    Is.LessThan((FixedPoint2)30));
            });
            entities.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SupportingSystemsRespectTargetsThresholdsAndExpiration()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([ReagentPrototype]);
        var spawned = new List<EntityUid>();

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var reagent = prototypes.Index<ReagentPrototype>(TestReagent);
            var status = entities.System<ChemicalPropertyStatusSystem>();
            var medical = entities.System<CMUChemicalMedicalSystem>();
            var index = entities.System<CMUMedicalBodyIndexSystem>();
        var statusEffects = entities.System<StatusEffectsSystem>();

            var human = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            spawned.Add(human);

            status.ApplyNerveStimulation(human, 1f);
            status.ApplyNerveStimulation(human, 3f);
            status.ApplyPainSensitivity(human, 1.5f);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<ChemicalNerveStimulationComponent>(human).Strength,
                    Is.EqualTo(3f));
                Assert.That(entities.GetComponent<ChemicalPainSensitivityComponent>(human).Multiplier,
                    Is.EqualTo(1.5f));
            });

            Assert.That(index.TryGetOrgan<CMUBrainComponent>(human, out var brain), Is.True);
            var brainHealth = entities.GetComponent<OrganHealthComponent>(brain);
            var brainBefore = brainHealth.Current;
            status.ApplyNeuroshield(human);
            Assert.That(medical.DamageOrgan<CMUBrainComponent>(human, 10, "Shock"), Is.True);
            Assert.That(brainHealth.Current, Is.EqualTo(brainBefore - 2));

            status.ApplyNeurocryogenic(human);
            var frozenHealth = brainHealth.Current;
            Assert.That(medical.DamageOrgan<CMUBrainComponent>(human, 10, "Shock"), Is.True);
            Assert.That(brainHealth.Current, Is.EqualTo(frozenHealth));
            Assert.That(medical.DamageOrgan<CMUBrainComponent>(human, 10, "Shock", OrganDamageSource.Direct), Is.True);
            Assert.That(brainHealth.Current, Is.EqualTo(frozenHealth - 10));

            Assert.That(index.TryGetOrgan<HeartComponent>(human, out var heart), Is.True);
            var heartComponent = entities.GetComponent<HeartComponent>(heart);
            SetField(heartComponent, nameof(HeartComponent.Stopped), true);
            Assert.That(medical.HealOrgan<HeartComponent>(human, 1, restartHeart: true), Is.True);
            Assert.That(heartComponent.Stopped, Is.False);

            var noOrgans = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            spawned.Add(noOrgans);
            Assert.Multiple(() =>
            {
                Assert.That(medical.HealOrgan<HeartComponent>(noOrgans, 1, restartHeart: true), Is.False);
                Assert.That(medical.DamageOrgan<CMUBrainComponent>(noOrgans, 1, "Shock"), Is.False);
            });

            var painkilling = reagent.Metabolisms!.Metabolisms.Values
                .SelectMany(entry => entry.Effects)
                .OfType<Painkilling>()
                .Single();
            statusEffects.TryRemoveStatusEffect(human, "StatusEffectDrowsiness");
            statusEffects.TryRemoveStatusEffect(human, "StatusEffectCMUUnconscious");
            ApplyEffect(entities, human, painkilling, reagent, 9.99f);
            Assert.That(statusEffects.HasStatusEffect(human, "StatusEffectDrowsiness"), Is.False);

            ApplyEffect(entities, human, painkilling, reagent, 10);
            Assert.That(statusEffects.HasStatusEffect(human, "StatusEffectDrowsiness"), Is.True);
            statusEffects.TryRemoveStatusEffect(human, "StatusEffectDrowsiness");

            ApplyEffect(entities, human, painkilling, reagent, 20);
            Assert.Multiple(() =>
            {
                Assert.That(statusEffects.HasStatusEffect(human, "StatusEffectDrowsiness"), Is.True);
                Assert.That(statusEffects.HasStatusEffect(human, "StatusEffectCMUUnconscious"), Is.True);
            });

            entities.EnsureComponent<NicotineAddictionComponent>(human);
            entities.System<ChemicalAddictionSystem>().AddOrSatisfy(human, TestReagent);
            var antiaddictive = reagent.Metabolisms.Metabolisms.Values
                .SelectMany(entry => entry.Effects)
                .OfType<Antiaddictive>()
                .Single();
            ApplyEffect(entities, human, antiaddictive, reagent, 1);
            ApplyEffect(entities, human, antiaddictive, reagent, 1);

            TestRepairingContact(entities, reagent, spawned);
            TestHydroponics(entities, prototypes, spawned);
            TestBonesAndShrapnel(entities, index, human);

            var earlyInfection = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            spawned.Add(earlyInfection);
            entities.EnsureComponent<VictimInfectedComponent>(earlyInfection);
            Assert.That(entities.System<SharedXenoParasiteSystem>().TryCureEarlyInfection(earlyInfection), Is.True);

            var establishedInfection = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var larva = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            spawned.Add(establishedInfection);
            spawned.Add(larva);
            var established = entities.EnsureComponent<VictimInfectedComponent>(establishedInfection);
            SetField(established, nameof(VictimInfectedComponent.SpawnedLarva), (EntityUid?)larva);
            Assert.That(entities.System<SharedXenoParasiteSystem>().TryCureEarlyInfection(establishedInfection), Is.False);
            Assert.That(entities.System<SharedXenoParasiteSystem>()
                .TryChemicallyExpelInfection(establishedInfection), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(3));

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var human = spawned[0];
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<ChemicalNerveStimulationComponent>(human), Is.False);
                Assert.That(entities.HasComponent<ChemicalPainSensitivityComponent>(human), Is.False);
                Assert.That(entities.HasComponent<ChemicalNeuroshieldComponent>(human), Is.False);
                Assert.That(entities.HasComponent<ChemicalNeurocryogenicComponent>(human), Is.False);
                Assert.That(entities.HasComponent<NicotineAddictionComponent>(human), Is.False);
                Assert.That(entities.HasComponent<ChemicalAddictionComponent>(human), Is.False);
                Assert.That(entities.HasComponent<VictimInfectedComponent>(spawned[^3]), Is.False);
                Assert.That(entities.HasComponent<VictimInfectedComponent>(spawned[^2]), Is.False);
                Assert.That(entities.EntityExists(spawned[^1]), Is.False);
            });

            foreach (var entity in spawned)
            {
                if (entities.EntityExists(entity))
                    entities.DeleteEntity(entity);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void SharedPotencyScaleIsLinearForEveryBeneficialPropertyType()
    {
        var assembly = typeof(Antiparasitic).Assembly;
        Assert.Multiple(() =>
        {
            foreach (var id in PropertyIds)
            {
                var type = assembly.GetTypes().Single(candidate => candidate.Name == id);
                var levelOne = (RMCChemicalEffect)Activator.CreateInstance(type)!;
                var levelFour = (RMCChemicalEffect)Activator.CreateInstance(type)!;
                levelOne.Potency = 1;
                levelFour.Potency = 4;

                Assert.That(levelFour.ActualPotency, Is.EqualTo(levelOne.ActualPotency * 4f),
                    $"{id} does not have linear actual potency.");
                Assert.That(levelFour.PotencyPerSecond, Is.EqualTo(levelOne.PotencyPerSecond * 4f),
                    $"{id} does not have linear per-second potency.");
                Assert.That(levelFour.LinearLevel, Is.EqualTo(4f),
                    $"{id} does not expose its generated level linearly.");
            }
        });
    }

    [Test]
    public void ChemicalStunDurationModifierDefaultsToNoChange()
    {
        var ev = new GetChemicalStunTimeMultiplierEvent();
        Assert.That(ev.Multiplier, Is.EqualTo(1f));
    }

    private static bool ApplyEffect(
        IEntityManager entities,
        EntityUid target,
        EntityEffect effect,
        ReagentPrototype reagent,
        FixedPoint2 bloodstreamQuantity,
        ReagentEffectOrigin origin = ReagentEffectOrigin.Metabolism,
        EntityUid? organ = null)
    {
        var source = new Solution(TestReagent, bloodstreamQuantity);
        var quantity = new ReagentQuantity(TestReagent, bloodstreamQuantity);
        var context = new ReagentEffectContext(
            reagent,
            source,
            null,
            organ,
            quantity,
            origin == ReagentEffectOrigin.Metabolism ? "Bloodstream" : null,
            origin == ReagentEffectOrigin.Reaction ? ReactionMethod.Touch : null,
            origin);
        return entities.System<SharedEntityEffectsSystem>()
            .TryApplyEffect(target, effect, reagentContext: context);
    }

    private static bool TryApply(
        SharedEntityEffectsSystem system,
        EntityUid target,
        EntityEffect effect,
        ReagentPrototype reagent,
        Solution? source)
    {
        var context = new ReagentEffectContext(
            reagent,
            source,
            null,
            null,
            new ReagentQuantity(TestReagent, 0.25f),
            "Bloodstream",
            null,
            ReagentEffectOrigin.Metabolism);
        return system.TryApplyEffect(target, effect, scale: 0.25f, reagentContext: context);
    }

    private static void TestRepairingContact(
        IEntityManager entities,
        ReagentPrototype reagent,
        ICollection<EntityUid> spawned)
    {
        var inorganic = entities.SpawnEntity(InorganicTarget, MapCoordinates.Nullspace);
        var organic = entities.SpawnEntity(OrganicTarget, MapCoordinates.Nullspace);
        var xeno = entities.SpawnEntity(XenoTarget, MapCoordinates.Nullspace);
        spawned.Add(inorganic);
        spawned.Add(organic);
        spawned.Add(xeno);

        var damageable = entities.System<DamageableSystem>();
        ApplyDamage(damageable, inorganic, "Structural", 30);
        ApplyDamage(damageable, xeno, "Structural", 30);
        ApplyDamage(damageable, organic, "Blunt", 30);

        var source = new Solution(TestReagent, 1);
        var quantity = new ReagentQuantity(TestReagent, 1);
        var reactive = entities.System<ReactiveSystem>();
        reactive.ReactionEntity(inorganic, ReactionMethod.Touch, reagent, quantity, source);
        reactive.ReactionEntity(organic, ReactionMethod.Touch, reagent, quantity, source);
        reactive.ReactionEntity(xeno, ReactionMethod.Touch, reagent, quantity, source);

        Assert.Multiple(() =>
        {
            Assert.That(GetDamage(entities, inorganic, "Structural"), Is.EqualTo((FixedPoint2)20));
            Assert.That(GetDamage(entities, organic, "Blunt"), Is.EqualTo((FixedPoint2)30));
            Assert.That(GetDamage(entities, xeno, "Structural"), Is.EqualTo((FixedPoint2)30));
        });
    }

    private static void TestHydroponics(
        IEntityManager entities,
        IPrototypeManager prototypes,
        ICollection<EntityUid> spawned)
    {
        var tray = entities.SpawnEntity("hydroponicsTray", MapCoordinates.Nullspace);
        var plantEntity = entities.SpawnEntity("WheatPlants", MapCoordinates.Nullspace);
        spawned.Add(tray);
        spawned.Add(plantEntity);
        var trayComponent = entities.GetComponent<PlantTrayComponent>(tray);
        var plant = entities.GetComponent<PlantHolderComponent>(plantEntity);
        entities.System<PlantTraySystem>().PlantingPlantInTray((tray, trayComponent), plantEntity);
        SetField(trayComponent, nameof(PlantTrayComponent.ToxinLevel), 0f);
        SetField(plant, nameof(PlantHolderComponent.MutationLevel), 0f);

        var carcinogenic = new Carcinogenic { Potency = 2 };
        var carcinogenicSource = new Solution(TestReagent, 1);
        var carcinogenicContext = new ReagentEffectContext(
            prototypes.Index<ReagentPrototype>(TestReagent),
            carcinogenicSource,
            null,
            null,
            new ReagentQuantity(TestReagent, 1),
            null,
            null,
            ReagentEffectOrigin.Hydroponics);
        Assert.That(entities.System<SharedEntityEffectsSystem>()
            .TryApplyEffect(tray, carcinogenic, reagentContext: carcinogenicContext), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(trayComponent.ToxinLevel, Is.EqualTo(1.5f));
            // Legacy hydro mutation adds the plant's default MutationMod (1) to the processed 1u quantity.
            Assert.That(plant.MutationLevel, Is.EqualTo(20f));
        });

        SetField(trayComponent, nameof(PlantTrayComponent.ToxinLevel), 5f);
        SetField(plant, nameof(PlantHolderComponent.Health), 20f);

        var solutions = entities.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetSolution(tray, trayComponent.SoilSolutionName, out var soil), Is.True);
        soil!.Value.Comp.Solution.AddReagent(TestReagent, 5);
        entities.System<PlantTraySystem>().UpdateReagents((tray, trayComponent));
        Assert.Multiple(() =>
        {
            Assert.That(trayComponent.ToxinLevel, Is.Zero);
            // Antitoxic clears all 5 toxin first; Anticorrosive then heals 0.5 potency * 1u * 5.
            Assert.That(plant.Health, Is.EqualTo(22.5f));
            Assert.That(soil.Value.Comp.Solution.GetTotalPrototypeQuantity(TestReagent),
                Is.EqualTo((FixedPoint2)4),
                "The RMC hydro effect consumed or scaled from more than the processed 1u tick quantity.");
        });

        Assert.That(entities.GetComponent<CMUChemicalMutationWhitelistComponent>(plantEntity).AllowedMutations,
            Is.EquivalentTo(new[]
            {
                "ChangeLifespan",
                "ChangeEndurance",
                "ChangeWaterConsumption",
                "ChangeNutrientConsumption",
                "ChangeToxinsTolerance",
                "ChangeWeedTolerance",
                "ChangeProduction",
                "ChangeMaturation",
                "ChangePotency",
                "ChangeSpecies",
            }));

        Assert.Multiple(() =>
        {
            Assert.That(IsMutationCancelled(entities, prototypes, plantEntity, "ChangePotency"), Is.False,
                "The chemical whitelist rejected one of its explicitly enabled mutations.");
            Assert.That(IsMutationCancelled(entities, prototypes, plantEntity, "ChangeHarvest"), Is.True,
                "The chemical whitelist allowed a mutation outside its union.");
        });

        soil.Value.Comp.Solution.AddReagent(TestReagent, 1);
        entities.System<PlantTraySystem>().UpdateReagents((tray, trayComponent));
        Assert.That(entities.GetComponent<CMUChemicalMutationWhitelistComponent>(plantEntity).AllowedMutations,
            Has.Count.EqualTo(10), "Repeated chemical ticks duplicated whitelist entries.");

        var suppressionExpiry = entities
            .GetComponent<CMUChemicalMutationSuppressionComponent>(plantEntity)
            .ExpiresAt;
        SetField(plant, nameof(PlantHolderComponent.MutationLevel), 1f);
        entities.System<PlantMutationSystem>().SpeciesChange(
            (plantEntity, entities.GetComponent<PlantDataComponent>(plantEntity)),
            "MeatWheatPlants");
        var replacement = trayComponent.PlantEntity;
        Assert.That(replacement, Is.Not.Null.And.Not.EqualTo(plantEntity));
        plantEntity = replacement!.Value;
        spawned.Add(plantEntity);
        plant = entities.GetComponent<PlantHolderComponent>(plantEntity);

        Assert.Multiple(() =>
        {
            Assert.That(plant.MutationLevel, Is.Zero,
                "Species replacement did not complete its forced mutation pass.");
            Assert.That(entities.GetComponent<CMUChemicalMutationWhitelistComponent>(plantEntity).AllowedMutations,
                Has.Count.EqualTo(10), "Species replacement lost or duplicated the mutation whitelist.");
            Assert.That(entities.GetComponent<CMUChemicalMutationSuppressionComponent>(plantEntity).ExpiresAt,
                Is.EqualTo(suppressionExpiry), "Species replacement changed the active Cardiopeutic expiry.");
            Assert.That(IsMutationCancelled(entities, prototypes, plantEntity, "ChangePotency"), Is.False);
            Assert.That(IsMutationCancelled(entities, prototypes, plantEntity, "ChangeHarvest"), Is.True);
        });

        entities.RemoveComponent<CMUChemicalMutationWhitelistComponent>(plantEntity);
        Assert.Multiple(() =>
        {
            Assert.That(IsMutationCancelled(entities, prototypes, plantEntity, "ChangeChemicals"), Is.True,
                "Cardiopeutic did not suppress chemical-production mutation.");
            Assert.That(IsMutationCancelled(entities, prototypes, plantEntity, "ChangeHarvest"), Is.False,
                "Cardiopeutic suppressed a mutation other than ChangeChemicals.");
        });

        SetField(plant, nameof(PlantHolderComponent.Dead), true);
        SetField(trayComponent, nameof(PlantTrayComponent.ToxinLevel), 5f);
        soil.Value.Comp.Solution.AddReagent(TestReagent, 1);
        entities.System<PlantTraySystem>().UpdateReagents((tray, trayComponent));
        Assert.That(trayComponent.ToxinLevel, Is.EqualTo(5f));
    }

    private static bool IsMutationCancelled(
        IEntityManager entities,
        IPrototypeManager prototypes,
        EntityUid plant,
        string mutationName)
    {
        var prototype = prototypes.Index<RandomPlantMutationListPrototype>("RandomPlantMutations");
        var mutation = prototype.Mutations.Single(candidate => candidate.Name == mutationName);
        var ev = new BeforeRandomPlantMutationEvent(plant, mutation);
        entities.EventBus.RaiseLocalEvent(plant, ref ev);
        return ev.Cancelled;
    }

    private static void TestBonesAndShrapnel(
        IEntityManager entities,
        CMUMedicalBodyIndexSystem index,
        EntityUid human)
    {
        var arms = index.GetBodyParts(human)
            .Where(part => part.Comp.PartType == BodyPartType.Arm)
            .Take(2)
            .ToArray();
        Assert.That(arms, Has.Length.EqualTo(2));

        var treatedPart = arms[0].Owner;
        var otherPart = arms[1].Owner;
        var bone = entities.GetComponent<BoneComponent>(treatedPart);
        SetField(bone, nameof(BoneComponent.Integrity), (FixedPoint2)20);
        var fracture = entities.EnsureComponent<FractureComponent>(treatedPart);
        entities.System<SharedFractureSystem>()
            .SetSeverity((treatedPart, fracture), FractureSeverity.Compound);
        entities.EnsureComponent<CMUSplintedComponent>(treatedPart);

        Assert.That(entities.System<SharedBoneSystem>().ChemicallyMendFractures(human, 10), Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(bone.Integrity, Is.EqualTo((FixedPoint2)30));
            Assert.That(fracture.Severity, Is.EqualTo(FractureSeverity.Simple));
        });

        var shrapnel = entities.System<SharedCMUShrapnelSystem>();
        Assert.That(shrapnel.AddShrapnel(treatedPart, 2, 10f), Is.True);
        Assert.That(shrapnel.AddShrapnel(otherPart, 2, 30f), Is.True);
        Assert.That(shrapnel.TryRemoveShrapnel(human, 1), Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(entities.GetComponent<CMUShrapnelComponent>(treatedPart).Fragments, Is.EqualTo(2));
            Assert.That(entities.GetComponent<CMUShrapnelComponent>(otherPart).Fragments, Is.EqualTo(1));
        });
    }

    private static void ApplyDamage(
        DamageableSystem system,
        EntityUid target,
        ProtoId<DamageTypePrototype> type,
        FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        system.TryChangeDamage(target, damage, true, interruptsDoAfters: false);
    }

    private static FixedPoint2 GetDamage(IEntityManager entities, EntityUid target, string type)
        => entities.GetComponent<DamageableComponent>(target).Damage.DamageDict.GetValueOrDefault(type);

    private static void SetField<T>(object target, string name, T value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(target.GetType().FullName, name);
        field.SetValue(target, value);
    }
}

#pragma warning restore RA0002
