using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Systems;

namespace Content.IntegrationTests.CMU14.Atmos;

public sealed class CMUBarricadeFireTest : GameTest
{
    [Test]
    public async Task BarricadeStopsTakingDamageWhenItsTileFireIsExtinguished()
    {
        var map = await Pair.CreateTestMap();
        EntityUid barricade = default;
        EntityUid fire = default;
        await Server.WaitPost(() =>
        {
            barricade = SSpawnAtPosition("CMBarricadeMetal", map.GridCoords);
            fire = SSpawnAtPosition("RMCTileFire", map.GridCoords);
        });

        var damage = Server.System<DamageableSystem>();
        await Pair.RunSeconds(2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(damage.GetTotalDamage(barricade).Float(), Is.Positive);
            var spray = SSpawn("RMCExtinguisherSpray");
            var solution = SComp<SolutionComponent>(spray);
            solution.Solution.AddReagent("Water", 1);
            var hit = new VaporHitEvent((spray, solution), 7);
            SEntMan.EventBus.RaiseLocalEvent(fire, ref hit);
        });

        await Pair.RunTicksSync(3);
        float damageAfterExtinguishing = 0;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(fire), Is.True);
            damageAfterExtinguishing = damage.GetTotalDamage(barricade).Float();
        });
        await Pair.RunSeconds(4f);
        await Server.WaitAssertion(() =>
            Assert.That(damage.GetTotalDamage(barricade).Float(), Is.EqualTo(damageAfterExtinguishing)));
    }
}
