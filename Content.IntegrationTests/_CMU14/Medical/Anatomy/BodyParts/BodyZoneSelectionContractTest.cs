using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class BodyZoneSelectionContractTest : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    [Test]
    public async Task AuthenticatedAimSelectionRemainsUntilTheNextSelection()
    {
        await AssertSelection(null);
        await SendSelection(TargetBodyZone.RightHand);
        await AssertSelection(TargetBodyZone.RightHand);
        await RunSeconds(6);
        await AssertSelection(TargetBodyZone.RightHand);
        await SendSelection(TargetBodyZone.LeftFoot);
        await AssertSelection(TargetBodyZone.LeftFoot);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task InvalidNetworkAndPublicSelectionsPreserveThePreviousChoice(bool selected)
    {
        TargetBodyZone? expected = null;
        if (selected)
        {
            expected = TargetBodyZone.Head;
            await SendSelection(expected.Value);
        }

        await SendSelection((TargetBodyZone) byte.MaxValue);
        await AssertSelection(expected);
        await Server.WaitPost(() => SEntMan.System<SharedBodyZoneTargetingSystem>()
            .SelectZone(SPlayer, (TargetBodyZone) byte.MaxValue));
        await AssertSelection(expected);
    }

    private async Task SendSelection(TargetBodyZone zone)
    {
        await Client.WaitPost(() => Client.ResolveDependency<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new BodyZoneTargetSelectedMessage(zone)));
        await RunUntilSynced();
    }

    private async Task AssertSelection(TargetBodyZone? expected)
    {
        await Server.WaitAssertion(() => Assert.That(SEntMan.System<SharedBodyZoneTargetingSystem>()
            .TryGetExplicitSelection(SPlayer), Is.EqualTo(expected)));
        // Capture a replication barrier after the authenticated command committed.
        await RunUntilSynced();
        await Client.WaitAssertion(() => Assert.That(CEntMan.System<SharedBodyZoneTargetingSystem>()
            .TryGetExplicitSelection(CPlayer), Is.EqualTo(expected)));
    }
}
