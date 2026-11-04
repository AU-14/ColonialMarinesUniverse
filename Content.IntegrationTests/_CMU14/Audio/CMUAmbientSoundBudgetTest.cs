using Content.Client.Audio;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Audio;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.Audio;

[TestFixture]
public sealed class CMUAmbientSoundBudgetTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestAmbientBudgetSource
          components:
          - type: AmbientSound
            range: 8
            sound:
              path: /Audio/Effects/beep1.ogg

        - type: entity
          parent: CMUTestAmbientBudgetSource
          id: CMUTestAmbientBudgetOtherSound
          components:
          - type: AmbientSound
            sound:
              path: /Audio/Effects/beep_landmine.ogg
        """;

    [Test]
    public async Task OneBatchRespectsPerSoundLimitAndLeavesRoomForOtherSounds()
    {
        EntityUid map = default;
        NetEntity listener = default;
        EntityUid? originalAttached = null;
        var sources = new List<Entity<AmbientSoundComponent>>();
        var configuration = Client.ResolveDependency<IConfigurationManager>();
        var originalCount = configuration.GetCVar(CCVars.MaxAmbientSources);
        var originalCooldown = configuration.GetCVar(CCVars.AmbientCooldown);

        try
        {
            await Client.WaitPost(() =>
            {
                configuration.SetCVar(CCVars.MaxAmbientSources, 16);
                configuration.SetCVar(CCVars.AmbientCooldown, 0f);
            });
            await Server.WaitPost(() =>
            {
                map = Server.System<SharedMapSystem>().CreateMap(runMapInit: true);
                var player = SEntMan.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
                SEntMan.EnsureComponent<EyeComponent>(player);
                originalAttached = ServerSession!.AttachedEntity;
                Server.PlayerMan.SetAttachedEntity(ServerSession, player);
                listener = SEntMan.GetNetEntity(player);
            });
            await Pair.RunTicksSync(10);
            await Client.WaitPost(() =>
            {
                var coordinates = new EntityCoordinates(CEntMan.GetEntity(listener), Vector2.One);
                for (var i = 0; i < 16; i++)
                {
                    var uid = CEntMan.SpawnEntity("CMUTestAmbientBudgetSource", coordinates);
                    sources.Add((uid, CComp<AmbientSoundComponent>(uid)));
                }

                configuration.SetCVar(CCVars.AmbientCooldown, 0.1f);
            });
            await Pair.RunTicksSync(15);
            await Client.WaitAssertion(() =>
            {
                var ambience = Client.System<AmbientSoundSystem>();
                Assert.That(sources.Count(ambience.IsActive), Is.EqualTo(6),
                    "A single update must enforce the per-sound budget while admitting nearby emitters.");

                var coordinates = new EntityCoordinates(CEntMan.GetEntity(listener), Vector2.One);
                var other = CEntMan.SpawnEntity("CMUTestAmbientBudgetOtherSound", coordinates);
                sources.Add((other, CComp<AmbientSoundComponent>(other)));
            });
            await Pair.RunTicksSync(15);
            await Client.WaitAssertion(() =>
            {
                var ambience = Client.System<AmbientSoundSystem>();
                Assert.That(sources.Take(16).Count(ambience.IsActive), Is.EqualTo(6),
                    "Later updates must keep the same sound within its budget.");
                Assert.That(ambience.IsActive(sources[^1]), Is.True,
                    "Capping duplicate loops must leave capacity for a different sound.");
            });
        }
        finally
        {
            await Client.WaitPost(() =>
            {
                foreach (var source in sources)
                    CEntMan.DeleteEntity(source);
                configuration.SetCVar(CCVars.MaxAmbientSources, originalCount);
                configuration.SetCVar(CCVars.AmbientCooldown, originalCooldown);
            });
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, originalAttached));
            if (map.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(map);
        }
    }
}
