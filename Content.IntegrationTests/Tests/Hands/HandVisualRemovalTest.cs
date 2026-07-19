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
        await using var pair = await PoolManager.GetServerClient();
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

            var expectedLayers = new[] { "inhand-right", "inhand-right-1" };
            Assert.That(hands.RevealedLayers[HandLocation.Right], Is.EquivalentTo(expectedLayers));
            foreach (var key in expectedLayers)
                Assert.That(spriteSys.LayerMapTryGet((holder, sprite), key, out _, false), Is.True);

            handsSys.RemoveHand((holder, hands), HandId);

            Assert.Multiple(() =>
            {
                Assert.That(hands.Hands.ContainsKey(HandId), Is.False);
                Assert.That(hands.RevealedLayers[HandLocation.Right], Is.Empty);
                foreach (var key in expectedLayers)
                    Assert.That(spriteSys.LayerMapTryGet((holder, sprite), key, out _, false), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
