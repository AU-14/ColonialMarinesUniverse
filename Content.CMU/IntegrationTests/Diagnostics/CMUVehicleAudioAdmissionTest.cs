using Content.Client.CMU14.Vehicle;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Vehicle.Components;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Graphics;

namespace Content.IntegrationTests.CMU14.Diagnostics;

public sealed class CMUVehicleAudioAdmissionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task EngineLoopsAllocateOnlyWithinHearingRangeAndResumeOnReturn()
    {
        const string sound = "/Audio/CMU14/Fighter/jet-exterior.ogg";
        EntityUid map = default;
        EntityUid otherMap = default;
        EntityUid vehicle = default;
        var eye = new Eye();
        await Client.WaitAssertion(() =>
        {
            var maps = CEntMan.System<SharedMapSystem>();
            map = maps.CreateMap(runMapInit: true);
            otherMap = maps.CreateMap(runMapInit: true);
            vehicle = CEntMan.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            CEntMan.AddComponent<GridVehicleMoverComponent>(vehicle);
            var engine = CEntMan.AddComponent<CMUVehicleEngineSoundComponent>(vehicle);
            engine.IdleSound = new SoundPathSpecifier(sound, AudioParams.Default.WithMaxDistance(10));
            engine.DriveSound = engine.IdleSound;
            engine.EngineRunning = true;
            Client.ResolveDependency<IEyeManager>().CurrentEye = eye;
        });

        // Other floors and distant sources remain in PVS, but must not consume local audio sources.
        foreach (var (listenerMap, position, expected) in new[]
                 {
                     (otherMap, Vector2.Zero, 0),
                     (map, new Vector2(100, 0), 0),
                     (map, Vector2.Zero, 2),
                     (map, new Vector2(100, 0), 0),
                     (map, Vector2.Zero, 2),
                     (otherMap, Vector2.Zero, 0),
                 })
        {
            await Client.WaitPost(() =>
            {
                eye.Position = CEntMan.System<SharedTransformSystem>().ToMapCoordinates(new EntityCoordinates(listenerMap, position));
                CEntMan.System<CMUVehicleEngineLoopSystem>().FrameUpdate(0);
            });
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() => Assert.That(
                CEntMan.EntityQuery<AudioComponent>().Count(audio => audio.FileName == sound), Is.EqualTo(expected)));
        }
    }
}
