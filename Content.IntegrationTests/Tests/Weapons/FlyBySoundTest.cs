using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Weapons;

[TestFixture]
[TestOf(typeof(SharedFlyBySoundSystem))]
public sealed class FlyBySoundTest : GameTest
{
    private const string ListenerPrototype = "FlyBySoundTestListener";
    private const string ProjectilePrototype = "FlyBySoundTestProjectile";
    private const string SoundPath = "/Audio/Effects/beep1.ogg";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
    };

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {ListenerPrototype}
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      listener:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
        mask:
        - MobMask
        layer:
        - MobLayer

- type: entity
  id: {ProjectilePrototype}
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fly-by:
        shape:
          !type:PhysShapeCircle
          radius: 0.5
        layer:
        - Impassable
        - MidImpassable
        - HighImpassable
        - LowImpassable
        hard: false
  - type: FlyBySound
    prob: 1
    range: 0.5
    sound:
      path: {SoundPath}
";

    [Test]
    public async Task PredictedAudioAndAuthoritativeReconciliationKeepOneFixture()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.Sessions.Single();
        EntityUid listener = default;

        await server.WaitPost(() =>
        {
            listener = server.EntMan.SpawnEntity(ListenerPrototype, map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, listener);
        });
        await pair.RunTicksSync(5);

        var clientListener = pair.ToClientUid(listener);
        EntityUid predicted = default;
        await client.WaitPost(() =>
        {
            predicted = client.EntMan.SpawnEntity(ProjectilePrototype, client.Transform(clientListener).Coordinates);
            client.EntMan.EnsureComponent<PredictedProjectileClientComponent>(predicted);
            client.EntMan.System<SharedPhysicsSystem>().UpdateIsPredicted(predicted);

            AssertSingleFlyByFixture(client.EntMan, predicted);
        });
        await client.WaitRunTicks(2);

        await client.WaitAssertion(() =>
        {
            var sounds = client.EntMan.EntityQuery<AudioComponent>()
                .Where(audio => audio.FileName == SoundPath)
                .Where(audio => client.Transform(audio.Owner).ParentUid == clientListener)
                .ToArray();

            Assert.That(sounds, Has.Length.EqualTo(1), "one fly-by contact should produce one predicted sound");
        });

        EntityUid authoritative = default;
        await server.WaitPost(() =>
        {
            var coordinates = new EntityCoordinates(map.Grid, new Vector2(4, 0));
            authoritative = server.EntMan.SpawnEntity(ProjectilePrototype, coordinates);
            var prediction = server.EntMan.EnsureComponent<PredictedProjectileServerComponent>(authoritative);
            prediction.ClientId = predicted.Id;
            prediction.ClientEnt = listener;
            server.EntMan.Dirty(authoritative, prediction);
        });
        await pair.RunTicksSync(5);

        var clientAuthoritative = pair.ToClientUid(authoritative);
        await client.WaitAssertion(() =>
        {
            Assert.That(client.EntMan.HasComponent<PredictedProjectileServerComponent>(clientAuthoritative), Is.True);
            AssertSingleFlyByFixture(client.EntMan, predicted);
            AssertSingleFlyByFixture(client.EntMan, clientAuthoritative);
        });

        await server.WaitPost(() =>
        {
            server.EntMan.RemoveComponent<PredictedProjectileServerComponent>(authoritative);
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            Assert.That(client.EntMan.EntityExists(predicted), Is.False,
                "the authoritative projectile should retire its local predicted copy");
            AssertSingleFlyByFixture(client.EntMan, clientAuthoritative);
        });

        await server.WaitPost(() =>
        {
            server.EntMan.RemoveComponent<FlyBySoundComponent>(authoritative);
        });
        await pair.RunTicksSync(3);

        await client.WaitAssertion(() =>
        {
            var fixtures = client.EntMan.GetComponent<FixturesComponent>(clientAuthoritative);
            Assert.That(fixtures.Fixtures.ContainsKey(SharedFlyBySoundSystem.FlyByFixture), Is.False);
        });

        await server.WaitPost(() =>
        {
            var flyBy = server.EntMan.EnsureComponent<FlyBySoundComponent>(authoritative);
            flyBy.Range = 0.75f;
            server.EntMan.Dirty(authoritative, flyBy);
        });
        await pair.RunTicksSync(3);

        await client.WaitAssertion(() => AssertSingleFlyByFixture(client.EntMan, clientAuthoritative));

        for (var i = 0; i < 3; i++)
        {
            await server.WaitPost(() =>
            {
                var flyBy = server.EntMan.GetComponent<FlyBySoundComponent>(authoritative);
                flyBy.Range += 0.25f;
                server.EntMan.Dirty(authoritative, flyBy);
            });
            await pair.RunTicksSync(2);
            await client.WaitAssertion(() => AssertSingleFlyByFixture(client.EntMan, clientAuthoritative));
        }
    }

    private static void AssertSingleFlyByFixture(IEntityManager entMan, EntityUid uid)
    {
        var fixtures = entMan.GetComponent<FixturesComponent>(uid);
        Assert.Multiple(() =>
        {
            Assert.That(fixtures.Fixtures.ContainsKey(SharedFlyBySoundSystem.FlyByFixture), Is.True);
            Assert.That(fixtures.Fixtures.Keys.Count(id => id == SharedFlyBySoundSystem.FlyByFixture), Is.EqualTo(1));
        });
    }
}
