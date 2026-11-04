using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Shared.CMU14.Medical.Anatomy.Metabolism.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
public sealed class MetabolismToxicityTest
{
    private static readonly ProtoId<MetabolismStagePrototype> Bloodstream = "Bloodstream";
    private static readonly ProtoId<MetabolismStagePrototype> Metabolites = "Metabolites";

    [Test]
    public async Task RepresentativeResourcesTagOnlyPoisonAndAlcohol()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            Assert.Multiple(() =>
            {
                Assert.That(GetEntry(prototypes, "WeldingFuel", Bloodstream).CMUToxicity,
                    Is.EquivalentTo(new[] { CMUMetabolismClass.Poison }));
                Assert.That(GetEntry(prototypes, "RMCEthanol", Metabolites).CMUToxicity,
                    Is.EquivalentTo(new[] { CMUMetabolismClass.Alcohol }));
                Assert.That(GetEntry(prototypes, "CMUParacetamol", Bloodstream).CMUToxicity, Is.Empty,
                    "A medicine entry was incorrectly treated as liver-toxic merely because it uses Bloodstream.");
                Assert.That(GetEntry(prototypes, "Nutriment", Metabolites).CMUToxicity, Is.Empty,
                    "Nutriment was incorrectly treated as liver-toxic merely because it uses Metabolites.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MetabolismDamagesLiverOnlyForExplicitToxicityAndStasisFreezesProcessing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var cases = new List<MetabolismCase>();

        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            server.CfgMan.SetCVar(CMUMedicalCCVars.Enabled, true);
            server.CfgMan.SetCVar(CMUMedicalCCVars.OrganEnabled, true);
            _ = server.System<MetabolismToxicityProbeSystem>();

            cases.Add(CreateCase(server.EntMan, server.ResolveDependency<IPrototypeManager>(),
                "WeldingFuel", Bloodstream, new HashSet<CMUMetabolismClass> { CMUMetabolismClass.Poison }));
            cases.Add(CreateCase(server.EntMan, server.ResolveDependency<IPrototypeManager>(),
                "RMCEthanol", Metabolites, new HashSet<CMUMetabolismClass> { CMUMetabolismClass.Alcohol }));
            cases.Add(CreateCase(server.EntMan, server.ResolveDependency<IPrototypeManager>(),
                "CMUParacetamol", Bloodstream, new HashSet<CMUMetabolismClass>()));
            cases.Add(CreateCase(server.EntMan, server.ResolveDependency<IPrototypeManager>(),
                "Nutriment", Metabolites, new HashSet<CMUMetabolismClass>()));
            cases.Add(CreateCase(server.EntMan, server.ResolveDependency<IPrototypeManager>(),
                "WeldingFuel", Bloodstream, new HashSet<CMUMetabolismClass> { CMUMetabolismClass.Poison }, inStasis: true));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var solutions = entities.System<SharedSolutionContainerSystem>();

            foreach (var testCase in cases)
            {
                Assert.That(solutions.TryGetSolution(testCase.Body, testCase.SolutionName, out var solution), Is.True);
                var remaining = solution!.Value.Comp.Solution.GetTotalPrototypeQuantity(testCase.Reagent);
                var bodyProbe = entities.GetComponent<MetabolismToxicityProbeComponent>(testCase.Body);
                var liverProbe = entities.GetComponent<MetabolismToxicityProbeComponent>(testCase.Liver);

                if (testCase.InStasis)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(remaining, Is.EqualTo((FixedPoint2)1));
                        Assert.That(bodyProbe.RateEvents, Is.Zero);
                        Assert.That(liverProbe.ReagentDamageEvents, Is.Zero);
                    });
                    continue;
                }

                Assert.Multiple(() =>
                {
                    Assert.That(remaining, Is.EqualTo((FixedPoint2)1 - testCase.Rate),
                        $"{testCase.Reagent} did not process exactly one metabolism cycle.");
                    Assert.That(bodyProbe.RateEvents, Is.EqualTo(1));
                    Assert.That(bodyProbe.Stages, Is.EqualTo(new[] { testCase.Stage }));
                    Assert.That(bodyProbe.ToxicitySnapshots.Single(), Is.EquivalentTo(testCase.Toxicity));
                    Assert.That(liverProbe.ReagentDamageEvents,
                        Is.EqualTo(testCase.Toxicity.Count),
                        $"{testCase.Reagent} applied an incorrect number of direct liver hits.");
                    Assert.That(liverProbe.ReagentDamageAmounts,
                        Is.All.EqualTo(FixedPoint2.New(0.05)),
                        $"{testCase.Reagent} did not use the exact 0.05 Poison direct-hit amount.");
                });
            }

            foreach (var testCase in cases)
                entities.DeleteEntity(testCase.Body);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClearanceMultiplierAppliesToEveryStageWithoutCreatingToxicity()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            server.CfgMan.SetCVar(CMUMedicalCCVars.Enabled, true);
            server.CfgMan.SetCVar(CMUMedicalCCVars.OrganEnabled, true);
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            var body = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                Assert.That(index.TryGetOrgan<LiverComponent>(body, out var liver), Is.True);
                Assert.That(index.TryGetOrgan<KidneysComponent>(body, out var kidneys), Is.True);
                SetField(entities.GetComponent<LiverComponent>(liver),
                    nameof(LiverComponent.ToxinClearMultiplier),
                    0.5f);
                SetField(entities.GetComponent<KidneysComponent>(kidneys),
                    nameof(KidneysComponent.WasteFiltration),
                    0.5f);

                entities.EnsureComponent<MetabolismToxicityProbeComponent>(liver);
                var stages = prototypes.EnumeratePrototypes<MetabolismStagePrototype>().ToArray();
                Assert.That(stages, Has.Length.EqualTo(5));
                foreach (var stage in stages)
                {
                    var ev = new MetabolismRateModifyEvent(body, stage.ID, "Water", new HashSet<CMUMetabolismClass>(), 1f);
                    entities.EventBus.RaiseLocalEvent(body, ref ev);
                    Assert.That(ev.Multiplier, Is.EqualTo(0.25f),
                        $"Clearance multiplier did not apply to stage {stage.ID}.");
                }

                Assert.That(entities.GetComponent<MetabolismToxicityProbeComponent>(liver).ReagentDamageEvents,
                    Is.Zero,
                    "An empty toxicity class set caused direct liver damage.");
                Assert.That(entities.GetComponent<MetabolismToxicityProbeComponent>(liver).ReagentDamageAmounts,
                    Is.Empty);
            }
            finally
            {
                entities.DeleteEntity(body);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static ReagentEffectsEntry GetEntry(
        IPrototypeManager prototypes,
        ProtoId<ReagentPrototype> reagent,
        ProtoId<MetabolismStagePrototype> stage)
    {
        var prototype = prototypes.Index(reagent);
        Assert.That(prototype.Metabolisms, Is.Not.Null, reagent.Id);
        Assert.That(prototype.Metabolisms!.Metabolisms.TryGetValue(stage, out var entry), Is.True, reagent.Id);
        return entry!;
    }

    private static MetabolismCase CreateCase(
        IEntityManager entities,
        IPrototypeManager prototypes,
        ProtoId<ReagentPrototype> reagent,
        ProtoId<MetabolismStagePrototype> stage,
        IReadOnlySet<CMUMetabolismClass> toxicity,
        bool inStasis = false)
    {
        var body = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var index = entities.System<CMUMedicalBodyIndexSystem>();
        Assert.That(index.TryGetOrgan<LiverComponent>(body, out var liver), Is.True);
        entities.EnsureComponent<MetabolismToxicityProbeComponent>(body);
        entities.EnsureComponent<MetabolismToxicityProbeComponent>(liver);
        if (inStasis)
            entities.EnsureComponent<CMInStasisComponent>(body);

        var solutionName = stage == Bloodstream
            ? BloodstreamComponent.DefaultBloodSolutionName
            : BloodstreamComponent.DefaultMetabolitesSolutionName;
        var solutions = entities.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetSolution(body, solutionName, out var solution), Is.True);
        solution!.Value.Comp.Solution.AddReagent(reagent, 1);

        return new MetabolismCase(
            body,
            liver,
            solutionName,
            reagent,
            stage,
            GetEntry(prototypes, reagent, stage).MetabolismRate,
            toxicity,
            inStasis);
    }

    private static void SetField<T>(object target, string name, T value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(target.GetType().FullName, name);
        field.SetValue(target, value);
    }

    private readonly record struct MetabolismCase(
        EntityUid Body,
        EntityUid Liver,
        string SolutionName,
        ProtoId<ReagentPrototype> Reagent,
        ProtoId<MetabolismStagePrototype> Stage,
        FixedPoint2 Rate,
        IReadOnlySet<CMUMetabolismClass> Toxicity,
        bool InStasis);
}

[RegisterComponent]
public sealed partial class MetabolismToxicityProbeComponent : Component
{
    public int RateEvents;
    public int ReagentDamageEvents;
    public List<ProtoId<MetabolismStagePrototype>> Stages = [];
    public List<HashSet<CMUMetabolismClass>> ToxicitySnapshots = [];
    public List<FixedPoint2> ReagentDamageAmounts = [];
}

public sealed class MetabolismToxicityProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetabolismToxicityProbeComponent, MetabolismRateModifyEvent>(OnRate);
        SubscribeLocalEvent<MetabolismToxicityProbeComponent, OrganDamagedEvent>(OnOrganDamaged);
    }

    private static void OnRate(
        Entity<MetabolismToxicityProbeComponent> ent,
        ref MetabolismRateModifyEvent args)
    {
        ent.Comp.RateEvents++;
        ent.Comp.Stages.Add(args.Stage);
        ent.Comp.ToxicitySnapshots.Add(args.ToxicityClasses.ToHashSet());
    }

    private static void OnOrganDamaged(
        Entity<MetabolismToxicityProbeComponent> ent,
        ref OrganDamagedEvent args)
    {
        if (args.Source == OrganDamageSource.Reagent)
        {
            ent.Comp.ReagentDamageEvents++;
            ent.Comp.ReagentDamageAmounts.Add(args.Damage.DamageDict["Poison"]);
        }
    }
}
