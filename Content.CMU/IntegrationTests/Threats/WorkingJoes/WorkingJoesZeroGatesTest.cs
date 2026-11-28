using System.Linq;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.Players.JobWhitelist;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats.WorkingJoes;

/// <summary>
/// Anyone can play a hijacked Joe: with role whitelists enforced, a player with no whitelist and no playtime may take
/// a member seat or the leader's ghost role, which a colony Working Joe's synthetic whitelist would refuse. The
/// time is tracked under the hijacked Joe's own tracker, as evidence for the role.
/// </summary>
[TestFixture]
public sealed class WorkingJoesZeroGatesTest
{
    private const string Member = "CMUMobHijackedJoe";
    private const string Leader = "CMUMobHijackedJoeLeader";
    private const string HijackedJob = "CMUJobHijackedJoe";
    private const string ColonyJoeJob = "AU14JobColonyWorkingJoe";

    [Test]
    public async Task ANonWhitelistedPlayerTakesAHijackedJoe()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var player = pair.Player!;
        var whitelist = server.ResolveDependency<JobWhitelistManager>();
        var previous = server.CfgMan.GetCVar(CCVars.GameRoleWhitelist);
        EntityUid body = default;

        await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.GameRoleWhitelist, true));

        try
        {
            await server.WaitAssertion(() =>
            {
                var ghostRoles = entities.System<GhostRoleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(whitelist.IsAllowed(player, ColonyJoeJob), Is.False,
                        "control: with whitelists on, this player may not play a colony Working Joe");
                    Assert.That(whitelist.IsAllowed(player, HijackedJob), "but the hijacked Joe has no whitelist");
                    Assert.That(ghostRoles.CanPlayForceJob(player, HijackedJob), "nor a playtime gate");
                    Assert.That(ghostRoles.CanJoinForceRole(player, Member, null), "a member seat is open to them");
                    Assert.That(ghostRoles.CanJoinForceRole(player, Leader, null), "and so is the leader's");
                });

                body = entities.SpawnEntity(Member, map.GridCoords);
            });

            await pair.RunTicksSync(5);

            await server.WaitPost(() =>
            {
                var identifier = entities.GetComponent<GhostRoleComponent>(body).Identifier;
                Assert.That(entities.System<GhostRoleSystem>().Takeover(player, identifier), "the ghost role is taken");
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var minds = entities.System<MindSystem>();
                var roles = entities.System<SharedRoleSystem>();
                var playTime = entities.System<PlayTimeTrackingSystem>();

                Assert.That(player.AttachedEntity, Is.EqualTo(body));
                Assert.That(minds.TryGetMind(body, out var mindId, out _));
                Assert.Multiple(() =>
                {
                    Assert.That(roles.MindHasRole<CMUHijackedJoeRoleComponent>(mindId), "the player is a hijacked Joe");
                    Assert.That(playTime.GetTimedRoles(mindId).ToList(), Does.Contain(HijackedJob),
                        "and their time is tracked as a hijacked Joe");
                });
            });
        }
        finally
        {
            await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.GameRoleWhitelist, previous));
        }

        await pair.CleanReturnAsync();
    }
}
