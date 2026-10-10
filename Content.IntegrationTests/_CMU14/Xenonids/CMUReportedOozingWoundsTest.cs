using System.Numerics;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Xenonids;

[TestFixture]
public sealed class CMUReportedOozingWoundsTest
{
    [Test]
    public async Task OverlappingDelayedSpraysDamageOncePerCast()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid caster = default, target = default, action = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            caster = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<XenoDespoilerComponent>(caster);
            action = entities.SpawnEntity(null, map.GridCoords);
            var actionComp = entities.AddComponent<ActionComponent>(action);
            var oozing = entities.AddComponent<XenoDespoilerOozingWoundsActionComponent>(action);
            oozing.BaseRadius = 2;
            oozing.LingeringAcidChance = 0; // Isolate spray damage from the separate puddle mechanic.
            target = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0.5f)));
            var cast = new XenoDespoilerOozingWoundsActionEvent { Performer = caster, Action = (action, actionComp) };
            entities.EventBus.RaiseLocalEvent(caster, cast);
            Assert.That(cast.Handled, Is.True);
        });

        await pair.Server.WaitRunTicks(40);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<DamageableSystem>().GetAllDamage((target, null)).DamageDict["Heat"],
                Is.EqualTo((FixedPoint2)30), "A body straddling delayed spray tiles gets one hit.");
            entities.System<SharedTransformSystem>().SetCoordinates(target, map.GridCoords.Offset(new Vector2(4, 0)));
        });
        await pair.Server.WaitRunTicks(2);
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.System<SharedTransformSystem>()
            .SetCoordinates(target, map.GridCoords.Offset(new Vector2(1, 0.5f))));
        await pair.Server.WaitRunTicks(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<DamageableSystem>().GetAllDamage((target, null)).DamageDict["Heat"],
                Is.EqualTo((FixedPoint2)30), "Re-entering the same cast does not damage again.");
            var cast = new XenoDespoilerOozingWoundsActionEvent
            {
                Performer = caster,
                Action = (action, entities.GetComponent<ActionComponent>(action)),
            };
            entities.EventBus.RaiseLocalEvent(caster, cast);
            Assert.That(cast.Handled, Is.True);
        });
        await pair.Server.WaitRunTicks(40);
        await pair.Server.WaitAssertion(() => Assert.That(
            pair.Server.EntMan.System<DamageableSystem>().GetAllDamage((target, null)).DamageDict["Heat"],
            Is.EqualTo((FixedPoint2)60), "A later cast has its own victim set."));
        await pair.CleanReturnAsync();
    }
}
