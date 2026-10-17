using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Charge.CursorCharge;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberEighteenXenoRegressionTest : GameTest
{
    private static readonly ProtoId<NpcFactionPrototype> Govfor = "GOVFOR";

    // the adaptability boon gathered every caste of the same tier, pathogen ones included,
    // so a normal hive could swap into neomorphs and blights
    [Test]
    public async Task AdaptabilityBoonDoesNotOfferPathogenCastes()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hiveSystem = SEntMan.System<SharedXenoHiveSystem>();
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var boon = SEntMan.SpawnEntity(null, map.GridCoords);
            var warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords);
            hiveSystem.SetHive(boon, hive);
            hiveSystem.SetHive(warrior, hive);

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new HiveBoonActivateAdaptabilityEvent { Boon = boon });

            var offered = SEntMan.GetComponent<XenoEvolutionComponent>(warrior).EvolvesToWithoutPoints;
            Assert.Multiple(() =>
            {
                // sanity, the boon still works for normal castes
                Assert.That(offered, Does.Contain(new EntProtoId("CMXenoLurker")));
                Assert.That(offered, Does.Not.Contain(new EntProtoId("CMU14XenoNeomorph")));
                Assert.That(offered, Does.Not.Contain(new EntProtoId("CMU14XenoBlight")));
            });

            SEntMan.DeleteEntity(warrior);
            SEntMan.DeleteEntity(boon);
            SEntMan.DeleteEntity(hive);
        });
    }

    // nubody gave xenos a real head part, and GibBody drops every part
    [Test]
    public async Task GibbedXenoDoesNotLeaveItsHeadBehind()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var drone = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            SEntMan.System<SharedBodySystem>().GibBody(drone);
        });
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            var heads = 0;
            var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (meta.EntityPrototype?.ID == "RMCHeadXeno" &&
                    xform.MapID == map.MapId &&
                    !containers.IsEntityInContainer(uid))
                {
                    heads++;
                }
            }

            Assert.That(heads, Is.Zero);
        });
    }

    // damage state landing on the client after a predicted heal read as "damage taken" and the
    // client handed itself hypertension stacks the server never had
    [Test]
    public async Task DespoilerHypertensionOnlyBuildsOnTheServer()
    {
        var map = await Pair.CreateTestMap();
        var server = EntityUid.Invalid;
        await Server.WaitPost(() => server = SSpawnAtPosition("RMCXenoDespoiler", map.GridCoords));
        await RunUntilSynced();

        var client = ToClientUid(server);
        await Client.WaitAssertion(() =>
        {
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 20;
            CEntMan.System<DamageableSystem>().TryChangeDamage(client, damage, ignoreResistances: true);

            var hyper = CEntMan.GetComponent<XenoDespoilerHypertensionComponent>(client);
            Assert.That(hyper.Points, Is.Zero);
            Assert.That(hyper.Stacks, Is.Zero);
        });

        await Server.WaitAssertion(() =>
        {
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 20;
            SEntMan.System<DamageableSystem>().TryChangeDamage(server, damage, ignoreResistances: true);

            // real hits still pay out
            Assert.That(SEntMan.GetComponent<XenoDespoilerHypertensionComponent>(server).Points, Is.GreaterThan(0));
            SEntMan.DeleteEntity(server);
        });
    }

    // kill threats paid intel for every hugger, lesser drone and popper
    [TestCase("CMXenoLesserDrone", 0)]
    [TestCase("CMUSwarmLesser", 0)]
    [TestCase("CMXenoParasite", 0)]
    [TestCase("CMU14XenoPopper", 0)]
    [TestCase("CMXenoDrone", 1)]
    public async Task KillThreatObjectiveIgnoresMinions(string prototype, int credited)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var killer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<NpcFactionSystem>().AddFaction(killer, Govfor);
            var objective = SEntMan.SpawnEntity("killthreatobjectiveds", map.GridCoords);
            var data = SEntMan.GetComponent<CMUObjectiveComponent>(objective);
            data.Active = true;
            data.Faction = "govfor";
            var kill = SEntMan.GetComponent<KillObjectiveComponent>(objective);
            kill.KillCount = 100;

            var mob = SEntMan.SpawnEntity(prototype, map.GridCoords.Offset(new Vector2(3, 0)));
            SEntMan.EnsureComponent<KillMarkedForComponent>(mob).AssociatedObjectives[objective] = "govfor";
            SEntMan.System<MobStateSystem>().ChangeMobState(mob, MobState.Dead, origin: killer);

            Assert.That(kill.AmountKilledPerFaction.GetValueOrDefault("govfor"), Is.EqualTo(credited));

            SEntMan.DeleteEntity(objective);
            SEntMan.DeleteEntity(killer);
        });
    }

    // hijack respawns players in the same tick it only queues the planet core for deletion,
    // and the surged flag that prefers the queen may not be set yet depending on handler order
    [Test]
    public async Task HijackRespawnSkipsAQueuedPlanetCore()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var component = SEntMan.GetComponent<HiveComponent>(hive);
            var core = SEntMan.SpawnEntity(null, map.GridCoords);
            var queen = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(10, 0)));
            SEntMan.EnsureComponent<HiveCoreComponent>(core);
            SEntMan.EnsureComponent<XenoEvolutionGranterComponent>(queen);
            var system = SEntMan.System<SharedXenoHiveSystem>();
            system.SetHive(core, hive);
            system.SetHive(queen, hive);
            Assert.That(component.HijackSurged, Is.False);

            SEntMan.QueueDeleteEntity(core);

            var spawn = typeof(SharedXenoHiveSystem).GetMethod("TryGetBurrowedLarvaSpawnPosition", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object[] args = [new Entity<HiveComponent>(hive, component), default(EntityCoordinates)];
            Assert.That(spawn.Invoke(system, args), Is.True);
            Assert.That(args[1], Is.EqualTo(SEntMan.GetComponent<TransformComponent>(queen).Coordinates));

            SEntMan.DeleteEntity(queen);
            SEntMan.DeleteEntity(hive);
        });
    }
}
