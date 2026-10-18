using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.CharacterDescription;

/// <summary>
///     Synthetics can edit their own character description from their right-click menu, like everyone else.
/// </summary>
[TestFixture]
public sealed class CMUSynthEditDescriptionTest : GameTest
{
    [TestCase("AU14JobCivilianColonySynthetic")]
    [TestCase("AU14JobCivilianColonist")]
    public async Task SpawnedCharacterCanEditTheirDescription(string jobId)
    {
        var map = await Pair.CreateTestMap();
        var previous = ServerSession!.AttachedEntity;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var spawning = Server.System<StationSpawningSystem>();
                var mob = spawning.SpawnPlayerMob(map.GridCoords, new ProtoId<JobPrototype>(jobId), new HumanoidCharacterProfile(), station: null);
                Server.PlayerMan.SetAttachedEntity(ServerSession, mob);

                Assert.That(SEntMan.HasComponent<CharacterDescriptionComponent>(mob), Is.True,
                    $"{jobId} spawned without a character description");

                var text = Loc.GetString("cmu-edit-description-verb");
                var verbs = Server.System<SharedVerbSystem>().GetLocalVerbs(mob, mob, typeof(Verb));
                Assert.That(verbs.Any(v => v.Text == text), Is.True, $"{jobId} has no edit description verb on themselves");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, previous));
        }
    }
}
