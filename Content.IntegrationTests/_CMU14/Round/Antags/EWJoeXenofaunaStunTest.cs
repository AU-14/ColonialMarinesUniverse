using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// ECS-11: EW Joe stuns are scaled by <see cref="EWJoeComponent.XenofaunaStunMultiplier"/> on xenofauna before
/// RMC's own xeno reduction, so a swept drone stays down longer than the base sweep, and humans are unchanged.
/// </summary>
[TestFixture]
public sealed class EWJoeXenofaunaStunTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Drone = "CMXenoDrone";

    [Test]
    public async Task SweepHoldsXenofaunaLongerThanTheBaseKnockdown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            // the same Joe without the multiplier, for the reference durations
            var plainJoe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var plainComp = entities.EnsureComponent<EWJoeComponent>(plainJoe);
            plainComp.XenofaunaStunMultiplier = 1f;

            var drone = entities.SpawnEntity(Drone, EWJoeTestHelpers.TileEast(entities, map, 2));
            var plainDrone = entities.SpawnEntity(Drone, EWJoeTestHelpers.TileEast(entities, map, 4));
            var human = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 6));
            var plainHuman = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 8));

            combat.Sweep((joe, comp), drone);
            combat.Sweep((plainJoe, plainComp), plainDrone);
            combat.Sweep((joe, comp), human);
            combat.Sweep((plainJoe, plainComp), plainHuman);

            var now = timing.CurTime;
            var scaled = DownFor(entities, drone, now);
            var unscaled = DownFor(entities, plainDrone, now);
            var baseTime = comp.SweepKnockdown;

            Assert.Multiple(() =>
            {
                Assert.That(unscaled, Is.GreaterThan(TimeSpan.Zero), "the reference drone was knocked down");
                Assert.That(scaled, Is.GreaterThan(unscaled), "the multiplier holds the drone longer");
                Assert.That(scaled.TotalSeconds, Is.EqualTo(unscaled.TotalSeconds * comp.XenofaunaStunMultiplier).Within(0.01),
                    "by exactly the Joe's xenofauna multiplier");
                Assert.That(scaled, Is.GreaterThanOrEqualTo(baseTime * 1.3),
                    "after RMC's xeno reduction the drone is still down about 1.3x the base sweep");
                Assert.That(DownFor(entities, human, now), Is.EqualTo(DownFor(entities, plainHuman, now)),
                    "humans are not xenofauna and are not scaled");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>How long <paramref name="uid"/> is down: its paralysis status, else its crawler knockdown.</summary>
    private static TimeSpan DownFor(IEntityManager entities, EntityUid uid, TimeSpan now)
    {
        var status = entities.System<StatusEffectsSystem>();
        // TryGetTime resolves the container with an error log when it is missing, so look first
        if (entities.TryGetComponent(uid, out StatusEffectContainerComponent? container)
            && status.TryGetTime(uid, SharedStunSystem.ParalyzeId, out var paralyze, container)
            && paralyze.EndEffectTime is { } end)
            return end - (paralyze.StartEffectTime ?? now);

        if (entities.TryGetComponent(uid, out KnockedDownComponent? knocked))
            return knocked.NextUpdate - now;

        return TimeSpan.Zero;
    }
}
