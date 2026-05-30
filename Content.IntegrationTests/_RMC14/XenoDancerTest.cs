using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids.Dancer;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoDancerTest
{
    [Test]
    public async Task YellowMarksStillApplyToAliveCrowds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mob = entMan.System<MobStateSystem>();
            var dancer = entMan.SpawnEntity("RMCXenoPraetorianDancer", map.GridCoords);
            var center = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var aliveTargets = new List<EntityUid>();
            var criticalTargets = new List<EntityUid>();

            try
            {
                mob.ChangeMobState(center, MobState.Critical);

                for (var i = 0; i < 8; i++)
                {
                    var x = 1 + i % 4;
                    var y = 1 + i / 4;
                    var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(x, y)));
                    mob.ChangeMobState(target, MobState.Critical);
                    criticalTargets.Add(target);
                }

                for (var i = 0; i < 12; i++)
                {
                    var x = 1 + i % 4;
                    var y = 3 + i / 4;
                    aliveTargets.Add(entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(x, y))));
                }

                var ev = new MeleeHitEvent([center], dancer, dancer, new DamageSpecifier(), null);
                entMan.EventBus.RaiseLocalEvent(dancer, ev);

                var aliveMarked = aliveTargets.Count(target => entMan.HasComponent<XenoYellowMarkedComponent>(target));
                var criticalMarked = criticalTargets.Count(target => entMan.HasComponent<XenoYellowMarkedComponent>(target));
                Assert.Multiple(() =>
                {
                    Assert.That(aliveMarked, Is.EqualTo(5));
                    Assert.That(criticalMarked, Is.Zero);
                });
            }
            finally
            {
                entMan.DeleteEntity(dancer);
                entMan.DeleteEntity(center);

                foreach (var target in aliveTargets)
                    entMan.DeleteEntity(target);

                foreach (var target in criticalTargets)
                    entMan.DeleteEntity(target);
            }
        });

        await pair.CleanReturnAsync();
    }
}
