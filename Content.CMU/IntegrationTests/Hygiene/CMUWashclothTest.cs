using Content.Server.CMU14.Hygiene;
using Content.Shared.CMU14.Hygiene;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
namespace Content.IntegrationTests.CMU14.Hygiene;

[TestFixture]
public sealed class CMUWashclothTest
{
    [Test]
    public async Task EachWipeCleansSkinAndTwoUsesExhaustCloth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid human = default, cloth = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("CMMobHuman", map.GridCoords);
            cloth = em.SpawnEntity("CMUFieldWashcloth", map.GridCoords);
            Assert.That(em.System<SharedHandsSystem>().TryPickup(human, cloth), Is.True);
            em.System<CMUHygieneSystem>().ApplyAlienResidue(human);
            var residue = em.GetComponent<CMUResidueComponent>(human);
            residue.Buildup = 1f;
            em.EventBus.RaiseLocalEvent(cloth, new UseInHandEvent(human));
        });
        await pair.RunTicksSync(330);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<CMUWashclothComponent>(cloth).UsesRemaining, Is.EqualTo(1));
            Assert.That(em.GetComponent<CMUResidueComponent>(human).Buildup, Is.Zero);
            Assert.That(em.GetComponent<CMUResidueComponent>(human).Color, Is.Null);
            em.System<CMUHygieneSystem>().ApplyAlienResidue(human);
            em.EventBus.RaiseLocalEvent(cloth, new UseInHandEvent(human));
        });
        await pair.RunTicksSync(330);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<CMUWashclothComponent>(cloth).UsesRemaining, Is.Zero);
            Assert.That(em.GetComponent<CMUResidueComponent>(human).Color, Is.Null);
            // A spent cloth cannot clean fresh contamination.
            em.System<CMUHygieneSystem>().ApplyAlienResidue(human);
            em.EventBus.RaiseLocalEvent(cloth, new UseInHandEvent(human));
        });
        await pair.RunTicksSync(330);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<CMUResidueComponent>(human).Color, Is.Not.Null);
            server.EntMan.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}
