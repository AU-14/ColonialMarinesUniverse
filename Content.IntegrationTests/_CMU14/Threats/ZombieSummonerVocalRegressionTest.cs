#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Server._CMU14.Threats.Mobs.ZombieSummoner;
using Content.Shared._CMU14.Threats.Mobs.ZombieSummoner;
using Content.Shared._RMC14.Emote;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Speech.Components;
using Content.Shared.Zombies;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Threats;

[TestFixture]
[TestOf(typeof(ZombieSummonerSystem))]
public sealed class ZombieSummonerVocalRegressionTest : GameTest
{
    [Test]
    public async Task SpawnedCursedZombieKeepsHumanVocalBankAndManualScream()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid summoner = default;
        NetEntity summonerNet = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var actor = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(session, actor);
                summoner = SEntMan.SpawnEntity("CMUZombieSummoner", map.GridCoords);
                summonerNet = SEntMan.GetNetEntity(summoner);

                var ui = Server.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryOpenUi(summoner, ZombieSummonerUiKey.Key, actor), Is.True);
            });

            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var clientSummoner = CEntMan.GetEntity(summonerNet);
                var ui = CEntMan.GetComponent<UserInterfaceComponent>(clientSummoner);
                Assert.That(ui.ClientOpenInterfaces.ContainsKey(ZombieSummonerUiKey.Key), Is.True);
            });
            await Client.WaitPost(() =>
            {
                var clientSummoner = CEntMan.GetEntity(summonerNet);
                var ui = CEntMan.GetComponent<UserInterfaceComponent>(clientSummoner);
                ui.ClientOpenInterfaces[ZombieSummonerUiKey.Key]
                    .SendPredictedMessage(new ZombieSummonerSpawnMessage(1, ZombieSummonerSpawnType.Civilian));
            });

            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                var summonerComp = SEntMan.GetComponent<ZombieSummonerComponent>(summoner);
                Assert.That(summonerComp.Zombies, Has.Count.EqualTo(1));
                var zombie = summonerComp.Zombies.Single();
                var zombieComp = SEntMan.GetComponent<ZombieComponent>(zombie);
                var vocal = SEntMan.GetComponent<VocalComponent>(zombie);
                var profile = SEntMan.GetComponent<HumanoidProfileComponent>(zombie);
                var deathgasp = SEntMan.GetComponent<DeathgaspComponent>(zombie);
                SEntMan.TryGetComponent<EmoteOnDamageComponent>(zombie, out var damageEmotes);
                SEntMan.TryGetComponent<AutoEmoteComponent>(zombie, out var autoEmotes);

                Assert.Multiple(() =>
                {
                    Assert.That(zombieComp.EmoteSoundsId, Is.Null,
                        "only the zombie override must be cleared");
                    Assert.That(vocal.EmoteSounds, Is.EqualTo(profile.Voice),
                        "the inherited human Vocal bank must remain authoritative");
                    Assert.That(vocal.ScreamId.Id, Is.EqualTo("Scream"));
                    Assert.That(vocal.EmoteAction?.Id, Is.EqualTo("ActionScream"));
                    Assert.That(deathgasp.Prototype.Id, Is.EqualTo("Scream"));
                    Assert.That(damageEmotes?.Emotes.Select(id => id.Id) ?? Enumerable.Empty<string>(),
                        Does.Not.Contain("Scream"),
                        "automatic damage screams remain suppressed, including by removing the component");
                    Assert.That(autoEmotes?.Emotes.Select(id => id.Id) ?? Enumerable.Empty<string>(),
                        Does.Not.Contain("ZombieGroan"),
                        "the passive zombie groan remains suppressed, including by removing the component");
                });

                SEntMan.RemoveComponent<EmoteCooldownComponent>(zombie);
                SEntMan.EnsureComponent<EmoteCooldownComponent>(zombie);
                Assert.That(Server.System<ChatSystem>()
                    .TryEmoteWithoutChat(zombie, "Scream", ignoreActionBlocker: true), Is.True,
                    "the cursed zombie must retain its manual human scream");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }
    }
}

#pragma warning restore RA0002
