using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14.Squads;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Squads;

/// <summary>
///     Round-start Weyland-Yutani staff used to join the Corporate squad only if a third party had
///     already created it, so the WY Overwatch console stayed empty. The first one in now creates it.
/// </summary>
[TestFixture]
[TestOf(typeof(CMUAssignSquadSystem))]
public sealed class CMUAssignSquadTest
{
    [TestCase("AU14JobCivilianCorporateLiaison", "CMUSquadWYCorporate")]
    [TestCase("AU14JobCivilianScientist", "CMUSquadWYScience")]
    [TestCase("AU14JobWYGuard", "CMUSquadWYSecurity")]
    public async Task FirstSpawnCreatesAndJoinsWYSquad(string jobId, string squadId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var squads = server.System<SquadSystem>();
            // Pooled servers can carry a squad over from another test; start from none.
            if (squads.TryGetSquad(squadId, out var existing))
                entities.DeleteEntity(existing);

            var profile = new HumanoidCharacterProfile();
            ProtoId<JobPrototype> job = jobId;
            var mob = server.System<StationSpawningSystem>().SpawnPlayerMob(map.GridCoords, job, profile, station: null);
            try
            {
                var spawned = new PlayerSpawnCompleteEvent(mob, pair.Player!, jobId, false, true, 1, map.MapUid, profile);
                entities.EventBus.RaiseLocalEvent(mob, spawned, broadcast: true);

                Assert.That(squads.TryGetSquad(squadId, out var squad), Is.True, "Spawning did not create the squad.");
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<SquadMemberComponent>(mob).Squad, Is.EqualTo(squad.Owner));
                    Assert.That(squad.Comp.Group, Is.EqualTo("WeYa"), "The squad would not show on the WY Overwatch console.");
                });
            }
            finally
            {
                entities.DeleteEntity(mob);
            }
        });
        await pair.CleanReturnAsync();
    }
}
