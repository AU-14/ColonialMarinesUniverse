using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU.Xenomorphs;

public sealed class CMUNeoEvolutionSlotsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task FreeCasteSlotsAreConsumedAndDoNotBlockOtherCastes()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hive = SSpawnAtPosition("CMUPathogenHive", map.GridCoords);
            var hives = Server.System<SharedXenoHiveSystem>();
            EntityUid SpawnMember(string prototype)
            {
                var uid = SSpawnAtPosition(prototype, map.GridCoords);
                hives.SetHive(uid, hive);
                return uid;
            }
            typeof(SharedGameTicker).GetProperty(nameof(SharedGameTicker.RoundStartTimeSpan))!
                .SetValue(Server.System<GameTicker>(), SGameTiming.CurTime - TimeSpan.FromMinutes(30));
            var candidate = SpawnMember("CMU14XenoSprinter");
            SEntMan.RemoveComponent<RestrictEvolveOffWeedsComponent>(candidate);
            var evolution = SComp<XenoEvolutionComponent>(candidate);
            var neos = new[] { SpawnMember("CMU14XenoNeomorph"), SpawnMember("CMU14XenoNeomorph"), SpawnMember("CMU14XenoNeomorph") };
            var system = Server.System<XenoEvolutionSystem>();
            var canEvolve = typeof(XenoEvolutionSystem).GetMethod("CanEvolvePopup", BindingFlags.NonPublic | BindingFlags.Instance)!;
            bool CanEvolve(string prototype) => (bool) canEvolve.Invoke(system,
                new object[] { new Entity<XenoEvolutionComponent>(candidate, evolution), new EntProtoId(prototype), false })!;

            Assert.That(CanEvolve("CMU14XenoNeomorph"), Is.False,
                "An occupied free neomorph slot must not permit unlimited tier-two evolutions.");
            SEntMan.DeleteEntity(neos[1]);
            SEntMan.DeleteEntity(neos[2]);
            SpawnMember("CMU14XenoBlight");
            SpawnMember("CMU14XenoConditor");
            SpawnMember("CMU14XenoConditor");
            SpawnMember("CMU14XenoHarbinger");
            Assert.That(CanEvolve("CMU14XenoBlight"), Is.True,
                "Castes occupying free slots must not exhaust the ordinary tier-two allowance.");
        });
    }
}
