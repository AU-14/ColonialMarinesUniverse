#pragma warning disable RA0002 // Seed queue deadlines without running the game's periodic refresh.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class WorkQueueBudgetRegressionTest : GameTest
{
    [Test]
    public async Task AreaAlertQueueSpansTicksWithoutDroppingItsTail()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<AreaInfoSystem>();
            var config = Server.ResolveDependency<IConfigurationManager>();
            var original = config.GetCVar(RMCCVars.RMCMaxTacmapAlertProcessTimeMilliseconds);
            var pending = Field<Queue<Entity<AreaInfoComponent>>>(system, "_marineAlertCopyQueue");
            var entities = new List<EntityUid>();
            try
            {
                config.SetCVar(RMCCVars.RMCMaxTacmapAlertProcessTimeMilliseconds, 0f);
                pending.Clear();
                for (var i = 0; i < 4; i++)
                {
                    var uid = SEntMan.SpawnEntity(null, map.GridCoords);
                    entities.Add(uid);
                    var area = SEntMan.AddComponent<AreaInfoComponent>(uid);
                    system.SetNextUpdateTime((uid, area), TimeSpan.MaxValue);
                    pending.Enqueue((uid, area));
                }

                for (var i = 0; i < entities.Count; i++)
                {
                    Assert.That(pending.Peek().Owner, Is.EqualTo(entities[i]));
                    system.Update(0f);
                    Assert.That(pending.Count, Is.EqualTo(entities.Count - i - 1));
                }
            }
            finally
            {
                pending.Clear();
                foreach (var entity in entities) SEntMan.DeleteEntity(entity);
                config.SetCVar(RMCCVars.RMCMaxTacmapAlertProcessTimeMilliseconds, original);
            }
        });
    }

    [Test]
    public async Task OverwatchSharesBudgetAcrossSquadsAndReleasesCompletedReferences()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<SharedOverwatchConsoleSystem>();
            var config = Server.ResolveDependency<IConfigurationManager>();
            var original = config.GetCVar(RMCCVars.RMCOverwatchMaxProcessTimeMilliseconds);
            var pending = Field<Dictionary<Entity<SquadTeamComponent>, Queue<EntityUid>>>(system, "_toProcess");
            var removed = Field<HashSet<Entity<SquadTeamComponent>>>(system, "_toRemove");
            var entities = new List<EntityUid>();
            var members = new List<EntityUid>();
            try
            {
                config.SetCVar(RMCCVars.RMCOverwatchMaxProcessTimeMilliseconds, 0f);
                pending.Clear();
                for (var squadIndex = 0; squadIndex < 2; squadIndex++)
                {
                    var squad = SEntMan.SpawnEntity(null, map.GridCoords);
                    entities.Add(squad);
                    var component = SEntMan.AddComponent<SquadTeamComponent>(squad);
                    var queue = new Queue<EntityUid>();
                    pending.Add((squad, component), queue);
                    for (var i = 0; i < 3; i++)
                    {
                        var member = SEntMan.SpawnEntity(null, map.GridCoords);
                        entities.Add(member);
                        members.Add(member);
                        queue.Enqueue(member);
                    }
                }

                for (var i = 0; i < members.Count; i++)
                {
                    system.Update(0f);
                    Assert.That(members.Count(uid => SEntMan.HasComponent<OverwatchDataComponent>(uid)),
                        Is.EqualTo(i + 1), "A single budget must apply to all squads in this tick.");
                    Assert.That(removed, Is.Empty, "Completed squad components must not remain retained.");
                }
                Assert.That(pending, Is.Empty);
            }
            finally
            {
                pending.Clear();
                removed.Clear();
                foreach (var entity in entities) SEntMan.DeleteEntity(entity);
                config.SetCVar(RMCCVars.RMCOverwatchMaxProcessTimeMilliseconds, original);
            }
        });
    }

    private static T Field<T>(object system, string name) where T : class =>
        (T) typeof(SharedOverwatchConsoleSystem).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(system)!
        ?? (T) typeof(AreaInfoSystem).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system)!;
}
