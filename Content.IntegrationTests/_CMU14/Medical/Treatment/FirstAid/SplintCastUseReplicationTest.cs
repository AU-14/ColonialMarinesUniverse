#pragma warning disable RA0002 // Assertions read authoritative/replicated use counters and inspect the actual running DoAfter.
using System.Linq;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using ClientExamineSystem = Content.Client.Examine.ExamineSystem;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.FirstAid;

[TestFixture]
public sealed class SplintCastUseReplicationTest
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task RealTreatmentReplicatesInitialAndChangedUsesExamineAndDepletion(bool cast, bool consumeBeforeFirstSnapshot)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        var player = pair.Player!;
        var originalPlayer = player.AttachedEntity;
        EntityUid medic = default, patient = default, item = default, selected = default, other = default;
        NetEntity itemNet = default, medicNet = default;
        var initialUses = consumeBeforeFirstSnapshot ? 3 : 4;
        var usesLocale = cast ? "cmu-cast-item-uses-remaining" : "cmu-splint-item-uses-remaining";
        try
        {
            await server.WaitAssertion(() =>
            {
                medic = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                item = entities.SpawnEntity(cast ? "CMUCastItem" : "CMUSplintItem", map.GridCoords);
                entities.EnsureComponent<CMInStasisComponent>(patient);
                server.PlayerMan.SetAttachedEntity(player, medic);
                Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(medic, item, checkActionBlocker: false), Is.True);
                var anatomy = entities.System<CMUMedicalBodyIndexSystem>();
                Assert.That(anatomy.TryGetBodyPart(patient,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out selected), Is.True);
                Assert.That(anatomy.TryGetBodyPart(patient,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Right), out other), Is.True);
                Assert.That(entities.System<SharedBoneSystem>().SeedFracture(selected, FractureSeverity.Simple), Is.True);
                Assert.That(entities.System<SharedBoneSystem>().SeedFracture(other, FractureSeverity.Simple), Is.True);
                entities.System<SharedBodyZoneTargetingSystem>().SelectZone((medic, null), TargetBodyZone.LeftArm);
                Assert.That(ServerUses(), Is.EqualTo(4), "Both shipped resource items have four uses; do not alter their balance in the fixture.");
                itemNet = entities.GetNetEntity(item);
                medicNet = entities.GetNetEntity(medic);
                if (consumeBeforeFirstSnapshot)
                {
                    entities.System<TagSystem>().AddTag(medic, "InstantDoAfters");
                    ApplyInteraction();
                    entities.System<TagSystem>().RemoveTag(medic, "InstantDoAfters");
                    Assert.That(ServerUses(), Is.EqualTo(3));
                }
            });
            // First visibility must carry the live count, including an item that
            // was already used before this client received its initial state.
            await AssertClientUses(initialUses);

            await server.WaitAssertion(() =>
            {
                // An uninjured target rejects the actual interaction and spends nothing.
                var rejected = new AfterInteractEvent(medic, item, medic, default, true);
                entities.EventBus.RaiseLocalEvent(item, rejected);
                Assert.That(rejected.Handled, Is.False);
                Assert.That(ServerUses(), Is.EqualTo(initialUses));

                // Use the ordinary configured delay for a real cancellation; no
                // completion event or spent-charge value is fabricated.
                ApplyInteraction();
                var doAfter = entities.System<SharedDoAfterSystem>();
                var running = entities.GetComponent<DoAfterComponent>(medic).DoAfters.Values
                    .Single(operation => doAfter.IsRunning(operation.Id));
                doAfter.Cancel(running.Id);
                Assert.That(doAfter.IsRunning(running.Id), Is.False);
                Assert.That(ServerUses(), Is.EqualTo(initialUses));
                Assert.That(Supported(other), Is.False);
            });
            await AssertClientUses(initialUses);

            await server.WaitAssertion(() => entities.System<TagSystem>().AddTag(medic, "InstantDoAfters"));
            for (var remaining = initialUses - 1; remaining >= 1; remaining--)
            {
                var expected = remaining;
                await server.WaitAssertion(() =>
                {
                    ApplyInteraction();
                    Assert.That(Supported(selected), Is.True);
                    Assert.That(Supported(other), Is.False, "Persistent site selection must remain on the treated arm.");
                    Assert.That(ServerUses(), Is.EqualTo(expected));
                    Assert.That(entities.IsQueuedForDeletion(item), Is.False);
                });
                // Includes the singular examine form at one remaining use.
                await AssertClientUses(expected);
            }

            await server.WaitAssertion(() =>
            {
                ApplyInteraction();
                Assert.That(ServerUses(), Is.Zero);
                Assert.That(entities.IsQueuedForDeletion(item), Is.True);
                // The final decrement queues deletion. Another same-tick caller
                // cannot turn that empty cast/splint into a free extra treatment.
                var treatment = entities.System<SharedCMUSplintItemSystem>();
                var repeated = cast
                    ? treatment.ApplyCastToPart((item, entities.GetComponent<CMUCastItemComponent>(item)), other)
                    : treatment.ApplySplintToPart((item, entities.GetComponent<CMUSplintItemComponent>(item)), other);
                Assert.That(repeated, Is.False);
                Assert.That(Supported(other), Is.False);
                Assert.That(ServerUses(), Is.Zero);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() => Assert.That(pair.Client.EntMan.TryGetEntity(itemNet, out _), Is.False));
            await server.WaitAssertion(() => Assert.That(entities.EntityExists(item), Is.False));
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(player, originalPlayer);
                foreach (var uid in new[] { item, patient, medic })
                    if (entities.EntityExists(uid)) entities.DeleteEntity(uid);
            });
        }
        await pair.RunUntilSynced();
        await pair.CleanReturnAsync();

        int ServerUses() => cast
            ? entities.GetComponent<CMUCastItemComponent>(item).Uses
            : entities.GetComponent<CMUSplintItemComponent>(item).Uses;

        bool Supported(EntityUid part) => cast
            ? entities.HasComponent<CMUCastComponent>(part)
            : entities.HasComponent<CMUSplintedComponent>(part);

        void ApplyInteraction()
        {
            var interact = new AfterInteractEvent(medic, item, patient, default, true);
            entities.EventBus.RaiseLocalEvent(item, interact);
            Assert.That(interact.Handled, Is.True);
        }

        async Task AssertClientUses(int expected)
        {
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var clientEntities = pair.Client.EntMan;
                var clientItem = clientEntities.GetEntity(itemNet);
                var clientMedic = clientEntities.GetEntity(medicNet);
                var uses = cast
                    ? clientEntities.GetComponent<CMUCastItemComponent>(clientItem).Uses
                    : clientEntities.GetComponent<CMUSplintItemComponent>(clientItem).Uses;
                Assert.That(uses, Is.EqualTo(expected), "Read the actual replicated component, not the server object.");
                var examine = clientEntities.System<ClientExamineSystem>();
                Assert.That(examine.IsInDetailsRange(clientMedic, clientItem), Is.True);
                var text = examine.GetExamineText(clientItem, clientMedic).ToMarkup();
                Assert.That(text, Does.Contain(Loc.GetString(usesLocale, ("uses", expected))));
                if (expected != 4)
                    Assert.That(text, Does.Not.Contain(Loc.GetString(usesLocale, ("uses", 4))), "Client examine cannot keep displaying the prototype's original count.");
            });
        }
    }
}
