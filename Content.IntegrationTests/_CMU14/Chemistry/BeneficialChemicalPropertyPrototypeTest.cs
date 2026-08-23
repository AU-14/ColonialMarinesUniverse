using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Server._AU14.Chemistry.Reagents;
using Content.Server._AU14.Chemistry.Research;
using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._CMU14.Chemistry.Effects.Positive;
using Content.Shared._CMU14.Chemistry.Effects.Special;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Traits.NicotineAddiction;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects.Positive;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests._CMU14.Chemistry;

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
            Medicine:
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
            Medicine:
              metabolismRate: 0.1
              effects:
              - !type:Defibrillating
                potency: 6

        - type: entity
          id: {{InorganicTarget}}
          components:
          - type: Damageable
            damageContainer: StructuralInorganic

        - type: entity
          id: {{OrganicTarget}}
          components:
          - type: Damageable
            damageContainer: Biological

        - type: entity
          id: {{XenoTarget}}
          components:
          - type: Damageable
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
            var effects = reagent.Metabolisms!.Values
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
                    var guidebook = chemical.GuidebookEffectDescription(prototypes, systems);
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
    public async Task EveryPropertyExecutesAtNormalOverdoseAndCriticalThresholds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([ReagentPrototype]);

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var reagent = prototypes.Index<ReagentPrototype>(TestReagent);
            var effects = reagent.Metabolisms!.Values
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
                        Assert.That(
                            () => effect.Effect(ReagentArgs(human, entities, reagent, quantity)),
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
            var statusEffects = entities.System<SharedStatusEffectsSystem>();

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

            var painkilling = reagent.Metabolisms!.Values
                .SelectMany(entry => entry.Effects)
                .OfType<Painkilling>()
                .Single();
            statusEffects.TryRemoveStatusEffect(human, "StatusEffectDrowsiness");
            statusEffects.TryRemoveStatusEffect(human, "StatusEffectCMUUnconscious");
            painkilling.Effect(ReagentArgs(human, entities, reagent, 9.99f));
            Assert.That(statusEffects.HasStatusEffect(human, "StatusEffectDrowsiness"), Is.False);

            painkilling.Effect(ReagentArgs(human, entities, reagent, 10));
            Assert.That(statusEffects.HasStatusEffect(human, "StatusEffectDrowsiness"), Is.True);
            statusEffects.TryRemoveStatusEffect(human, "StatusEffectDrowsiness");

            painkilling.Effect(ReagentArgs(human, entities, reagent, 20));
            Assert.Multiple(() =>
            {
                Assert.That(statusEffects.HasStatusEffect(human, "StatusEffectDrowsiness"), Is.True);
                Assert.That(statusEffects.HasStatusEffect(human, "StatusEffectCMUUnconscious"), Is.True);
            });

            entities.EnsureComponent<NicotineAddictionComponent>(human);
            entities.System<ChemicalAddictionSystem>().AddOrSatisfy(human, TestReagent);
            var antiaddictive = reagent.Metabolisms.Values
                .SelectMany(entry => entry.Effects)
                .OfType<Antiaddictive>()
                .Single();
            antiaddictive.Effect(ReagentArgs(human, entities, reagent, 1));
            antiaddictive.Effect(ReagentArgs(human, entities, reagent, 1));

            TestRepairingContact(entities, reagent, spawned);
            TestHydroponics(entities, reagent, spawned);
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

    private static EntityEffectReagentArgs ReagentArgs(
        EntityUid target,
        IEntityManager entities,
        ReagentPrototype reagent,
        FixedPoint2 bloodstreamQuantity)
    {
        var source = new Solution(TestReagent, bloodstreamQuantity);
        return new EntityEffectReagentArgs(target, entities, null, source, 1, reagent, null, 1);
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

        var contact = new ReactionEntityEvent(
            ReactionMethod.Touch,
            reagent,
            new ReagentQuantity(TestReagent, 1),
            null);
        entities.EventBus.RaiseLocalEvent(inorganic, ref contact);
        entities.EventBus.RaiseLocalEvent(organic, ref contact);
        entities.EventBus.RaiseLocalEvent(xeno, ref contact);

        Assert.Multiple(() =>
        {
            Assert.That(GetDamage(entities, inorganic, "Structural"), Is.EqualTo((FixedPoint2)20));
            Assert.That(GetDamage(entities, organic, "Blunt"), Is.EqualTo((FixedPoint2)30));
            Assert.That(GetDamage(entities, xeno, "Structural"), Is.EqualTo((FixedPoint2)30));
        });
    }

    private static void TestHydroponics(
        IEntityManager entities,
        ReagentPrototype reagent,
        ICollection<EntityUid> spawned)
    {
        var tray = entities.SpawnEntity(null, MapCoordinates.Nullspace);
        spawned.Add(tray);
        var plant = entities.EnsureComponent<PlantHolderComponent>(tray);
        var seed = new SeedData();
        SetField(seed, nameof(SeedData.Unique), true);
        SetField(seed, nameof(SeedData.Endurance), 100f);
        SetField(seed, nameof(SeedData.Potency), 50f);
        plant.Seed = seed;
        plant.Health = 20f;
        plant.Toxins = 5f;

        var hydroArgs = new EntityEffectHydroArgs(tray, entities, null, null, 1, reagent, null, 1);
        entities.EventBus.RaiseEvent(EventSource.Local, new HydroTickEvent<Antitoxic>(1, hydroArgs));
        entities.EventBus.RaiseEvent(EventSource.Local, new HydroTickEvent<Anticorrosive>(1, hydroArgs));
        Assert.Multiple(() =>
        {
            Assert.That(plant.Toxins, Is.Zero);
            Assert.That(plant.Health, Is.EqualTo(25f));
        });

        entities.EventBus.RaiseEvent(EventSource.Local, new HydroTickEvent<Hepatopeutic>(1, hydroArgs));
        entities.EventBus.RaiseEvent(EventSource.Local, new HydroTickEvent<Nephropeutic>(1, hydroArgs));
        entities.EventBus.RaiseEvent(EventSource.Local, new HydroTickEvent<Pneumopeutic>(1, hydroArgs));
        entities.EventBus.RaiseEvent(EventSource.Local, new HydroTickEvent<Oculopeutic>(1, hydroArgs));
        entities.EventBus.RaiseEvent(EventSource.Local, new HydroTickEvent<Neuropeutic>(1, hydroArgs));
        Assert.Multiple(() =>
        {
            Assert.That(plant.MutationController.Fields["Plant Cancer"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Gluttony"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Light Tolerance"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Weed Tolerance"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Toxin Tolerance"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Endurance"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Lifespan"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Production"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Maturity"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Potency"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Bioluminescence"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Flowers"], Is.EqualTo(1));
            Assert.That(plant.MutationController.Fields["Mutate Species"], Is.EqualTo(1));
        });

        SetField(seed, nameof(SeedData.Immutable), true);
        plant.MutationController.Fields["Potency"] = 0;
        entities.EventBus.RaiseEvent(EventSource.Local, new HydroTickEvent<Oculopeutic>(5, hydroArgs));
        Assert.That(plant.MutationController.Fields["Potency"], Is.Zero);

        plant.Dead = true;
        plant.Toxins = 5f;
        entities.EventBus.RaiseEvent(EventSource.Local, new HydroTickEvent<Antitoxic>(1, hydroArgs));
        Assert.That(plant.Toxins, Is.EqualTo(5f));
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
