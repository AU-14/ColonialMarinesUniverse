#nullable enable

using System;
using Content.Client.DisplacementMap;
using Content.Client.Inventory;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14.Marines.Squads;

[TestFixture, TestOf(typeof(DisplacementMapSystem))]
public sealed class SquadArmorDisplacementTest
{
    private const string GlovesSlot = "gloves";
    private static readonly string SquadGlovesKey =
        $"enum.{nameof(SquadArmorLayers)}.{SquadArmorLayers.Gloves}";

    [Test]
    public async Task SquadArmorDisplacementTargetsExistingLayer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var inventory = client.System<InventorySystem>();
            var spriteSystem = client.System<SpriteSystem>();
            var wearer = entMan.SpawnEntity("CMMobVox", MapCoordinates.Nullspace);
            var gloves = entMan.SpawnEntity("CMHandsBlackMarine", MapCoordinates.Nullspace);

            entMan.EnsureComponent<SquadMemberComponent>(wearer);
            entMan.EnsureComponent<SquadArmorWearerComponent>(wearer);
            Assert.That(inventory.TryEquip(wearer, gloves, GlovesSlot, force: true), Is.True);

            var inventorySlots = entMan.GetComponent<InventorySlotsComponent>(wearer);
            var sprite = entMan.GetComponent<SpriteComponent>(wearer);
            var displacementKey = $"{SquadGlovesKey}-displacement";

            Assert.That(inventorySlots.VisualLayerKeys[GlovesSlot], Does.Contain(displacementKey));
            Assert.That(
                spriteSystem.LayerMapTryGet((wearer, sprite), displacementKey, out var displacementIndex, false),
                Is.True);

            var displacementLayer = (SpriteComponent.Layer) sprite[displacementIndex];
            var copyParameters = displacementLayer.CopyToShaderParameters;
            Assert.That(copyParameters, Is.Not.Null);

            var targetIndex = -1;
            var targetFound = copyParameters!.LayerKey switch
            {
                Enum enumKey => spriteSystem.LayerMapTryGet((wearer, sprite), enumKey, out targetIndex, false),
                string stringKey => spriteSystem.LayerMapTryGet((wearer, sprite), stringKey, out targetIndex, false),
                _ => false,
            };

            Assert.That(copyParameters.LayerKey, Is.EqualTo(SquadGlovesKey));
            Assert.That(targetFound, Is.True);

            var targetLayer = (SpriteComponent.Layer) sprite[targetIndex];
            Assert.That(targetLayer.Shader, Is.Not.Null);

            entMan.DeleteEntity(gloves);
            entMan.DeleteEntity(wearer);
        });

        await pair.CleanReturnAsync();
    }
}
