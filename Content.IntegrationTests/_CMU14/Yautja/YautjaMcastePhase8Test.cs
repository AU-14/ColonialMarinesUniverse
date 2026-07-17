using Content.IntegrationTests.Pair;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Actions.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaMcastePhase8Test
{
    [Test]
    public async Task CannonPackRejectsNullspaceUserBeforeDrainLikeCmss13NoLocGuard()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaUsePlasmaCannons", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var cannon = packComp.Cannon!.Value;

                var ev = new YautjaUsePlasmaCannonsActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(pack, ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 2000),
                        "CMSS13 /obj/item/yautja_cannon_pack/cannon_internal() returns on !user.loc before the 50-power drain.");
                    Assert.That(packComp.CannonsDeployed, Is.False);
                    Assert.That(hands.IsHolding(hunter, cannon), Is.False);
                    Assert.That(packComp.CannonContainer!.Contains(cannon), Is.True);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, pack, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackRejectsNonHumanUserBeforeDrainLikeCmss13CannonInternal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();

            var xeno = entMan.SpawnEntity("CMXenoRunner", map.GridCoords);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaUsePlasmaCannons", map.GridCoords);

            try
            {
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var cannon = packComp.Cannon!.Value;

                var ev = new YautjaUsePlasmaCannonsActionEvent
                {
                    Performer = xeno,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(pack, ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 2000),
                        "CMSS13 /obj/item/yautja_cannon_pack/cannon_internal() returns on !ishuman(user) before the 50-power drain.");
                    Assert.That(packComp.CannonsDeployed, Is.False);
                    Assert.That(hands.IsHolding(xeno, cannon), Is.False);
                    Assert.That(packComp.CannonContainer!.Contains(cannon), Is.True);
                });
            }
            finally
            {
                foreach (var uid in new[] { xeno, pack, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackDestroyDeletesInternalCannonLikeCmss13Destroy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", MapCoordinates.Nullspace);
            var cannon = EntityUid.Invalid;

            try
            {
                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                cannon = packComp.Cannon!.Value;

                Assert.That(entMan.Deleted(cannon), Is.False);

                entMan.DeleteEntity(pack);

                Assert.That(entMan.Deleted(cannon) || entMan.IsQueuedForDeletion(cannon), Is.True,
                    "CMSS13 /obj/item/yautja_cannon_pack/Destroy() QDEL_NULLs the internal dual plasma cannon.");
            }
            finally
            {
                if (!entMan.Deleted(pack))
                    entMan.DeleteEntity(pack);
                if (cannon != EntityUid.Invalid && !entMan.Deleted(cannon))
                    entMan.DeleteEntity(cannon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SoldierBracersAutoArmSelfDestructWhenWearerDiesLikeCmss13Process()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var timing = server.ResolveDependency<IGameTiming>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaSoldierBracers", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                mobState.ChangeMobState(hunter, MobState.Dead);

                Assert.Multiple(() =>
                {
                    Assert.That(bracerComp.SelfDestructArmed, Is.True,
                        "CMSS13 /obj/item/clothing/gloves/yautja/hunter/soldier/process() calls explode(human_holder) when the wearer is DEAD.");
                    Assert.That(bracerComp.User, Is.EqualTo(hunter),
                        "The death-triggered explosion should stay centered on the wearer like CMSS13 explode(human_holder).");

                    var countdown = bracerComp.SelfDestructAt - timing.CurTime;
                    Assert.That(countdown, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(7.2)),
                        "CMSS13 bracer explode() uses do_after(victim, rand(72, 80), ...), with BYOND deciseconds.");
                    Assert.That(countdown, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(8)),
                        "The MCaste auto self-destruct should reuse the source randomized bracer explosion window.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }
}
