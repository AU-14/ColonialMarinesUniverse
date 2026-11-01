using System.Linq;
using Content.Server.CMU14.Yautja;
using Content.Server.Mind;
using Content.Shared.ActionBlocker;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Speech;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

/// <summary>
/// The natural (ColonyFall) Wendigo: speechless but still emotes, trophied by Yautja as a xeno like every Wendigo, and its player's
/// mind holds the Wendigo job even after the threat spawn sets its own.
/// </summary>
[TestFixture]
public sealed class CMUWendigoNaturalTest
{
    private const string Natural = "AU14Wendigo";
    private const string Lab = "CMUWendigoLab";
    private const string WendigoJob = "CMXenoWendigo";
    private const string ThreatLeaderJob = "AU14JobThreatLeader";

    [Test]
    public async Task NaturalWendigoIsSpeechlessXenoTrophyWithWendigoJob()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        EntityUid natural = default, lab = default, mindId = default;
        await server.WaitAssertion(() =>
        {
            var blocker = entMan.System<ActionBlockerSystem>();
            var trophy = entMan.System<YautjaTrophySystem>();
            var minds = entMan.System<MindSystem>();
            var roles = entMan.System<SharedRoleSystem>();

            natural = entMan.SpawnEntity(Natural, map.GridCoords);
            lab = entMan.SpawnEntity(Lab, map.GridCoords);

            Assert.That(blocker.CanSpeak(natural), Is.False, "The natural Wendigo must not speak at all.");
            Assert.That(blocker.CanEmote(natural), Is.True, "The natural Wendigo keeps its emotes.");
            Assert.That(entMan.GetComponent<SpeechComponent>(natural).AllowedEmotes.Select(e => e.Id), Does.Contain("XenoRoar"),
                "Disabling speech must not drop the inherited emote list.");
            Assert.That(blocker.CanSpeak(lab), Is.True, "Lab-made Wendigos still speak.");

            Assert.That(trophy.IsXenoTrophyTarget(natural), Is.True, "Yautja trophy the natural Wendigo as a xeno.");
            Assert.That(trophy.IsXenoTrophyTarget(lab), Is.True, "Yautja trophy lab-made Wendigos as xenos too.");

            // The threat spawn transfers the mind, then sets its own job in the same tick.
            mindId = minds.CreateMind(null).Owner;
            minds.TransferTo(mindId, natural);
            roles.MindAddJobRole(mindId, silent: true, jobPrototype: ThreatLeaderJob);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var roles = entMan.System<SharedRoleSystem>();
            Assert.That(roles.MindHasRole<JobRoleComponent>(mindId, out var job), Is.True);
            Assert.That(job!.Value.Comp1.JobPrototype?.Id, Is.EqualTo(WendigoJob),
                "Wendigo playtime must be tracked under the Wendigo job.");

            entMan.DeleteEntity(natural);
            entMan.DeleteEntity(lab);
            entMan.DeleteEntity(mindId);
        });

        await pair.CleanReturnAsync();
    }
}
