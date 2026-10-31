using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Hijack;
using Content.Server.Materials;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._RMC14.Hijack;
using Content.Shared._RMC14.Sentry.Laptop;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Serilog.Events;

namespace Content.IntegrationTests.CMU14.Upstream;

public sealed class CMUUpstreamLifecycleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task InvalidDestinationCannotStartFtlButValidDestinationCan()
    {
        var ship = await Pair.CreateTestMap();
        var destination = await Pair.CreateTestMap();
        bool ExpectedDestinationError(string sawmill, LogEvent message) =>
            message.Level == LogEventLevel.Error &&
            message.RenderMessage().StartsWith("Found invalid FTL destination", StringComparison.Ordinal);
        Pair.ServerLogHandler.JudgeLog += ExpectedDestinationError;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var uid = ship.Grid.Owner;
                var shuttle = SEntMan.EnsureComponent<ShuttleComponent>(uid);
                var system = Server.System<ShuttleSystem>();
                var detached = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var invalid = new (EntityCoordinates Coordinates, Angle Angle)[]
                {
                    (new EntityCoordinates(detached, Vector2.Zero), Angle.Zero),
                    (default, Angle.Zero),
                    (destination.GridCoords, new Angle(double.NaN)),
                };
                foreach (var (coordinates, angle) in invalid)
                {
                    Assert.That(system.FTLFree(uid, coordinates, angle, null), Is.False);
                    system.FTLToCoordinates(uid, shuttle, coordinates, angle);
                    Assert.That(SEntMan.HasComponent<FTLComponent>(uid), Is.False,
                        "Invalid destinations must be rejected before startup audio, undocking, or flight state begins.");
                }

                system.FTLToCoordinates(uid, shuttle, destination.GridCoords, Angle.Zero);
                var flight = SEntMan.GetComponent<FTLComponent>(uid);
                Assert.That(flight.State, Is.EqualTo(FTLState.Starting));
                Assert.That(flight.TargetCoordinates, Is.EqualTo(destination.GridCoords));
                SEntMan.RemoveComponent<FTLComponent>(uid);
                SEntMan.DeleteEntity(detached);
            });
        }
        finally
        {
            Pair.ServerLogHandler.JudgeLog -= ExpectedDestinationError;
        }
    }

    [Test]
    public async Task HandheldListenOnlyStopsTransmissionImmediatelyAndCyclesOff()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var radio = SEntMan.SpawnEntity("RMCRadioHandheldColony", map.GridCoords);
            var microphone = SEntMan.GetComponent<RadioMicrophoneComponent>(radio);
            var speaker = SEntMan.GetComponent<RadioSpeakerComponent>(radio);
            void Activate() => SEntMan.EventBus.RaiseLocalEvent(radio, new ActivateInWorldEvent(user, radio, true));

            Activate();
            var attempt = new ListenAttemptEvent(user);
            SEntMan.EventBus.RaiseLocalEvent(radio, attempt);
            Assert.Multiple(() =>
            {
                Assert.That(microphone.Enabled, Is.False);
                Assert.That(speaker.Enabled, Is.True);
                Assert.That(attempt.Cancelled, Is.True, "No speech may be relayed while deferred listener removal is pending.");
            });
            Activate();
            Assert.That((microphone.Enabled, speaker.Enabled), Is.EqualTo((false, false)));
            Activate();
            Assert.That((microphone.Enabled, speaker.Enabled), Is.EqualTo((true, true)));
        });
    }

    [Test]
    public async Task UnloadingMixedMagazineConservesRounds()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var box = SEntMan.SpawnEntity("CMUBoxBulletsPistol9MM", map.GridCoords);
            var boxComp = SEntMan.GetComponent<BulletBoxComponent>(box);
            typeof(BulletBoxComponent).GetField(nameof(boxComp.Amount))!.SetValue(boxComp, 0);
            var magazine = SEntMan.SpawnEntity("RMCMagazinePistolL14", map.GridCoords);
            var ammo = SEntMan.GetComponent<BallisticAmmoProviderComponent>(magazine);
            var gun = Server.System<SharedGunSystem>();
            gun.SetBallisticUnspawned((magazine, ammo), 1);
            var first = SEntMan.SpawnEntity(ammo.Proto, map.GridCoords);
            var second = SEntMan.SpawnEntity(ammo.Proto, map.GridCoords);
            Assert.That(gun.TryBallisticInsert((magazine, ammo), first, null, true), Is.True);
            Assert.That(gun.TryBallisticInsert((magazine, ammo), second, null, true), Is.True);

            var transfer = new BulletBoxTransferDoAfterEvent(true);
            transfer.DoAfter = new DoAfter(0,
                new DoAfterArgs(SEntMan, user, TimeSpan.Zero, transfer, box, box, magazine), SGameTiming.CurTime);
            SEntMan.EventBus.RaiseLocalEvent(box, transfer);

            Assert.Multiple(() =>
            {
                Assert.That(boxComp.Amount, Is.EqualTo(3));
                Assert.That(ammo.Count, Is.Zero);
                Assert.That(ammo.UnspawnedCount, Is.Zero);
                Assert.That(SEntMan.Deleted(first) || SEntMan.IsQueuedForDeletion(first), Is.True);
                Assert.That(SEntMan.Deleted(second) || SEntMan.IsQueuedForDeletion(second), Is.True);
            });
        });
    }

    [Test]
    public async Task ReclaimingPendingDeletionYieldsMaterialOnce()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var recycler = SEntMan.SpawnEntity("CMURecycler", map.GridCoords);
            var component = SEntMan.GetComponent<MaterialReclaimerComponent>(recycler);
            typeof(MaterialReclaimerComponent).GetField(nameof(component.Efficiency))!.SetValue(component, 1f);
            var item = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<PhysicalCompositionComponent>(item).MaterialComposition.Add("Steel", 1);
            var system = Server.System<MaterialReclaimerSystem>();
            system.Reclaim(recycler, item);
            Assert.That(SEntMan.IsQueuedForDeletion(item), Is.True);
            system.Reclaim(recycler, item);
            Assert.That(system.TryStartProcessItem(recycler, item), Is.False);
            Assert.That(SEntMan.GetComponent<MaterialStorageComponent>(recycler).Storage["CMSteel"], Is.EqualTo(1),
                "The queued item must yield once, with CMU's material conversion preserved.");
        });
    }

    [Test]
    public async Task LaptopCloseClearsOnlyItsOwnWatcher()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var laptop = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<SentryLaptopComponent>(laptop);
            var other = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<SentryLaptopComponent>(other);
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var watcher = SEntMan.AddComponent<SentryLaptopWatcherComponent>(user);
            typeof(SentryLaptopWatcherComponent).GetField(nameof(watcher.Laptop))!.SetValue(watcher, laptop);
            typeof(SentryLaptopWatcherComponent).GetField(nameof(watcher.CurrentSentry))!.SetValue(watcher, SEntMan.GetNetEntity(other));

            SEntMan.EventBus.RaiseLocalEvent(other, new BoundUIClosedEvent(SentryLaptopUiKey.Key, other, user));
            SEntMan.EventBus.RaiseLocalEvent(other, new SentryLaptopCloseCameraBuiMsg { Actor = user });
            Assert.That(watcher.Laptop, Is.EqualTo(laptop), "Closing another laptop must not release this camera.");
            SEntMan.EventBus.RaiseLocalEvent(laptop, new BoundUIClosedEvent(SentryLaptopUiKey.Key, laptop, user));
            Assert.Multiple(() =>
            {
                Assert.That(watcher.Laptop, Is.Null);
                Assert.That(watcher.CurrentSentry, Is.Null);
            });
        });
    }

    [Test]
    public async Task HijackQueueDrainsWithoutDroppingPendingWarnings()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var active = SEntMan.EnsureComponent<RMCHijackActiveMapComponent>(map.MapUid);
            var warnings = new List<EntityUid>();
            for (var i = 0; i < 3; i++)
            {
                // Unanchored pipes cancel their burst, but must still consume their queue entry and warning.
                var pipe = SEntMan.SpawnEntity(null, map.GridCoords);
                var pipeComp = SEntMan.AddComponent<RMCHijackActivePipeComponent>(pipe);
                pipeComp.Map = map.MapUid;
                pipeComp.Warning = SEntMan.SpawnEntity("RMCHijackPipeExplosionWarning", map.GridCoords);
                warnings.Add(pipeComp.Warning.Value);
                active.Explode.Add(pipe);
            }
            active.ExplodeAt = TimeSpan.Zero;
            active.Next = TimeSpan.Zero;
            var system = Server.System<RMCHijackRandomDamageSystem>();
            system.Update(0);
            Assert.That(active.Explode, Has.Count.EqualTo(2), "The remaining wave must survive the update.");
            Assert.That(warnings.FindAll(SEntMan.IsQueuedForDeletion), Has.Count.EqualTo(1),
                "Only the processed pipe's warning may disappear; queue order is not part of the contract.");
            system.Update(0);
            system.Update(0);
            Assert.That(active.Explode, Is.Empty);
            Assert.That(active.ExplodeAt, Is.Null);
            Assert.That(warnings.TrueForAll(SEntMan.IsQueuedForDeletion), Is.True);
        });
    }
}
