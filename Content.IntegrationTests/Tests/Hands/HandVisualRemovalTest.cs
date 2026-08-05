#nullable enable

using Content.Client.Hands.Systems;
using Content.Shared.Hands.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Hands;

[TestFixture]
[TestOf(typeof(HandsSystem))]
public sealed class HandVisualRemovalTest
{
    private const string HandId = "hand_right";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HandVisualRemovalTestHolder
  components:
  - type: Sprite
  - type: Hands
    hands:
      hand_right:
        location: Right
    sortedHands:
    - hand_right
";

    [Test]
    public async Task RemovingHandClearsInhandVisualLayers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.ResolveDependency<IEntityManager>();
            var handsSys = client.System<HandsSystem>();
            var spriteSys = client.System<SpriteSystem>();
            var holder = entMan.SpawnEntity("HandVisualRemovalTestHolder", MapCoordinates.Nullspace);
            var item = entMan.SpawnEntity("CrowbarRed", MapCoordinates.Nullspace);
            var hands = entMan.GetComponent<HandsComponent>(holder);
            var sprite = entMan.GetComponent<SpriteComponent>(holder);

            handsSys.DoPickup(holder, HandId, item, hands, log: false);

            var revealed = hands.RevealedLayers[HandLocation.Right];
            var layerKeys = revealed.ToArray();
            Assert.That(layerKeys, Is.Not.Empty);
            foreach (var key in layerKeys)
                Assert.That(spriteSys.LayerMapTryGet((holder, sprite), key, out _, false), Is.True);

            handsSys.RemoveHand((holder, hands), HandId);

            Assert.Multiple(() =>
            {
                Assert.That(handsSys.TryGetHand((holder, hands), HandId, out _), Is.False);
                Assert.That(revealed, Is.Empty);
                foreach (var key in layerKeys)
                    Assert.That(spriteSys.LayerMapTryGet((holder, sprite), key, out _, false), Is.False);
            });

            entMan.DeleteEntity(item);
            entMan.DeleteEntity(holder);
        });

        await pair.CleanReturnAsync();
    }
}
