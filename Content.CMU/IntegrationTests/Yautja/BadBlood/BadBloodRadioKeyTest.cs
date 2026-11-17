using Content.IntegrationTests.Fixtures;
using Content.Shared.Radio;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// COUPLING-01: the Bad Blood channel was "#b", but every key examine printed ":" + key, so the communicator told
// Bad Bloods to use ":b", which is GOVFOR Bravo. The channel is "#r" now and the examine prints each channel's prefix.
[TestFixture]
public sealed class BadBloodRadioKeyTest : GameTest
{
    private const string BadBloodChannel = "CMUYautjaBadBlood";
    private const string BravoChannel = "radioGovforBravo";
    private const string BadBloodKey = "CMUYautjaBadBloodEncryptionKey";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task BadBloodKeyIsItsOwnAndShowsItsPrefix()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var badBlood = protos.Index<RadioChannelPrototype>(BadBloodChannel);
            var bravo = protos.Index<RadioChannelPrototype>(BravoChannel);

            Assert.Multiple(() =>
            {
                Assert.That($"{badBlood.RadioPrefix}{badBlood.KeyCode}", Is.EqualTo("#r"));
                Assert.That(char.ToLowerInvariant(badBlood.KeyCode), Is.Not.EqualTo(char.ToLowerInvariant(bravo.KeyCode)),
                    "Bad Bloods must not be told to use Bravo's letter");
            });

            foreach (var channel in protos.EnumeratePrototypes<RadioChannelPrototype>())
            {
                if (channel.ID == BadBloodChannel)
                    continue;

                Assert.That($"{channel.RadioPrefix}{char.ToLowerInvariant(channel.KeyCode)}", Is.Not.EqualTo("#r"),
                    $"{channel.ID} shares the Bad Blood key");
            }

            // typing the key reaches the Bad Blood channel, and Bravo's key does not
            var chat = Server.System<Content.Server.Chat.Systems.ChatSystem>();
            var speaker = SEntMan.SpawnEntity(null, map.GridCoords);
            Assert.That(chat.TryProcessRadioMessage(speaker, "#r hunt", out _, out var heard, quiet: true), Is.True);
            Assert.That(heard?.ID, Is.EqualTo(BadBloodChannel));
            chat.TryProcessRadioMessage(speaker, ":b hunt", out _, out var bravoHeard, quiet: true);
            Assert.That(bravoHeard?.ID, Is.Not.EqualTo(BadBloodChannel));

            // the key's examine shows "#r", the key that works, not ":r"
            var key = SEntMan.SpawnEntity(BadBloodKey, map.GridCoords);
            var examiner = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var text = Server.System<Content.Server.Examine.ExamineSystem>().GetExamineText(key, examiner).ToString();
            Assert.That(text, Does.Contain("#r"), text);
            Assert.That(text, Does.Not.Contain(":r"), text);

            SEntMan.DeleteEntity(key);
            SEntMan.DeleteEntity(examiner);
            SEntMan.DeleteEntity(speaker);
        });
    }
}
