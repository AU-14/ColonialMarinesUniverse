#pragma warning disable RA0002 // Controlled fixture setup for state replication and lifecycle behavior.
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Round;
using Content.Server._RMC14.Ladder;
using Content.Server._RMC14.Overwatch;
using Content.Server._RMC14.Xenonids.Watch;
using Content.Shared._RMC14.Ladder;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Xenomorphs.Pathogen;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameStates;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class NetworkWorkRegressionTest : GameTest
{
    [Test]
    public async Task HandSwitchUsesDeltaAndPreservesFullStateFallback()
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
        var map = await Pair.CreateTestMap();
        EntityUid holder = default;
        NetEntity net = default;
        HandsComponent hands = null;
        GameTick from = default;
        HandsComponentState initial = null;
        await Server.WaitAssertion(() =>
        {
            holder = SEntMan.SpawnEntity(null, map.GridCoords);
            hands = SEntMan.AddComponent<HandsComponent>(holder);
            var system = Server.System<SharedHandsSystem>();
            system.AddHand((holder, hands), "left", new Hand(HandLocation.Left));
            system.AddHand((holder, hands), "right", new Hand(HandLocation.Right));
            initial = (HandsComponentState)SEntMan.GetComponentState(SEntMan.EventBus, hands, ServerSession!, GameTick.Zero)!;
            net = SEntMan.GetNetEntity(holder);
            Server.System<PvsOverrideSystem>().AddSessionOverride(holder, ServerSession!);
        });
        try
        {
            await Pair.RunTicksSync(15);
            await Server.WaitPost(() => from = SGameTiming.CurTick);
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<SharedHandsSystem>().TrySetActiveHand((holder, hands), "right"), Is.True);
                var state = SEntMan.GetComponentState(SEntMan.EventBus, hands, ServerSession!, from);
                Assert.That(state, Is.TypeOf<HandsComponentActiveHandDeltaState>());
                var merged = ((HandsComponentActiveHandDeltaState)state!).CreateNewFullState(initial);
                Assert.That(merged.ActiveHandId, Is.EqualTo("right"));
                Assert.That(initial.ActiveHandId, Is.EqualTo("left"));
                Assert.That(merged.Hands.Keys, Is.EquivalentTo(initial.Hands.Keys));
            });
            await Pair.RunTicksSync(15);
            await Client.WaitAssertion(() =>
                Assert.That(CEntMan.GetComponent<HandsComponent>(CEntMan.GetEntity(net)).ActiveHandId, Is.EqualTo("right")));
            await Server.WaitAssertion(() =>
            {
                hands.ShowInHands = false;
                SEntMan.Dirty(holder, hands);
                var full = SEntMan.GetComponentState(SEntMan.EventBus, hands, ServerSession!, from);
                Assert.That(full, Is.TypeOf<HandsComponentState>());
                Assert.That(((HandsComponentState)full!).ShowInHands, Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(holder));
        }
    }

    [Test]
    public async Task DropshipViewCleanupSurvivesFactionChanges()
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var original = ServerSession!.AttachedEntity;
            var actor = SEntMan.SpawnEntity(null, map.GridCoords);
            var terminal = SEntMan.SpawnEntity(null, map.GridCoords);
            var target = SEntMan.SpawnEntity(null, map.GridCoords);
            var console = SEntMan.EnsureComponent<DropshipTerminalWeaponsComponent>(terminal);
            var whitelist = SEntMan.EnsureComponent<WhitelistedShuttleComponent>(terminal);
            var signal = SEntMan.EnsureComponent<DropshipTargetComponent>(target);
            console.Target = target;
            whitelist.Faction = "govfor";
            signal.CreatorFaction = "govfor";
            Server.PlayerMan.SetAttachedEntity(ServerSession, actor);
            var system = Server.System<DropshipWeaponSystem>();
            void Call(string method) => typeof(DropshipWeaponSystem)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(system,
                    [new Entity<DropshipTerminalWeaponsComponent>(terminal, console), new Entity<ActorComponent?>(actor, null)]);
            try
            {
                Call("AddPvs");
                Assert.That(ServerSession.ViewSubscriptions, Does.Contain(target));
                signal.CreatorFaction = "opfor";
                Call("RemovePvs");
                Assert.That(ServerSession.ViewSubscriptions, Does.Not.Contain(target));
                Call("AddPvs");
                Assert.That(ServerSession.ViewSubscriptions, Does.Not.Contain(target), "Admission must still enforce factions.");
            }
            finally
            {
                Server.PlayerMan.SetAttachedEntity(ServerSession, original);
                SEntMan.DeleteEntity(actor);
                SEntMan.DeleteEntity(terminal);
                SEntMan.DeleteEntity(target);
            }
        });
    }

    [TestCase("ladder", false)]
    [TestCase("overwatch", false)]
    [TestCase("xeno", false)]
    [TestCase("ladder", true)]
    [TestCase("overwatch", true)]
    [TestCase("xeno", true)]
    public async Task SwitchingAndRemovingWatchersReleasesOldViews(string kind, bool detach)
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var original = ServerSession!.AttachedEntity;
            var watcher = SEntMan.SpawnEntity(null, map.GridCoords);
            var first = SEntMan.SpawnEntity(null, map.GridCoords);
            var second = SEntMan.SpawnEntity(null, map.GridCoords);
            var hive = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<EyeComponent>(watcher);
            SEntMan.EnsureComponent<HiveComponent>(hive);
            SEntMan.EnsureComponent<CMUPathogenHiveMemberComponent>(watcher);
            foreach (var uid in new[] { watcher, first, second })
                SEntMan.EnsureComponent<HiveMemberComponent>(uid).Hive = hive;
            Server.PlayerMan.SetAttachedEntity(ServerSession, watcher);

            void Watch(EntityUid target)
            {
                if (kind == "xeno")
                    Server.System<XenoWatchSystem>().Watch(watcher, target);
                else if (kind == "ladder")
                {
                    var comp = SEntMan.EnsureComponent<LadderComponent>(target);
                    typeof(LadderSystem).GetMethod("Watch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
                        Server.System<LadderSystem>(), [new Entity<ActorComponent?, EyeComponent?>(watcher, null, null),
                            new Entity<LadderComponent?>(target, comp)]);
                }
                else
                {
                    var comp = SEntMan.EnsureComponent<OverwatchCameraComponent>(target);
                    typeof(OverwatchConsoleSystem).GetMethod("Watch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
                        Server.System<OverwatchConsoleSystem>(), [new Entity<ActorComponent?, EyeComponent?>(watcher, null, null),
                            new Entity<OverwatchCameraComponent?>(target, comp)]);
                }
            }

            try
            {
                Watch(first);
                Assert.That(ServerSession.ViewSubscriptions, Does.Contain(first));
                Watch(second);
                Assert.That(ServerSession.ViewSubscriptions, Does.Not.Contain(first));
                Assert.That(ServerSession.ViewSubscriptions, Does.Contain(second));
                Watch(second);
                Assert.That(ServerSession.ViewSubscriptions, Does.Contain(second), "Watching the same target must retain its subscription.");
                if (detach) Server.PlayerMan.SetAttachedEntity(ServerSession, original);
                else if (kind == "xeno") SEntMan.RemoveComponent<XenoWatchingComponent>(watcher);
                else if (kind == "ladder") SEntMan.RemoveComponent<LadderWatchingComponent>(watcher);
                else SEntMan.RemoveComponent<OverwatchWatchingComponent>(watcher);
                Assert.That(ServerSession.ViewSubscriptions, Does.Not.Contain(second));
                Assert.That(SEntMan.GetComponent<EyeComponent>(watcher).Target, Is.Null);
            }
            finally
            {
                Server.PlayerMan.SetAttachedEntity(ServerSession, original);
                SEntMan.DeleteEntity(watcher);
                SEntMan.DeleteEntity(first);
                SEntMan.DeleteEntity(second);
                SEntMan.DeleteEntity(hive);
            }
        });
    }

    [Test]
    public async Task BodyHealthUsesDeltasAndUnchangedAssignmentsDoNotDirty()
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
        var map = await Pair.CreateTestMap();
        EntityUid part = default;
        BodyPartHealthComponent health = null;
        NetEntity net = default;
        await Server.WaitAssertion(() =>
        {
            part = SEntMan.SpawnEntity(null, map.GridCoords);
            health = SEntMan.AddComponent<BodyPartHealthComponent>(part);
            health.Max = 140;
            health.Current = 90;
            health.SeveranceDamage = 40;
            health.SeveranceThreshold = 275;
            health.PassiveHealMultiplier = 0;
            SEntMan.Dirty(part, health);
            net = SEntMan.GetNetEntity(part);
            Server.System<PvsOverrideSystem>().AddSessionOverride(part, ServerSession!);
        });
        await Pair.RunTicksSync(15);
        GameTick before = default;
        await Server.WaitAssertion(() =>
        {
            before = SGameTiming.CurTick;
            var dirty = health.LastModifiedTick;
            Server.System<SharedBodyPartHealthSystem>().SetCurrent((part, health), health.Current);
            Assert.That(health.LastModifiedTick, Is.EqualTo(dirty));
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedBodyPartHealthSystem>().SetCurrent((part, health), FixedPoint2.New(95));
            var state = SEntMan.GetComponentState(SEntMan.EventBus, health, ServerSession!, before);
            Assert.That(state, Is.InstanceOf<IComponentDeltaState>());
        });
        await Pair.RunTicksSync(15);
        await Client.WaitAssertion(() =>
        {
            var received = CEntMan.GetComponent<BodyPartHealthComponent>(CEntMan.GetEntity(net));
            Assert.That(received.Current, Is.EqualTo(FixedPoint2.New(95)));
            Assert.That(received.SeveranceDamage, Is.EqualTo(FixedPoint2.New(40)));
        });
        await Server.WaitAssertion(() =>
        {
            before = SGameTiming.CurTick;
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedBodyPartHealthSystem>().SetCurrent((part, health), FixedPoint2.New(120));
            var state = SEntMan.GetComponentState(SEntMan.EventBus, health, ServerSession!, before);
            Assert.That(state, Is.Not.InstanceOf<IComponentDeltaState>(),
                "Changing both healing fields explicitly dirties the full component state.");
            Assert.That(health.SeveranceDamage, Is.EqualTo(FixedPoint2.New(20)));
        });
        await Pair.RunTicksSync(15);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(net, out var local), Is.True);
            var received = CEntMan.GetComponent<BodyPartHealthComponent>(local!.Value);
            Assert.Multiple(() =>
            {
                Assert.That(received.Current, Is.EqualTo(FixedPoint2.New(120)));
                Assert.That(received.SeveranceDamage, Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(received.Max, Is.EqualTo(FixedPoint2.New(140)));
                Assert.That(received.SeveranceThreshold, Is.EqualTo(FixedPoint2.New(275)));
            });
        });
        await Server.WaitPost(() => SEntMan.DeleteEntity(part));
    }
}
