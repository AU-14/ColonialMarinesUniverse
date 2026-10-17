using System.Linq;
using System.Numerics;
using Content.Server.Explosion.Components;
using Content.Shared._RMC14.Line;
using Content.Shared._RMC14.Synth;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class ReportedCombatRegressionTest
{
    [TestCase("RMCAirBurstProjectileFrag")]
    [TestCase("RMCAirBurstProjectileIncendiary")]
    [TestCase("RMCAirBurstProjectileHornet")]
    public async Task AirburstEmitsItsPayloadWithoutTimerKey(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var shell = entities.SpawnEntity(prototype, map.GridCoords);
            var payload = entities.GetComponent<ProjectileGrenadeComponent>(shell);
            var count = payload.Capacity;
            var projectile = payload.FillPrototype;
            var before = entities.EntityQuery<MetaDataComponent>().Count(c => c.EntityPrototype?.ID == projectile);
            var trigger = entities.System<TriggerSystem>();
            trigger.Trigger(shell, key: "unrelated", predicted: false);
            Assert.That(payload.UnspawnedCount, Is.EqualTo(count));
            trigger.Trigger(shell, predicted: false);
            Assert.That(payload.UnspawnedCount, Is.Zero);
            Assert.That(entities.EntityQuery<MetaDataComponent>().Count(c => c.EntityPrototype?.ID == projectile) - before,
                Is.EqualTo(count), "The airburst must emit damaging fragments as well as its visual effect.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FlameSpreadCannotCrossClosedAirlock(bool thick)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            for (var x = 0; x <= 8; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), new Tile(tiles["Plating"].TileId));

            var start = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
            var end = start.Offset(new Vector2(8, 0));
            var airlocks = new EntityUid[5];
            for (var y = -2; y <= 2; y++)
                airlocks[y + 2] = entities.SpawnEntity("CMAirlock", start.Offset(new Vector2(3, y)));
            var line = entities.System<LineSystem>();
            var transform = entities.System<SharedTransformSystem>();
            var closed = line.DrawLine(start, end, TimeSpan.Zero, 8, out _, hitBlocker: true, thick: thick);
            Assert.That(closed, Is.Not.Empty);
            Assert.That(closed.All(tile => transform.WithEntityId(transform.ToCoordinates(tile.Coordinates), map.Grid).X < 3),
                Is.True, "Neither the airlock tile nor tiles behind it may ignite.");
            #pragma warning disable RA0002 // Exercise line geometry with each settled door state, without a powered-door timer.
            foreach (var airlock in airlocks)
                entities.GetComponent<DoorComponent>(airlock).State = DoorState.Open;
            Assert.That(line.CanReachTile(start, end, true), Is.True);
            foreach (var airlock in airlocks)
                entities.GetComponent<DoorComponent>(airlock).State = DoorState.Closed;
            #pragma warning restore RA0002
            Assert.That(line.CanReachTile(start, end, true), Is.False, "A door closed after firing must stop delayed flames.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("Blunt", 50)]
    [TestCase("Piercing", 50)]
    [TestCase("Heat", 90)]
    public async Task SynthResistanceSurvivesReplacingDamageable(string type, int expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var target = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent<DamageableComponent>(target);
            entities.AddComponent<InjurableComponent>(target);
            entities.AddComponent<SynthComponent>(target);
            entities.RemoveComponent<DamageableComponent>(target);
            entities.AddComponent<DamageableComponent>(target);
            var damage = new DamageSpecifier(pair.Server.ResolveDependency<IPrototypeManager>().Index<DamageTypePrototype>(type), FixedPoint2.New(100));
            var result = entities.System<DamageableSystem>().TryChangeDamage(target, damage);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.GetTotal(), Is.EqualTo(FixedPoint2.New(expected)));
            entities.DeleteEntity(target);
        });
        await pair.CleanReturnAsync();
    }
}
