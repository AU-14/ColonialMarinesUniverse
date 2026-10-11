using System.Numerics;
using Content.Server.CMU14.Expeditions;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Expeditions;

public sealed partial class CMUExpeditionAdvancedAgentTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task SixGuardsCoordinateWithoutStarvationAndMeasureSearchCost(bool twoSquads)
    {
        EntityUid map = default;
        var guards = new List<EntityUid>();
        var rifles = new List<EntityUid>();
        var priorAmmo = new int[6];
        var shots = new int[6];
        var acquired = new bool[6];
        var previousSearch = new int[6];
        var searches = new List<double>();
        var routes = new List<double>();
        var sawFlank = false;
        await Server.WaitAssertion(() =>
        {
            var arena = Arena("CMUExpeditionScavengerAggressive");
            map = arena.Map;
            guards.Add(arena.Guard);
            var origin = SEntMan.GetComponent<TransformComponent>(arena.Guard).Coordinates;
            var offsets = new[] { new Vector2(-2, -5), new Vector2(-2, -3), new Vector2(-2, -1), new Vector2(-2, 2), new Vector2(-2, 5) };
            for (var i = 0; i < offsets.Length; i++)
                guards.Add(SEntMan.SpawnEntity(i % 2 == 0 ? "CMUExpeditionScavengerCautious" : "CMUExpeditionScavengerAggressive", origin.Offset(offsets[i])));
            for (var i = 0; i < guards.Count; i++)
            {
                SEntMan.GetComponent<CMUExpeditionAgentComponent>(guards[i]).Squad = twoSquads && i >= 3 ? 2 : 1;
                Assert.That(Server.System<GunSystem>().TryGetGun(guards[i], out var gun), Is.True);
                rifles.Add(gun.Owner);
                priorAmmo[i] = Ammo(gun.Owner);
            }
        });
        for (var sample = 0; sample < 120; sample++)
        {
            await Pair.RunSeconds(0.2f);
            await Server.WaitAssertion(() =>
            {
                var flankers = new Dictionary<int, int>();
                for (var i = 0; i < guards.Count; i++)
                {
                    var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guards[i]);
                    acquired[i] |= agent.Target != null;
                    var ammo = Ammo(rifles[i]);
                    shots[i] += Math.Max(0, priorAmmo[i] - ammo);
                    priorAmmo[i] = ammo;
                    if (agent.Action == CMUTacticalAction.Flank)
                    {
                        sawFlank = true;
                        flankers[agent.Squad] = flankers.GetValueOrDefault(agent.Squad) + 1;
                    }
                    Assert.That(agent.LastSearchCells, Is.LessThanOrEqualTo(256));
                    Assert.That(agent.LastRouteCells, Is.LessThanOrEqualTo(256));
                    if (previousSearch[i] != agent.Searches)
                    {
                        searches.Add(agent.LastSearchMilliseconds);
                        previousSearch[i] = agent.Searches;
                    }
                    if (agent.LastRouteMilliseconds > 0 && agent.Action == CMUTacticalAction.Flank)
                        routes.Add(agent.LastRouteMilliseconds);
                }
                Assert.That(flankers.Values, Has.All.LessThanOrEqualTo(1), "Only one member per squad may leave for a flank while teammates maintain pressure.");
            });
        }
        await Server.WaitAssertion(() =>
        {
            var details = guards.Select((g, i) =>
            {
                var a = SEntMan.GetComponent<CMUExpeditionAgentComponent>(g);
                return $"guard {i}: shots={shots[i]}, {a.State}/{a.Action}, flanks={a.Flanks}, failures={a.FailedPlans}, search mean={(a.Searches == 0 ? 0 : a.TotalSearchMilliseconds / a.Searches):F2}ms, max={a.MaxSearchMilliseconds:F2}ms, route={a.LastRouteMilliseconds:F2}ms";
            }).ToArray();
            TestContext.Progress.WriteLine($"six-guard contact; twoSquads={twoSquads}; searches={searches.Count}; p50={Percentile(searches, 0.5):F2}ms; p95={Percentile(searches, 0.95):F2}ms; max={searches.DefaultIfEmpty().Max():F2}ms\n{string.Join(Environment.NewLine, details)}");
            Assert.That(acquired, Has.All.True);
            Assert.That(shots, Has.All.GreaterThan(0), $"Every guard must contribute, including both squads: {string.Join(';', details)}");
            Assert.That(searches.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(sawFlank, Is.True, $"At least one covered flank should be attempted in a mixed six-person squad: {string.Join(';', details)}");
            Assert.That(guards.Sum(g => SEntMan.GetComponent<CMUExpeditionAgentComponent>(g).Flanks), Is.GreaterThan(0), "A coordinated flank must physically complete, not just receive a role label.");
            SEntMan.DeleteEntity(map);
        });
    }

    private static double Percentile(List<double> samples, double percentile)
    {
        if (samples.Count == 0)
            return 0;
        var sorted = samples.Order().ToArray();
        return sorted[(int) Math.Ceiling((sorted.Length - 1) * percentile)];
    }
}
