using Content.Server._RMC14.Medical.IV;
using Content.Shared._RMC14.Ladder;
using Content.Shared._RMC14.Medical.IV;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Mind;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class DeletedReferenceRegressionTest
{
    [Test]
    public async Task OwnerDeletionImmediatelyClearsIdCardReference()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var owner = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var card = entities.SpawnEntity("CMIDCardStandardDogtag", MapCoordinates.Nullspace);
            var component = entities.GetComponent<IdCardComponent>(card);
            entities.System<SharedIdCardSystem>().TryChangeOriginalOwner(card, owner);
            entities.DeleteEntity(owner);
            Assert.That(component.OriginalOwner, Is.Null, "No PVS packet may carry the dead owner while waiting for a sweep.");
            Assert.DoesNotThrow(() => entities.GetComponentState(entities.EventBus, component, null, GameTick.Zero));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletedPatientDetachesIVWithoutRipFeedback()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity(null, map.GridCoords);
            var iv = entities.SpawnEntity(null, map.GridCoords);
            var component = entities.AddComponent<IVDripComponent>(iv);
            component.AttachedTo = patient;
            entities.DeleteEntity(patient);
            entities.System<IVDripSystem>().Update(0);
            Assert.That(component.AttachedTo, Is.Null);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingAnOwnedOrVisitedBodyLeavesSerializableMindState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var minds = entities.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            var body = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var visitor = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            minds.TransferTo(mind, body);
            minds.Visit(mind, visitor);
            entities.DeleteEntity(visitor);
            Assert.That(mind.Comp.VisitingEntity, Is.Null);
            entities.DeleteEntity(body);
            Assert.That(mind.Comp.OwnedEntity, Is.Null);
            Assert.DoesNotThrow(() => entities.GetComponentState(entities.EventBus, mind.Comp, null, GameTick.Zero));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletedClimberDoesNotBreakLadderNetworkState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var climber = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var ladder = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var component = entities.AddComponent<LadderComponent>(ladder);
            component.LastDoAfterEnt = entities.GetNetEntity(climber);
            component.LastDoAfterId = 1;
            entities.DeleteEntity(climber);
            Assert.DoesNotThrow(() => entities.GetComponentState(entities.EventBus, component, null, GameTick.Zero));
        });
        await pair.CleanReturnAsync();
    }
}
