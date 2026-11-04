using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Maturing;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUQueenCallPlanetTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ImmatureQueenCanCallFromColonyButNotShipMap(bool underground)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipHijackInitialDelayMinutes, 0);
            var queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            SEntMan.EnsureComponent<XenoMaturingComponent>(queen);
            var console = SEntMan.SpawnEntity(null, map.GridCoords);
            var system = SEntMan.System<SharedDropshipSystem>();
            var check = typeof(SharedDropshipSystem).GetMethod("TryDropshipHijackPopup", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object[] args = [console, new Entity<DropshipHijackerComponent?>(queen, SEntMan.GetComponent<DropshipHijackerComponent>(queen)), false];
            Assert.That((bool) check.Invoke(system, args)!, Is.False);
            if (underground)
            {
                var surface = SEntMan.System<SharedMapSystem>().CreateMap(out _);
                var zLevels = SEntMan.System<CMUZLevelsSystem>();
                var network = zLevels.CreateZNetwork();
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
                {
                    [surface] = 0,
                    [SEntMan.GetComponent<TransformComponent>(queen).MapUid!.Value] = -1,
                }), Is.True);
                SEntMan.EnsureComponent<RMCPlanetComponent>(surface);
            }
            else
                SEntMan.EnsureComponent<RMCPlanetComponent>(map.Grid);
            Assert.That((bool) check.Invoke(system, args)!, Is.True, "Colony grids and underground levels must qualify for a queen call.");
        });
    }
}
