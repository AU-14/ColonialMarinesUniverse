using System.Linq;
using System.Numerics;
using Content.Shared._CMU14.Chemistry.Effects.Negative;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects.Negative;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Alchemist;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoAlchemistTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMXenoSpitterAlchemist
  id: RMCTestXenoSpitterAlchemistStocked
  components:
  - type: XenoAlchemist
    sagunine: 2
    cholinine: 3
    noctine: 4

- type: entity
  parent: CMXenoSpitterAlchemist
  id: RMCTestXenoSpitterAlchemistFull
  components:
  - type: XenoAlchemist
    sagunine: 20
    selectedChemical: Cholinine

- type: entity
  parent: CMXenoSpitterAlchemist
  id: RMCTestXenoSpitterAlchemistNoctineFull
  components:
  - type: XenoAlchemist
    noctine: 20

- type: entity
  parent: CMXenoSpitterAlchemist
  id: RMCTestXenoSpitterAlchemistCrynineFull
  components:
  - type: XenoAlchemist
    cholinine: 10
    noctine: 10

- type: entity
  parent: CMXenoSpitterAlchemist
  id: RMCTestXenoSpitterAlchemistPyrinineFull
  components:
  - type: XenoAlchemist
    sagunine: 10
    cholinine: 10
";

    private static readonly string[] AlchemistReagents =
    [
        "RMCXenoAlchBrute",
        "RMCXenoAlchBurn",
        "RMCXenoAlchPain",
        "RMCXenoAlchFire",
        "RMCXenoAlchBloodloss",
        "RMCXenoAlchFreeze",
        "RMCXenoAlchPurge",
    ];

    private static readonly ProtoId<ReagentPrototype> AlchemistBrute = "RMCXenoAlchBrute";
    private static readonly ProtoId<ReagentPrototype> AlchemistBurn = "RMCXenoAlchBurn";
    private static readonly ProtoId<ReagentPrototype> AlchemistPain = "RMCXenoAlchPain";
    private static readonly ProtoId<ReagentPrototype> AlchemistFire = "RMCXenoAlchFire";
    private static readonly ProtoId<ReagentPrototype> AlchemistBloodloss = "RMCXenoAlchBloodloss";
    private static readonly ProtoId<ReagentPrototype> AlchemistFreeze = "RMCXenoAlchFreeze";
    private static readonly ProtoId<ReagentPrototype> AlchemistPurge = "RMCXenoAlchPurge";

    [Test]
    public async Task TailInjectionCannotTargetSameHiveXenos()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var alchemist = entMan.SpawnEntity("RMCTestXenoSpitterAlchemistStocked", map.GridCoords);
            var target = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = entMan.SpawnEntity("ActionXenoTailInjection", map.GridCoords);
            var directAction = SpawnAction(entMan);
            var hiveSystem = entMan.System<SharedXenoHiveSystem>();

            try
            {
                var comp = entMan.GetComponent<XenoAlchemistComponent>(alchemist);

                hiveSystem.SetHive(alchemist, hive);
                hiveSystem.SetHive(target, hive);

                var ev = new ActionValidateEvent
                {
                    Input = new RequestPerformActionEvent(
                        entMan.GetNetEntity(action),
                        entMan.GetNetEntity(target),
                        default(GameTick)),
                    User = alchemist,
                    Provider = alchemist,
                };

                entMan.EventBus.RaiseLocalEvent(action, ref ev);
                RaiseTailInjection(entMan, alchemist, target, directAction);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Invalid, Is.True);
                    Assert.That(comp.Sagunine + comp.Cholinine + comp.Noctine, Is.EqualTo(9));
                });
            }
            finally
            {
                entMan.DeleteEntity(hive);
                entMan.DeleteEntity(alchemist);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action);
                entMan.DeleteEntity(directAction.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TailInjectionInjectsRealChemicalIntoHumans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            var alchemist = entMan.SpawnEntity("RMCTestXenoSpitterAlchemistStocked", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                var comp = entMan.GetComponent<XenoAlchemistComponent>(alchemist);

                RaiseTailInjection(entMan, alchemist, target, action);

                Assert.That(solutions.TryGetInjectableSolution(target, out _, out var solution), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(solution!.GetTotalPrototypeQuantity("RMCXenoAlchPurge"), Is.EqualTo(FixedPoint2.New(9)));
                    Assert.That(TotalDamage(entMan, target), Is.EqualTo(20));
                    Assert.That(comp.Sagunine + comp.Cholinine + comp.Noctine, Is.Zero);
                });
            }
            finally
            {
                entMan.DeleteEntity(alchemist);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlchemistChemicalsAreRealToxinReagents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var id in AlchemistReagents)
            {
                var reagent = prototypes.Index<ReagentPrototype>(id);
                Assert.That(reagent.Toxin, Is.True, id);
                Assert.That(reagent.Metabolisms!.ContainsKey("Poison"), Is.True, id);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlchemistChemicalsApplyPressureOverFiftySeconds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var brute = prototypes.Index(AlchemistBrute);
            var burn = prototypes.Index(AlchemistBurn);
            var pain = prototypes.Index(AlchemistPain);
            var fire = prototypes.Index(AlchemistFire);
            var bloodloss = prototypes.Index(AlchemistBloodloss);
            var freeze = prototypes.Index(AlchemistFreeze);
            var purge = prototypes.Index(AlchemistPurge);

            foreach (var reagent in new[] { brute, burn, pain, fire, bloodloss, freeze, purge })
            {
                Assert.That(GetPoison(reagent).MetabolismRate, Is.EqualTo(FixedPoint2.New(0.4f)), reagent.ID);
            }

            Assert.Multiple(() =>
            {
                Assert.That(GetEffect<Biocidic>(brute).Potency, Is.EqualTo(4), brute.ID);
                Assert.That(GetEffect<Corrosive>(burn).Potency, Is.EqualTo(4), burn.ID);
                Assert.That(GetEffect<RMCAlchemistPain>(pain).Potency, Is.EqualTo(8), pain.ID);
                Assert.That(GetEffect<Corrosive>(fire).Potency, Is.EqualTo(4), fire.ID);
                Assert.That(GetEffect<Hemolytic>(bloodloss).Potency, Is.EqualTo(2), bloodloss.ID);
                Assert.That(GetEffect<RMCAlchemistPurgeNonToxins>(purge).Amount, Is.EqualTo(FixedPoint2.New(0.4f)), purge.ID);
            });

            AssertAdjusts(brute, "CMBicaridine", -0.4f);
            AssertAdjusts(brute, "CMMeralyne", -0.4f);
            AssertAdjusts(burn, "CMKelotane", -0.4f);
            AssertAdjusts(burn, "CMDermaline", -0.4f);
            AssertAdjusts(pain, "CMUParacetamol", -0.4f);
            AssertAdjusts(pain, "CMUTramadol", -0.4f);
            AssertAdjusts(pain, "CMUOxycodone", -0.4f);
            AssertAdjusts(pain, "CMUSleen", -0.4f);
            AssertAdjusts(pain, "CMUSoporific", -0.4f);
            AssertAdjusts(bloodloss, "CMDexalin", -0.4f);
            AssertAdjusts(bloodloss, "CMDexalinPlus", -0.4f);
            AssertAdjusts(bloodloss, "CMInaprovaline", -0.4f);
            AssertAdjusts(bloodloss, "RMCIron", -0.4f);
            AssertAdjusts(freeze, "CMCryoxadone", -0.4f);
            AssertAdjusts(freeze, "CMClonexadone", -0.4f);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlchemistMeleeHitGeneratesTwoChemicalUnits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var alchemist = entMan.SpawnEntity("CMXenoSpitterAlchemist", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                var comp = entMan.GetComponent<XenoAlchemistComponent>(alchemist);
                var ev = new MeleeHitEvent([target], alchemist, alchemist, new DamageSpecifier(), null);

                entMan.EventBus.RaiseLocalEvent(alchemist, ev);

                Assert.Multiple(() =>
                {
                    Assert.That(comp.SlashGenerateAmount, Is.EqualTo(2));
                    Assert.That(comp.Sagunine, Is.EqualTo(2));
                });
            }
            finally
            {
                entMan.DeleteEntity(alchemist);
                entMan.DeleteEntity(target);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlchemistStockpileCapsAtTwentyTotal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var alchemist = entMan.SpawnEntity("RMCTestXenoSpitterAlchemistFull", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                var comp = entMan.GetComponent<XenoAlchemistComponent>(alchemist);
                var ev = new MeleeHitEvent([target], alchemist, alchemist, new DamageSpecifier(), null);

                entMan.EventBus.RaiseLocalEvent(alchemist, ev);

                Assert.Multiple(() =>
                {
                    Assert.That(comp.MaxStockpile, Is.EqualTo(20));
                    Assert.That(comp.Sagunine + comp.Cholinine + comp.Noctine, Is.EqualTo(20));
                    Assert.That(comp.Cholinine, Is.Zero);
                });
            }
            finally
            {
                entMan.DeleteEntity(alchemist);
                entMan.DeleteEntity(target);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoctineInjectionDazesHumanTargets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<SharedStatusEffectsSystem>();
            var alchemist = entMan.SpawnEntity("RMCTestXenoSpitterAlchemistNoctineFull", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                RaiseTailInjection(entMan, alchemist, target, action);

                Assert.That(status.HasStatusEffect(target, RMCDazedSystem.StatusEffectDazed), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(alchemist);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CrynineInjectionSlowsHumanTargets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var alchemist = entMan.SpawnEntity("RMCTestXenoSpitterAlchemistCrynineFull", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                RaiseTailInjection(entMan, alchemist, target, action);

                Assert.That(entMan.HasComponent<RMCSlowdownComponent>(target), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(alchemist);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PyrinineInjectionDazesHumanTargets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<SharedStatusEffectsSystem>();
            var alchemist = entMan.SpawnEntity("RMCTestXenoSpitterAlchemistPyrinineFull", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                RaiseTailInjection(entMan, alchemist, target, action);

                Assert.That(status.HasStatusEffect(target, RMCDazedSystem.StatusEffectDazed), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(alchemist);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static Entity<ActionComponent> SpawnAction(IEntityManager entMan)
    {
        var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        return (action, entMan.EnsureComponent<ActionComponent>(action));
    }

    private static void RaiseTailInjection(
        IEntityManager entMan,
        EntityUid xeno,
        EntityUid target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoTailInjectionActionEvent
        {
            Performer = xeno,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(xeno, ev);
    }

    private static float TotalDamage(IEntityManager entMan, EntityUid target)
    {
        return entMan.GetComponent<DamageableComponent>(target).Damage.GetTotal().Float();
    }

    private static ReagentEffectsEntry GetPoison(ReagentPrototype reagent)
    {
        Assert.That(reagent.Metabolisms, Is.Not.Null, reagent.ID);
        Assert.That(reagent.Metabolisms!.TryGetValue("Poison", out var poison), Is.True, reagent.ID);
        return poison!;
    }

    private static T GetEffect<T>(ReagentPrototype reagent) where T : EntityEffect
    {
        var effect = GetPoison(reagent).Effects.OfType<T>().SingleOrDefault();
        Assert.That(effect, Is.Not.Null, reagent.ID);
        return effect!;
    }

    private static void AssertAdjusts(ReagentPrototype reagent, string target, float amount)
    {
        var effect = GetPoison(reagent).Effects
            .OfType<AdjustReagent>()
            .SingleOrDefault(e => e.Reagent == target);

        Assert.That(effect, Is.Not.Null, $"{reagent.ID} should adjust {target}");
        Assert.That(effect!.Amount, Is.EqualTo(FixedPoint2.New(amount)), $"{reagent.ID} should adjust {target}");
    }
}
