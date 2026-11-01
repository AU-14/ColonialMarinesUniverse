using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Audio;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;

namespace Content.IntegrationTests.CMU14.Audio;

public sealed class CMUClientTransientAudioTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task RejectedAndInaudibleRequestsAllocateNothingAndDeletionRestoresAdmission()
    {
        await Client.WaitAssertion(() =>
        {
            const string path = "/Audio/Effects/beep1.ogg";
            var maps = CEntMan.System<SharedMapSystem>();
            var map = maps.CreateMap(runMapInit: true);
            var otherMap = maps.CreateMap(runMapInit: true);
            var source = CEntMan.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            var eye = new Eye();
            var transform = CEntMan.System<SharedTransformSystem>();
            Client.ResolveDependency<IEyeManager>().CurrentEye = eye;
            Client.ResolveDependency<IConfigurationManager>().SetCVar(Robust.Shared.CVars.AudioDefaultConcurrent, 1);
            var budget = CEntMan.System<CMUSharedAudioBudgetSystem>();
            var sound = new SoundPathSpecifier(path, AudioParams.Default.WithMaxDistance(10));

            foreach (var coordinates in new[] { new EntityCoordinates(otherMap, Vector2.Zero), new EntityCoordinates(map, new Vector2(100, 0)) })
            {
                eye.Position = transform.ToMapCoordinates(coordinates);
                Assert.That(budget.PlayPredicted(sound, source, source), Is.Null);
            }
            Assert.That(CEntMan.EntityQuery<AudioComponent>().Count(c => c.FileName == path), Is.Zero);

            eye.Position = transform.ToMapCoordinates(new EntityCoordinates(map, Vector2.Zero));
            var first = budget.PlayPredicted(sound, source, source);
            Assert.That(first, Is.Not.Null);
            for (var i = 0; i < 3; i++)
                Assert.That(budget.PlayPredicted(sound, source, source), Is.Null);
            Assert.That(CEntMan.EntityQuery<AudioComponent>().Count(c => c.FileName == path), Is.EqualTo(1));
            CEntMan.DeleteEntity(first!.Value.Entity);
            Assert.That(budget.PlayPredicted(sound, source, source), Is.Not.Null);
        });
    }
}
