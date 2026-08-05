using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
[TestOf(typeof(HumanoidAppearanceComponent))]
public sealed class CmuHumanVisualBodyTest : GameTest
{
    [Test]
    public async Task HumanSpawnsWithRenderableBodyParts()
    {
        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.Spawn("CMMobHuman");
            var body = SEntMan.GetComponent<BodyComponent>(human);

            Assert.That(SEntMan.HasComponent<HumanoidAppearanceComponent>(human), Is.True);
            Assert.That(SEntMan.HasComponent<VisualBodyComponent>(human), Is.False,
                "The new organ renderer conflicts with CMMobHuman's legacy CMU body-part graph.");

            var parts = SEntMan.EntityQuery<BodyPartComponent>()
                .Count(part => part.Body == human);
            Assert.That(parts, Is.EqualTo(10), "CMMobHuman did not build its complete CMU body-part graph.");
        });
    }

    [Test]
    public async Task HumanSpawnsWithUsableHands()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pullTarget = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var item = SEntMan.SpawnEntity("RMCWeaponRifleM54C", map.GridCoords);
            var hands = SEntMan.System<SharedHandsSystem>();
            var pulling = SEntMan.System<PullingSystem>();
            var handsComponent = SEntMan.GetComponent<HandsComponent>(human);

            Assert.Multiple(() =>
            {
                Assert.That(handsComponent.Count, Is.EqualTo(2));
                Assert.That(hands.TryPickupAnyHand(human, item), Is.True);
                Assert.That(pulling.TryStartPull(human, pullTarget), Is.True);
            });
        });
    }
}
