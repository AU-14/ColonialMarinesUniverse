using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server._RMC14.Language.Systems;
using Content.Shared.Chat;
using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.CMU14.Language;

[TestFixture]
public sealed class CMUThreatSpeechDeliveryTest : GameTest
{
    [TestCase("CMU14XenoNeomorph", "CMMobHuman", "Pathogen")]
    [TestCase("CMXenoDrone", "CMMobHuman", "Xeno")]
    [TestCase("CMXenoDrone", "CMMobHuman", "English")]
    [TestCase("CMUMobYautja", "CMUMobYautjaHellhound", "Yautja")]
    public async Task KnownLanguageSpeechReachesListener(string speakerPrototype, string listenerPrototype, string language)
    {
        var map = await Pair.CreateTestMap();
        var original = ServerSession!.AttachedEntity;
        EntityUid source = default;
        var messages = new List<ChatMessage>();
        var controller = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();
        Action<ChatMessage> capture = message => messages.Add(message);
        await Client.WaitPost(() => controller.MessageAdded += capture);
        try
        {
            await Server.WaitPost(() =>
            {
                if (language == "Pathogen")
                    SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
                source = SEntMan.SpawnEntity(speakerPrototype, map.GridCoords);
                var listener = SEntMan.SpawnEntity(listenerPrototype, map.GridCoords);
                if (language == "English")
                {
                    var hive = SEntMan.SpawnEntity("CMUCorruptedHive", map.GridCoords);
                    Server.System<SharedXenoHiveSystem>().SetHive(source, hive);
                }
                var languages = Server.System<LanguageSystem>();
                languages.AddLanguage(listener, language);
                languages.SetLanguage(source, language);
                Server.PlayerMan.SetAttachedEntity(ServerSession, listener);
            });
            await Pair.RunTicksSync(3);
            await Server.WaitPost(() => Server.System<ChatSystem>().TrySendInGameICMessage(
                source, "speech delivery probe", InGameICChatType.Speak, hideChat: false, ignoreActionBlocker: true));
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() => Assert.That(
                messages.Any(message => message.Channel == ChatChannel.Local && message.Message.Contains("speech delivery probe", StringComparison.OrdinalIgnoreCase)),
                Is.True, "a listener who understands the language must receive the actual local speech"));
        }
        finally
        {
            await Client.WaitPost(() => controller.MessageAdded -= capture);
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, original));
        }
    }
}
