using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.IntegrationTests.CMU14.Audio;

[TestFixture]
public sealed class CMUStopGlobalSoundTest : GameTest
{
    private const string AdminSound = "/Audio/StationEvents/sound_station_14.ogg";
    private const string OtherSound = "/Audio/StationEvents/running_out.ogg";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task StopsAllAdminTracksAndAllowsFuturePlayback(bool targeted)
    {
        await Client.WaitPost(() =>
        {
            Client.CfgMan.SetCVar(CCVars.AdminSoundsEnabled, true);
            CEntMan.System<SharedAudioSystem>().PlayGlobal(
                new SoundPathSpecifier(OtherSound), Filter.Local(), false, AudioParams.Default.WithLoop(true));
        });

        await Server.ExecuteCommand($"playglobalsound {AdminSound}");
        await Server.ExecuteCommand($"playglobalsound {AdminSound}");
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CountAudio(AdminSound), Is.EqualTo(2));
            Assert.That(CountAudio(OtherSound), Is.EqualTo(1));
        });

        var command = targeted ? $"stopglobalsound \"{ServerSession!.Name}\"" : "stopglobalsound";
        await Server.ExecuteCommand(command);
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CountAudio(AdminSound), Is.Zero);
            Assert.That(CountAudio(OtherSound), Is.EqualTo(1), "Unrelated audio must keep playing.");
            Assert.That(Client.CfgMan.GetCVar(CCVars.AdminSoundsEnabled), Is.True);
        });

        // Repeated stops are harmless, and stopping does not mute future admin sounds.
        await Server.ExecuteCommand(command);
        await Server.ExecuteCommand($"playglobalsound {AdminSound}");
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() => Assert.That(CountAudio(AdminSound), Is.EqualTo(1)));

        await Server.WaitAssertion(() =>
        {
            var registered = Server.ResolveDependency<IConsoleHost>().AvailableCommands["stopglobalsound"];
            Assert.That(registered.Description, Does.Contain("playglobalsound"));
            Assert.That(registered.Help, Does.StartWith("stopglobalsound"));
        });
    }

    private int CountAudio(string filename)
    {
        return CEntMan.EntityQuery<AudioComponent>().Count(audio => audio.FileName == filename);
    }
}
