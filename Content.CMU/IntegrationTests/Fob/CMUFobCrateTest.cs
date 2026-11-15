using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Fob;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Fob;

[TestFixture, TestOf(typeof(CMUFobCrateSystem))]
public sealed class CMUFobCrateTest : InteractionTest
{
    [Test]
    public async Task CrateRequiresCompletedWrenchWorkForClicksAndVerbs()
    {
        await SpawnTarget("CMUFobMonitoringCrate");
        var crate = STarget!.Value;
        var storage = Server.System<SharedEntityStorageSystem>();
        var verbs = Server.System<SharedVerbSystem>();

        await Activate();
        await PlaceInHands("CMScrewdriver");
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(storage.IsOpen(crate), Is.False);
            Assert.That(storage.TryOpenStorage(SPlayer, crate, silent: true), Is.False,
                "The ordinary Open action must not bypass wrenching.");
            Assert.That(verbs.GetLocalVerbs(crate, SPlayer, typeof(InteractionVerb))
                .Any(v => v.Text == Loc.GetString("verb-common-open")), Is.False);
        });

        await PlaceInHands("CMWrench");
        await Interact(awaitDoAfters: false);
        await RunSeconds(0.2f);
        await Drop();
        await RunSeconds(2.2f);
        await Server.WaitAssertion(() => Assert.That(storage.IsOpen(crate), Is.False,
            "Dropping the wrench must cancel opening."));

        await PlaceInHands("CMWrench");
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(storage.IsOpen(crate), Is.True);
            Assert.That(SEntMan.GetComponent<EntityStorageComponent>(crate).Contents.ContainedEntities, Is.Empty,
                "Successful wrench work must use normal storage opening and release the shipment.");
            Assert.That(storage.TryCloseStorage(crate, SPlayer), Is.True);
            Assert.That(storage.TryOpenStorage(SPlayer, crate, silent: true), Is.False,
                "Closing the crate must not permanently remove its wrench requirement.");
            var open = verbs.GetLocalVerbs(crate, SPlayer, typeof(InteractionVerb))
                .Single(v => v.Text == Loc.GetString("cmu-fob-crate-open"));
            verbs.ExecuteVerb(open, SPlayer, crate);
            Assert.That(storage.IsOpen(crate), Is.False, "The menu must start work, not open instantly.");
        });
        await AwaitDoAfters();
        await RunUntilSynced();
        await Server.WaitAssertion(() => Assert.That(storage.IsOpen(crate), Is.True));
        await Client.WaitAssertion(() => Assert.That(
            CEntMan.GetComponent<EntityStorageComponent>(CTarget!.Value).Open, Is.True));
    }
}
