using Content.Server.Chat.Systems;
using Content.Server.Examine;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Chat;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Radio;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaRadioRegressionTest
{
    private static readonly string[] YautjaChannels =
    [
        "CMUYautja",
        "CMUYautjaOverseer",
        "CMUYautjaBadBlood",
        "CMUYautjaStranded",
        "CMUYautjaMilitary",
    ];

    [Test]
    public async Task CommunicatorUsesDedicatedEnglishRadioKeyAndCanTuneYautjaTower()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            var communicator = prototypes.Index<EntityPrototype>("CMUYautjaCommunicator");
            Assert.That(communicator.TryGetComponent<ItemIFFComponent>(out var iff, factory), Is.True);
            Assert.That(iff!.Factions, Does.Contain(new EntProtoId<IFFFactionComponent>("FactionYautja")));

            var faction = prototypes.Index<EntityPrototype>("FactionYautja");
            Assert.That(faction.TryGetComponent<FactionFrequenciesComponent>(out var frequencies, factory), Is.True);
            Assert.That(frequencies!.Channels, Is.EquivalentTo(YautjaChannels));

            foreach (var candidate in prototypes.EnumeratePrototypes<RadioChannelPrototype>())
            {
                if (candidate.ID == "CMUYautja")
                    continue;

                Assert.That(candidate.RadioPrefix == ':' && candidate.KeyCode == 'g', Is.False,
                    $"The dedicated :g key must not alias another radio channel ({candidate.ID}).");
            }

            foreach (var channelId in YautjaChannels)
            {
                var channel = prototypes.Index<RadioChannelPrototype>(channelId);
                Assert.Multiple(() =>
                {
                    Assert.That(channel.KeyCode, Is.Not.EqualTo(SharedChatSystem.DefaultChannelKey), channelId);
                    Assert.That(channel.KeyCode, Is.EqualTo(channelId switch
                    {
                        "CMUYautja" => 'g',
                        "CMUYautjaOverseer" => 'o',
                        "CMUYautjaBadBlood" => 'b',
                        "CMUYautjaStranded" => 's',
                        "CMUYautjaMilitary" => 'm',
                        _ => '\0'
                    }), channelId);
                    Assert.That(channel.RadioPrefix, Is.EqualTo(channelId == "CMUYautja" ? ':' : '#'), channelId);
                    Assert.That(channel.Tower, Is.True, channelId);
                    Assert.That(channel.Faction, Is.EqualTo("FactionYautja"), channelId);
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DedicatedKeycodeIsParsedAsRadioMessage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var hunter = server.EntMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var chat = server.EntMan.System<ChatSystem>();

            Assert.That(chat.TryProccessRadioMessage(hunter, ":g test message", out var output, out var channel), Is.True);
            Assert.That(output, Is.EqualTo("Test message"));
            Assert.That(channel!.ID, Is.EqualTo("CMUYautja"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MilitaryCommunicatorExamineListsYspecOnMilitaryPrefix()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid communicator = default;

        await server.WaitPost(() => communicator = server.EntMan.SpawnEntity(
            "CMUYautjaMilitaryCommunicator", map.GridCoords));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();

            try
            {
                var text = examine.GetExamineText(communicator, communicator).ToMarkup();
                Assert.That(text, Does.Contain("YSPEC"), "The military communicator should show its localized channel name.");
                Assert.That(text, Does.Contain(":m"), "The military communicator should show its military radio prefix.");
            }
            finally
            {
                if (!entMan.Deleted(communicator))
                    entMan.DeleteEntity(communicator);
            }
        });

        await pair.CleanReturnAsync();
    }
}
