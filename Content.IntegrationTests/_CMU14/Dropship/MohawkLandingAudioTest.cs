using System.Linq;
using Content.Server.Shuttles.Systems;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkLandingAudioTest
{
    private const string Landing = "/Audio/CMU14/Dropships/Mohawk/landing.ogg";
    private const string OriginalLanding = "/Audio/_RMC14/Machines/Shuttle/engine_landing.ogg";
    private const string Takeoff = "/Audio/CMU14/Dropships/Mohawk/takeoff.ogg";
    private const string Flight = "/Audio/CMU14/Dropships/Mohawk/flight.ogg";
    private const string OriginalTakeoff = "/Audio/_RMC14/Machines/Shuttle/engine_startup.ogg";
    private const string OriginalFlight = "/Audio/Effects/Shuttle/hyperspace_progress.ogg";

    [TestCase("omaha", true, 0.5f)]
    [TestCase("midway", true, 0.5f)]
    [TestCase("midway", true, 10f)]
    [TestCase("midway", false, 0.5f)]
    public async Task FlightCuesFollowPhasesForPassengersAndLandingZone(string variant, bool customCue, float startupTime)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        EntityUid destination = default;
        EntityUid onboardStream = default;
        EntityUid exteriorStream = default;
        EntityUid travelStream = default;
        var expected = customCue ? Landing : OriginalLanding;
        var expectedTakeoff = customCue ? Takeoff : OriginalTakeoff;
        var expectedFlight = customCue ? Flight : OriginalFlight;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            var dropship = entities.GetComponent<DropshipComponent>(ship);
            Assert.That(((SoundPathSpecifier) dropship.ArrivalSound).Path.ToString(), Is.EqualTo(Landing));
            Assert.That(((SoundPathSpecifier) dropship.StartupSound!).Path.ToString(), Is.EqualTo(Takeoff));
            Assert.That(((SoundPathSpecifier) dropship.TravelSound!).Path.ToString(), Is.EqualTo(Flight));
            if (!customCue)
            {
#pragma warning disable RA0002 // Check the unchanged stock dropship cue through the same real flight.
                dropship.ArrivalSound = new DropshipComponent().ArrivalSound;
                dropship.StartupSound = null;
                dropship.TravelSound = null;
#pragma warning restore RA0002
            }
            var ground = maps.CreateMap();
            destination = entities.SpawnEntity(null, new EntityCoordinates(ground, 20, 20));
            entities.AddComponent<DropshipDestinationComponent>(destination);
            var nav = entities.EntityQuery<DropshipNavigationComputerComponent>()
                .Single(c => entities.GetComponent<TransformComponent>(c.Owner).GridUid == ship);
            // Other flight fixtures shorten this mutable system setting on pooled pairs.
            entities.System<ShuttleSystem>().DefaultArrivalTime = 10f;
            Assert.That(entities.System<SharedDropshipSystem>().FlyTo((nav.Owner, nav), destination, null,
                startupTime: startupTime, hyperspaceTime: 12f), Is.True);
            var startup = entities.GetComponent<AudioComponent>(entities.GetComponent<FTLComponent>(ship).StartupStream!.Value);
            Assert.That(startup.FileName, Is.EqualTo(expectedTakeoff));
            Assert.That(startup.Params.Volume, Is.EqualTo(6));
            Assert.That(startup.Flags.HasFlag(AudioFlags.GridAudio), Is.True);
            Assert.That(startup.Params.Loop, Is.EqualTo(customCue && startupTime > 6.36f));
        });
        if (startupTime > 6.36f)
        {
            await pair.RunSeconds(7);
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                var flight = entities.GetComponent<FTLComponent>(ship);
                Assert.That(flight.State, Is.EqualTo(FTLState.Starting));
                Assert.That(entities.EntityExists(flight.StartupStream!.Value), Is.True,
                    "The takeoff cue must continue after its first playback until the countdown finishes.");
            });
            await pair.RunSeconds(startupTime - 7);
        }
        await pair.RunSeconds(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<FTLComponent>(ship).State, Is.EqualTo(FTLState.Travelling));
            travelStream = entities.GetComponent<FTLComponent>(ship).TravelStream!.Value;
            var travel = entities.GetComponent<AudioComponent>(travelStream);
            Assert.That(travel.FileName, Is.EqualTo(expectedFlight));
            Assert.That(travel.Params.Loop, Is.True);
            Assert.That(travel.Params.Volume, Is.EqualTo(-3));
            Assert.That(travel.Flags.HasFlag(AudioFlags.GridAudio), Is.True);
            var tail = entities.EntityQuery<AudioComponent>().Single(a => a.FileName == expectedTakeoff &&
                entities.GetComponent<TransformComponent>(a.Owner).ParentUid != ship);
            Assert.That(tail.Flags.HasFlag(AudioFlags.NoOcclusion), Is.True,
                "The departure point must keep the chosen takeoff tail, not switch to the stock cue.");
            Assert.That(tail.Params.Loop, Is.False, "The departure point must not loop forever.");
            if (startupTime > 6.36f)
                Assert.That(entities.GetComponent<FTLComponent>(ship).StartupStream, Is.Null,
                    "The onboard takeoff loop stops when the flight loop starts.");
            if (customCue)
                Assert.That(entities.EntityQuery<AudioComponent>().Any(a =>
                    a.FileName == OriginalTakeoff || a.FileName == OriginalFlight), Is.False);
            Assert.That(entities.EntityQuery<AudioComponent>().Any(a => a.FileName == expected), Is.False,
                "Landing audio must wait for the ten-second arrival phase.");
        });
        await pair.RunSeconds(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var flight = entities.GetComponent<FTLComponent>(ship);
            Assert.That(flight.State, Is.EqualTo(FTLState.Arriving));
            Assert.That(flight.StateTime.Length.TotalSeconds, Is.EqualTo(10));
            var onboard = entities.EntityQuery<AudioComponent>().Single(a => a.FileName == expected &&
                entities.GetComponent<TransformComponent>(a.Owner).ParentUid == ship);
            onboardStream = onboard.Owner;
            exteriorStream = entities.GetComponent<DropshipDestinationComponent>(destination).ArrivalSoundEntity!.Value;
            var exterior = entities.GetComponent<AudioComponent>(exteriorStream);
            Assert.That(exterior.FileName, Is.EqualTo(expected));
            Assert.That(onboard.AudioStart, Is.EqualTo(flight.StateTime.Start));
            Assert.That(exterior.AudioStart, Is.EqualTo(onboard.AudioStart));
            Assert.That(onboard.Flags.HasFlag(AudioFlags.GridAudio), Is.True);
            Assert.That(onboard.Params.Volume, Is.EqualTo(5));
            Assert.That(exterior.Params.Volume, Is.EqualTo(0));
            if (customCue)
                Assert.That(entities.EntityQuery<AudioComponent>().Any(a => a.FileName == OriginalLanding), Is.False,
                    "The stock arrival cue must not play over the Mohawk mix.");
        });
        await pair.RunSeconds(11);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.EntityExists(onboardStream), Is.EqualTo(!customCue),
                "The ten-second Mohawk cue ends at touchdown; the longer stock cue keeps its original tail.");
            Assert.That(entities.EntityExists(exteriorStream), Is.False);
            Assert.That(entities.EntityExists(travelStream), Is.False,
                "The flight loop must stop at touchdown.");
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }
}
