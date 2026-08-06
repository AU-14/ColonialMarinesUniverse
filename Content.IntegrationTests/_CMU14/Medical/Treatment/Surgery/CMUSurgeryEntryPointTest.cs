using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Interaction;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUSurgeryEntryPointTest
{
    [Test]
    public async Task RmcSurgeryToolRoutesCmuPatientToCmuDispatcher()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid surgeon = default;
        EntityUid patient = default;
        EntityUid scalpel = default;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;
            surgeon = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            scalpel = entities.SpawnEntity("CMScalpel", map.GridCoords);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            entities.System<SkillsSystem>().SetSkill(surgeon, "RMCSkillSurgery", 3);
            entities.System<StandingStateSystem>().Down(
                patient,
                playSound: false,
                dropHeldItems: false,
                force: true);

            var interact = new AfterInteractEvent(
                surgeon,
                scalpel,
                patient,
                entities.GetComponent<TransformComponent>(patient).Coordinates,
                canReach: true);
            entities.EventBus.RaiseLocalEvent(scalpel, interact);

            Assert.Multiple(() =>
            {
                Assert.That(interact.Handled, Is.True);
                Assert.That(entities.HasComponent<CMUSurgeryWindowOpenComponent>(surgeon), Is.True);
                Assert.That(entities.HasComponent<CMUSurgeryWindowOpenComponent>(patient), Is.False);
            });

            entities.DeleteEntity(scalpel);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(surgeon);
        });

        await pair.CleanReturnAsync();
    }
}
