using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class ViewerBudgetRegressionTest : GameTest
{
    [Test]
    public async Task ViewerRefreshWaveSpansUpdatesAndEventuallyDrains()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<CMUZLevelsSystem>();
            var type = typeof(CMUZLevelsSystem);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var pending = (Queue<EntityUid>) type.GetField("_pendingProbeViewers", flags)!.GetValue(system)!;
            var queued = (HashSet<EntityUid>) type.GetField("_queuedProbeViewers", flags)!.GetValue(system)!;
            var update = type.GetMethod("UpdateView", flags)!;
            var config = Server.ResolveDependency<IConfigurationManager>();
            var originalBudget = config.GetCVar(CMUZLevelsCVars.ProbeBudgetMs);
            var entities = new List<EntityUid>();
            try
            {
                config.SetCVar(CMUZLevelsCVars.ProbeBudgetMs, 20f);
                for (var i = 0; i < 130; i++)
                {
                    var uid = SEntMan.SpawnEntity(null, map.GridCoords);
                    entities.Add(uid);
                    SEntMan.AddComponent<CMUZLevelViewerComponent>(uid);
                }
                pending.Clear();
                queued.Clear();
                type.GetField("_nextZLevelViewerUpdate", flags)!.SetValue(system, TimeSpan.Zero);
                update.Invoke(system, new object[] { 0f });
                Assert.That(pending.Count, Is.GreaterThanOrEqualTo(66), "At most 64 viewers may be reconciled in one update.");
                for (var i = 0; i < 130 && pending.Count > 0; i++)
                {
                    var before = pending.Count;
                    update.Invoke(system, new object[] { 0f });
                    Assert.That(pending.Count, Is.LessThan(before));
                }
                Assert.That(pending, Is.Empty);
                Assert.That(queued, Is.Empty);
            }
            finally
            {
                pending.Clear();
                queued.Clear();
                foreach (var entity in entities) SEntMan.DeleteEntity(entity);
                config.SetCVar(CMUZLevelsCVars.ProbeBudgetMs, originalBudget);
            }
        });
    }
}
