using System.Numerics;
using Content.Shared._RMC14.Xenonids.Alchemist;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
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
}
