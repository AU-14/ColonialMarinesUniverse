using Content.Client._RMC14.TacticalMap;
using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Server._RMC14.Xenonids.Watch;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Utility;
using ClientEyeSystem = Robust.Client.GameObjects.EyeSystem;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Build deterministic authorized tactical feeds and hive fixtures.
    [TestCase(true)]
    [TestCase(false)]
    public async Task QueenXenoWatchRejectsUnauthorizedMapRequests(bool classic)
    {
        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var original = session.AttachedEntity;
            var queen = SEntMan.SpawnEntity("CMXenoQueen", new EntityCoordinates(_upper, new Vector2(0.5f)));
            var friend = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_upper, new Vector2(3.5f)));
            var enemy = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_upper, new Vector2(6.5f)));
            try
            {
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                var hive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(_upper, Vector2.Zero));
                var otherHive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(_upper, Vector2.Zero));
                var hives = SEntMan.System<SharedXenoHiveSystem>();
                hives.SetHive(queen, hive);
                hives.SetHive(friend, hive);
                hives.SetHive(enemy, otherHive);
                Server.PlayerMan.SetAttachedEntity(session, queen);
                Assert.That(_ui.TryOpenUi(queen, TacticalMapUserUi.Key, queen), Is.True);
                var user = SComp<TacticalMapUserComponent>(queen);
                user.Map = _upper;
                user.Xenos = true;
                user.XenoBlips.Clear();
                SEntMan.EventBus.RaiseLocalEvent(queen, (object) new CMUReconViewMessage(Vector2i.Zero)
                    { Actor = queen, UiKey = TacticalMapUserUi.Key });
                var generation = _recon.BuildSnapshot(queen, queen)!.Generation;
                void Request(EntityUid target, int? gen = null)
                {
                    BoundUserInterfaceMessage message = classic ? new TacticalMapQueenWatchMsg(target.Id) :
                        new CMUReconXenoWatchMessage(gen ?? generation, SEntMan.GetNetEntity(target));
                    message.Actor = queen;
                    message.UiKey = TacticalMapUserUi.Key;
                    SEntMan.EventBus.RaiseLocalEvent(queen, (object) message);
                    Assert.That(SComp<EyeComponent>(queen).Target, Is.Null, "Unauthorized requests must not start watching.");
                }
                var blip = new TacticalMapBlip { Indices = new(3, 3), Color = Color.Green };
                Request(friend); // Not in the authorized feed.
                user.XenoBlips[friend.Id] = blip;
                user.XenoBlips[enemy.Id] = blip;
                user.XenoBlips[_console.Id] = blip;
                user.XenoBlips[queen.Id] = blip;
                Request(enemy);
                Request(_console);
                Request(queen);
                if (!classic) Request(friend, generation - 1);
                user.XenoBlips[friend.Id] = blip with
                    { Image = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "enemy_blip") };
                Request(friend);
                user.XenoBlips[friend.Id] = blip;
                SEntMan.System<MobStateSystem>().ChangeMobState(friend, MobState.Dead);
                Request(friend);
                SEntMan.System<MobStateSystem>().ChangeMobState(friend, MobState.Alive);
                var watch = SEntMan.System<XenoWatchSystem>();
                Assert.That(watch.CanQueenWatch(friend, queen), Is.False, "An ordinary xeno cannot gain queen-map watch access.");
                Assert.That(watch.CanQueenWatch(queen, friend), Is.True);
                _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
                Request(friend);
            }
            finally
            {
                _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var xeno in new[] { queen, friend, enemy }) SEntMan.DeleteEntity(xeno);
            }
        });
    }

#pragma warning restore RA0002
}
