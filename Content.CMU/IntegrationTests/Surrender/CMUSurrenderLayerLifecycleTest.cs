using System.Linq;
using Content.Client.Body;
using Content.Client.CMU14.Surrender;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Surrender;
using Content.Shared.Body;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Surrender;

[TestFixture]
public sealed class CMUSurrenderLayerLifecycleTest : GameTest
{
    [Test]
    public async Task RepeatedPoseChangesRestoreExactlyTheOriginalLayersAndShaders()
    {
        var map = await Pair.CreateTestMap();
        var attached = ServerSession!.AttachedEntity;
        EntityUid human = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(ServerSession, human);
                var inventory = Server.System<InventorySystem>();
                Assert.That(inventory.TryEquip(human,
                    SEntMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords), "jumpsuit", force: true), Is.True);
                Assert.That(inventory.TryEquip(human,
                    SEntMan.SpawnEntity("CMArmorM3Medium", map.GridCoords), "outerClothing", force: true), Is.True);
                Assert.That(inventory.TryEquip(human,
                    SEntMan.SpawnEntity("CMBeltMarine", map.GridCoords), "belt", force: true), Is.True);
            });
            await Pair.RunUntilSynced();
            await Pair.RunTicksSync(2);
            var client = Pair.ToClientUid(human);
            await Client.WaitAssertion(() =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(client);
                var layers = sprite.AllLayers.OfType<SpriteComponent.Layer>().ToArray();
                var shaders = layers.Select(layer => layer.Shader).ToArray();
                var visuals = Client.System<CMUSurrenderVisualsSystem>();
                for (var cycle = 0; cycle < 100; cycle++)
                {
                    CEntMan.AddComponent<CMUSurrenderComponent>(client);
                    visuals.FrameUpdate(0);
                    Assert.That(sprite.AllLayers.Count(), Is.GreaterThan(layers.Length));
                    AssertShaderTargetsExist((client, sprite));
                    CEntMan.RemoveComponent<CMUSurrenderComponent>(client);
                    Assert.That(sprite.AllLayers, Is.EqualTo(layers), $"Layer ownership changed on cycle {cycle}.");
                    Assert.That(sprite.AllLayers.OfType<SpriteComponent.Layer>().Select(layer => layer.Shader), Is.EqualTo(shaders));
                    AssertShaderTargetsExist((client, sprite));
                }
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, attached));
            if (human != default)
                await Pair.DeleteEntityTreeLeafFirst(human);
        }
    }

    [TestCase("CMUUndershirtOlive")]
    [TestCase("CMUUndershirtWhite")]
    public async Task LocalMarkingReplacementCannotLeaveDanglingShaderTargets(string replacement)
    {
        var map = await Pair.CreateTestMap();
        var attached = ServerSession!.AttachedEntity;
        EntityUid human = default;
        ProtoId<OrganCategoryPrototype> category = default;
        const string oliveKey = "CMUUndershirtOlive-undershirt_uscm_short";
        try
        {
            await Server.WaitPost(() =>
            {
                human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(ServerSession, human);
                var torso = Server.System<BodySystem>().EnumerateOrgans<VisualOrganMarkingsComponent>(human)
                    .Single(organ => organ.Comp2.MarkingData.Layers.Contains(HumanoidVisualLayers.UndergarmentTop));
                category = SEntMan.GetComponent<OrganComponent>(torso.Owner).Category!.Value;
                Server.System<Content.Server.Body.VisualBodySystem>().ApplyMarkings(human, new()
                {
                    [category] = new() { [HumanoidVisualLayers.UndergarmentTop] = [new Marking("CMUUndershirtOlive", 1)] },
                });
            });
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>()
                .TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();

            var client = Pair.ToClientUid(human);
            await Client.WaitAssertion(() =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(client);
                var sprites = Client.System<SpriteSystem>();
                var visuals = Client.System<CMUSurrenderVisualsSystem>();
                visuals.FrameUpdate(0);
                Assert.That(sprites.LayerMapTryGet((client, sprite), $"cmu-surrender-{oliveKey}", out _, false), Is.True);

                // This public client path rebuilds markings without raising VisualBodySpriteRefreshEvent.
                Client.System<VisualBodySystem>().ApplyMarkings(client, new()
                {
                    [category] = new() { [HumanoidVisualLayers.UndergarmentTop] = [new Marking(replacement, 1)] },
                });
                Assert.That(sprites.LayerMapTryGet((client, sprite), $"cmu-surrender-{oliveKey}", out _, false), Is.False);
                visuals.FrameUpdate(0);
                AssertShaderTargetsExist((client, sprite));

                var rsi = (SpriteSpecifier.Rsi) CProtoMan.Index<MarkingPrototype>(replacement).Sprites.Single();
                var key = $"cmu-surrender-{replacement}-{rsi.RsiState}";
                Assert.That(sprites.TryGetLayer((client, sprite), key, out var target, false), Is.True);
                Assert.That(sprites.TryGetLayer((client, sprite), key + "-displacement", out _, false), Is.True);
                Assert.That(target!.ShaderPrototype?.Id, Is.EqualTo("CMUSurrenderDraw"));

                // Cleanup must own the layer itself, even when another visual refresh removes its map key.
                sprites.LayerMapRemove((client, sprite), key + "-displacement");
            });

            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>()
                .TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(client);
                AssertShaderTargetsExist((client, sprite));
                Assert.That(sprite.AllLayers.OfType<SpriteComponent.Layer>().Any(layer =>
                    layer.CopyToShaderParameters?.LayerKey is string key && key.StartsWith("cmu-surrender-")), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, attached));
            if (human != default)
                await Pair.DeleteEntityTreeLeafFirst(human);
        }
    }

    private void AssertShaderTargetsExist(Entity<SpriteComponent> ent)
    {
        foreach (var layer in ent.Comp.AllLayers.OfType<SpriteComponent.Layer>())
        {
            if (layer.CopyToShaderParameters?.LayerKey is string key)
            {
                Assert.That(Client.System<SpriteSystem>().LayerMapTryGet(ent.AsNullable(), key, out _, false), Is.True,
                    $"Drawing this displacement layer would throw KeyNotFoundException for '{key}'.");
            }
        }
    }
}
