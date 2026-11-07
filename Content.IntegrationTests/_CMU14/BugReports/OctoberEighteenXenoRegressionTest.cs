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

    // StartLunge paralyzes the charger, and grid vehicles let downed mobs pass through them,
    // so the lunge never touched the hull and the crew never got shaken
    [Test]
    public async Task LungingChargerStillCollidesWithGridVehicles()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tank = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            var charger = SEntMan.SpawnEntity("RMCXenoCrusherCharger", map.GridCoords.Offset(new Vector2(6, 0)));
            var standing = SEntMan.System<StandingStateSystem>();
            standing.Down(charger, playSound: false, dropHeldItems: false, force: true);
            Assert.That(standing.IsDown(charger), Is.True);

            bool Cancelled()
            {
                var tankFixture = SEntMan.GetComponent<FixturesComponent>(tank).Fixtures.Values.First();
                var chargerFixture = SEntMan.GetComponent<FixturesComponent>(charger).Fixtures.Values.First();
                var ev = new PreventCollideEvent(tank, charger,
                    SEntMan.GetComponent<PhysicsComponent>(tank),
                    SEntMan.GetComponent<PhysicsComponent>(charger),
                    tankFixture,
                    chargerFixture);
                SEntMan.EventBus.RaiseLocalEvent(tank, ref ev);
                return ev.Cancelled;
            }

            // a plain downed xeno still slides under, that's the exemption working as meant
            Assert.That(Cancelled(), Is.True);

            SEntMan.EnsureComponent<XenoChargerStateComponent>(charger).MoveState = XenoChargerMoveState.Lunging;
            Assert.That(Cancelled(), Is.False);

            SEntMan.DeleteEntity(charger);
            SEntMan.DeleteEntity(tank);
        });
    }

    // death clears CurrentQueen, but only the reaper put it back on revival, so hive collapse
    // ended the round over a living queen
    [Test]
    public async Task RevivedQueenIsTheHiveQueenAgain()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var component = SEntMan.GetComponent<HiveComponent>(hive);
            var queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            SEntMan.System<SharedXenoHiveSystem>().SetHive(queen, hive);
            Assert.That(component.CurrentQueen, Is.EqualTo(queen));

            var mobStates = SEntMan.System<MobStateSystem>();
            mobStates.ChangeMobState(queen, MobState.Dead);
            Assert.That(component.CurrentQueen, Is.Null);

            mobStates.ChangeMobState(queen, MobState.Alive);
            Assert.That(component.CurrentQueen, Is.EqualTo(queen));

            SEntMan.DeleteEntity(queen);
            SEntMan.DeleteEntity(hive);
        });
    }
}
