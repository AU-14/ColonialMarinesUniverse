using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Configure tracked fixtures without injecting blips into the tactical feed.
    [TestCase(false)]
    [TestCase(true)]
    public async Task ZLevelContactsSurviveFloorChangesAndRemainWatchable(bool ghost)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid viewer = default, drone = default, unlinked = default;
        byte[] terrain = null!;
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                Assert.That(SEntMan.HasComponent<TacticalMapComponent>(_lower), Is.False);
                viewer = SEntMan.SpawnEntity(ghost ? "MobObserver" : "CMXenoQueen", new EntityCoordinates(_upper, new Vector2(0.5f)));
                drone = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_lower, new Vector2(3.5f)));
                if (!ghost)
                {
                    var hive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(_upper, Vector2.Zero));
                    var hives = SEntMan.System<SharedXenoHiveSystem>();
                    hives.SetHive(viewer, hive);
                    hives.SetHive(drone, hive);
                }
                SComp<TacticalMapUserComponent>(viewer).LiveUpdate = true;
                Server.PlayerMan.SetAttachedEntity(session, viewer);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(viewer, new OpenTacticalMapActionEvent { Performer = viewer }));
            await Pair.RunTicksSync(60);
            var droneNet = SEntMan.GetNetEntity(drone);

            async Task AssertContact(int depth, Vector2i tile, bool click)
            {
                await Server.WaitAssertion(() =>
                {
                    var feed = SComp<TacticalMapComponent>(_upper).XenoBlips;
                    Assert.That(feed.ContainsKey(drone.Id), Is.True, "Moving to a linked floor must not break tracking.");
                    Assert.That(feed[drone.Id].Indices, Is.EqualTo(tile));
                    Assert.That(SComp<ActiveTacticalMapTrackedComponent>(drone).Map, Is.EqualTo(_upper));
                    Assert.That(SEntMan.HasComponent<TacticalMapComponent>(_lower), Is.False, "Do not create competing map feeds for each floor.");
                });
                await Client.WaitAssertion(() =>
                {
                    var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                    var contact = view.TrackedContacts.Single(c => c.Blip.Indices == tile);
                    Assert.That(contact.Depth, Is.EqualTo(depth));
                    Assert.That(contact.Blip.Image, Is.Not.Null, "Keep the actual tracked icon.");
                    Assert.That(contact.XenoWatchTarget, Is.EqualTo(ghost ? (NetEntity?) null : droneNet));
                    view.Measure(new Vector2(600, 400));
                    view.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(600, 400)));
                    view.SelectLevel(depth - view.Scene!.MinDepth);
                    view.RestoreCamera(view.CaptureCamera() with
                        { Center = (Vector2) tile + new Vector2(0.5f), Distance = 30, Overhead = true, Fit = false });
                    Assert.That(view.XenoAt(view.Size / 2), Is.EqualTo(ghost ? (NetEntity?) null : droneNet));
                    if (terrain == null) terrain = view.Scene.Cells;
                    else Assert.That(view.Scene.Cells, Is.SameAs(terrain), "Contact movement must not reload the frozen terrain.");
                    if (click) Click(view, view.Size / 2);
                });
                if (!click) return;
                await Pair.RunTicksSync(20);
                await Server.WaitAssertion(() => Assert.That(SComp<EyeComponent>(viewer).Target, Is.EqualTo(drone),
                    "The queen must be able to watch an icon on another floor."));
            }

            await AssertContact(-1, new Vector2i(3, 3), !ghost);
            await Server.WaitPost(() => SEntMan.System<SharedTransformSystem>().SetCoordinates(drone,
                new EntityCoordinates(_upper, new Vector2(5.5f, 4.5f))));
            await Pair.RunTicksSync(30);
            await AssertContact(0, new Vector2i(5, 4), false);
            await Server.WaitPost(() => SEntMan.System<SharedTransformSystem>().SetCoordinates(drone,
                new EntityCoordinates(_lower, new Vector2(-3.5f, 2.5f))));
            await Pair.RunTicksSync(30);
            await AssertContact(-1, new Vector2i(-4, 2), false);
            await Server.WaitPost(() =>
            {
                unlinked = _maps.CreateMap(runMapInit: true);
                SEntMan.System<SharedTransformSystem>().SetCoordinates(drone, new EntityCoordinates(unlinked, Vector2.Zero));
            });
            await Pair.RunTicksSync(30);
            await Server.WaitAssertion(() => Assert.That(SComp<TacticalMapComponent>(_upper).XenoBlips.ContainsKey(drone.Id), Is.False,
                "Leaving the z-level network must remove the old battlefield contact."));
            await Client.WaitAssertion(() =>
            {
                var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                Assert.That(view.TrackedContacts.Any(c => c.XenoWatchTarget == droneNet || c.Blip.Indices == new Vector2i(-4, 2)), Is.False);
            });
        }
        finally
        {
            if (Server.IsAlive) await Server.WaitPost(() =>
            {
                if (viewer.IsValid()) _ui.CloseUi(viewer, TacticalMapUserUi.Key, viewer);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var uid in new[] { viewer, drone, unlinked })
                    if (uid.IsValid() && SEntMan.EntityExists(uid)) SEntMan.DeleteEntity(uid);
            });
            if (Server.IsAlive && Client.IsAlive) await Pair.RunUntilSynced();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ZLevelContactsKeepFactionAndVehicleFeedsTogether(bool secondaryMap)
    {
        EntityUid friend = default, enemy = default, vehicle = default;
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
            if (secondaryMap) SEntMan.EnsureComponent<TacticalMapComponent>(_lower);
            var icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "rmc_commander");
            EntityUid Track(Vector2 position)
            {
                var uid = SEntMan.SpawnEntity(null, new EntityCoordinates(_lower, position));
                SEntMan.EnsureComponent<TacticalMapIconComponent>(uid).Icon = icon;
                SEntMan.EnsureComponent<TacticalMapTrackedComponent>(uid);
                return uid;
            }
            friend = Track(new Vector2(2.5f, 3.5f));
            SEntMan.EnsureComponent<GovforMapTrackedComponent>(friend);
            enemy = Track(new Vector2(6.5f));
            SEntMan.EnsureComponent<OpforMapTrackedComponent>(enemy);
            vehicle = Track(new Vector2(-3.5f));
            SEntMan.EnsureComponent<VehicleInteriorComponent>(vehicle);
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var root = SComp<TacticalMapComponent>(_upper);
            Assert.That(root.GovforBlips.ContainsKey(friend.Id), Is.True);
            Assert.That(root.OpforBlips.ContainsKey(enemy.Id), Is.True);
            Assert.That(root.MarineBlips.ContainsKey(vehicle.Id), Is.True, "Vehicles need the same floor resolution as personnel.");
            foreach (var uid in new[] { friend, enemy, vehicle })
                Assert.That(SComp<ActiveTacticalMapTrackedComponent>(uid).Map, Is.EqualTo(_upper));
            if (secondaryMap)
            {
                var lower = SComp<TacticalMapComponent>(_lower);
                Assert.That(lower.GovforBlips, Is.Empty);
                Assert.That(lower.OpforBlips, Is.Empty);
                Assert.That(lower.MarineBlips, Is.Empty);
            }
            else
            {
                var tactical = SEntMan.System<TacticalMapSystem>();
                var computer = SComp<TacticalMapComputerComponent>(_console);
                tactical.RefreshReconstructionContacts((_console, computer));
                Assert.That(computer.Blips.ContainsKey(friend.Id), Is.True);
                Assert.That(computer.Blips.ContainsKey(enemy.Id), Is.False, "Another floor must not bypass faction visibility.");
                Assert.That(computer.Blips[friend.Id].Image!.RsiState, Is.EqualTo("rmc_commander"));
            }
        });
        await Server.WaitPost(() =>
        {
            foreach (var uid in new[] { friend, enemy, vehicle })
                SEntMan.System<SharedTransformSystem>().SetCoordinates(uid, new EntityCoordinates(_upper, new Vector2(7.5f)));
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var root = SComp<TacticalMapComponent>(_upper);
            Assert.That(root.GovforBlips[friend.Id].Indices, Is.EqualTo(new Vector2i(7, 7)));
            Assert.That(root.OpforBlips[enemy.Id].Indices, Is.EqualTo(new Vector2i(7, 7)));
            Assert.That(root.MarineBlips[vehicle.Id].Indices, Is.EqualTo(new Vector2i(7, 7)));
        });
    }
#pragma warning restore RA0002
}
